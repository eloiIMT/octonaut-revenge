// Assets/Scripts/TentacleCurveIK.cs
using System.Collections.Generic;
using UnityEngine;

// Solveur de forme pour les tentacules-jambes, à la place du Chain IK.
// Le Chain IK (FABRIK) part de la pose précédente et ne plie que le strict nécessaire :
// la tentacule restait à plat et seul le bout se cassait vers le sol.
// Ici, la tentacule entière suit une courbe de Bézier racine -> pied (une arche),
// dont la hauteur est ajustée pour que sa longueur égale celle de la tentacule.
// Tourne en LateUpdate, après le Rig : c'est cette pose qui est rendue.
[RequireComponent(typeof(TentacleStepper))]
public class TentacleCurveIK : MonoBehaviour
{
    const int CurveSamples = 32;
    const int SearchSamples = 16;
    const int SearchIterations = 14;

    private TentacleStepper stepper;
    private TentacleIKSettings settings;

    private Transform[] bones;          // racine ... bout
    private Quaternion[] bindLocalRot;  // pose de liaison, réappliquée chaque frame (pas de dérive)
    private Vector3[] childAxis;        // direction vers l'os suivant, en espace local de l'os
    private float[] segLen;
    private float[] cumLen;             // longueur cumulée racine -> articulation k
    private float chainLength;

    private Vector3[] joints;
    private readonly Vector3[] samples = new Vector3[CurveSamples + 1];
    private readonly float[] sampleLen = new float[CurveSamples + 1];

    // Poignées de la courbe lissées, en espace du corps (le déplacement du corps ne crée pas de retard)
    private Vector3 startHandleLocal;
    private Vector3 endHandleLocal;
    private bool hasHandles;
    private float wavePhase;

    void Start()
    {
        stepper = GetComponent<TentacleStepper>();
        settings = stepper.settings;
        var data = stepper.chainConstraint.data;

        var chain = new List<Transform>();
        for (Transform t = data.tip; t != null; t = t.parent)
        {
            chain.Insert(0, t);
            if (t == data.root) break;
        }
        bones = chain.ToArray();

        int n = bones.Length;
        bindLocalRot = new Quaternion[n];
        childAxis = new Vector3[n];
        segLen = new float[n];
        cumLen = new float[n];
        joints = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            bindLocalRot[i] = bones[i].localRotation;
            if (i < n - 1)
            {
                childAxis[i] = bones[i + 1].localPosition.normalized;
                segLen[i] = Vector3.Distance(bones[i].position, bones[i + 1].position);
            }
            if (i > 0) cumLen[i] = cumLen[i - 1] + segLen[i - 1];
        }
        chainLength = cumLen[n - 1];

        wavePhase = Random.value * 10f; // désynchronise les tentacules entre elles
    }

    void LateUpdate()
    {
        // Le Chain IK ne sert que si le solveur de courbe est désactivé (pour comparer)
        stepper.chainConstraint.weight = settings.useCurveSolver ? 0f : 1f;
        if (!settings.useCurveSolver || bones.Length < 3) return;

        float dt = Time.deltaTime;
        wavePhase += dt * settings.waveSpeed;

        // 1. Repartir de la pose de liaison : la direction naturelle de la tentacule suit le corps
        for (int i = 0; i < bones.Length; i++)
            bones[i].localRotation = bindLocalRot[i];

        Vector3 root = bones[0].position;
        Vector3 foot = stepper.ikTarget.position;

        // 2. Forme : arche racine -> pied, puis ondulation
        BuildCurve(root, foot, dt);
        PlaceJoints(root, foot);

        // 3. Quelques passes FABRIK pour des longueurs d'os exactes, racine et pied fixés.
        //    Elles partent de la courbe, donc la forme est conservée.
        SolveLengths(root, foot);

        // 4. Orienter chaque os vers l'articulation suivante (de la racine vers le bout)
        for (int i = 0; i < bones.Length - 1; i++)
        {
            Vector3 current = bones[i].rotation * childAxis[i];
            Vector3 wanted = joints[i + 1] - bones[i].position;
            bones[i].rotation = Quaternion.FromToRotation(current, wanted) * bones[i].rotation;
        }
    }

    void BuildCurve(Vector3 root, Vector3 foot, float dt)
    {
        Vector3 up = Vector3.up;
        Vector3 natural = Vector3.ProjectOnPlane(bones[0].rotation * childAxis[0], up).normalized;
        Vector3 toFoot = Vector3.ProjectOnPlane(foot - root, up);
        Vector3 toFootDir = toFoot.sqrMagnitude > 1e-4f ? toFoot.normalized : natural;

        float distance = toFoot.magnitude;

        // Parties horizontales des poignées, bornées par la distance racine -> pied : la courbe ne peut
        // pas dépasser le pied puis revenir (c'est ce qui repliait la tentacule sur elle-même).
        // Pied presque sous la racine : la tentacule bombe vers l'extérieur dans sa direction naturelle.
        // Près de la racine, la direction du pied devient instable : on revient à la direction naturelle.
        float aim = settings.rootAim * Mathf.Clamp01(distance / (chainLength * 0.5f));
        Vector3 outDir = natural * (1f - aim) + toFootDir * aim;
        outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : natural;

        // Bombé : les deux poignées partent vers l'extérieur, pour une boucle ronde au lieu d'une épingle
        float bulge = Mathf.Max(0f, chainLength * settings.minBulge - distance * settings.rootReach);
        Vector3 startFlat = outDir * (distance * settings.rootReach + bulge);
        Vector3 endFlat = -toFootDir * distance * settings.footReach + outDir * bulge;

        // Pendant un pas, le bout traîne derrière le mouvement (poignée côté pied décalée vers l'avant)
        if (stepper.IsStepping)
        {
            float lift = Mathf.Sin(stepper.StepProgress * Mathf.PI);
            endFlat += stepper.StepDirection * settings.stepTipDrag * lift * chainLength * 0.25f;
        }

        // Lissage des parties horizontales dans l'espace du corps : la forme a de l'inertie
        // (le corps qui avance ne crée pas de retard). La hauteur, elle, est recalculée juste après
        // à chaque frame : lisser la courbe entière la raccourcissait, et l'excédent de longueur
        // se repliait en zigzag.
        Transform space = stepper.body;
        Vector3 startLocal = space.InverseTransformDirection(startFlat);
        Vector3 endLocal = space.InverseTransformDirection(endFlat);
        if (!hasHandles || settings.shapeSmoothing <= 0f)
        {
            startHandleLocal = startLocal;
            endHandleLocal = endLocal;
            hasHandles = true;
        }
        else
        {
            float k = 1f - Mathf.Exp(-settings.shapeSmoothing * dt);
            startHandleLocal = Vector3.Lerp(startHandleLocal, startLocal, k);
            endHandleLocal = Vector3.Lerp(endHandleLocal, endLocal, k);
        }
        startFlat = space.TransformDirection(startHandleLocal);
        endFlat = space.TransformDirection(endHandleLocal);

        // Seule la hauteur de l'arche varie : la longueur de la courbe croît avec elle,
        // on la cherche par dichotomie pour qu'elle égale exactement la longueur de la tentacule.
        float height = 0f;
        if (Vector3.Distance(root, foot) < chainLength)
        {
            float lo = 0f, hi = chainLength;
            for (int it = 0; it < SearchIterations; it++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 a = root + startFlat + up * mid;
                Vector3 b = foot + endFlat + up * mid * settings.endHandleRatio;
                if (CurveLength(root, a, b, foot) < chainLength) lo = mid;
                else hi = mid;
            }
            height = (lo + hi) * 0.5f;
        }

        Vector3 p1 = root + startFlat + up * height;
        Vector3 p2 = foot + endFlat + up * height * settings.endHandleRatio;

        // Table longueur d'arc -> point, pour répartir les os
        samples[0] = root;
        sampleLen[0] = 0f;
        for (int s = 1; s <= CurveSamples; s++)
        {
            samples[s] = Bezier(root, p1, p2, foot, s / (float)CurveSamples);
            sampleLen[s] = sampleLen[s - 1] + Vector3.Distance(samples[s - 1], samples[s]);
        }
    }

    void PlaceJoints(Vector3 root, Vector3 foot)
    {
        Vector3 up = Vector3.up;
        Vector3 side = Vector3.Cross(up, foot - root);
        side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.Cross(up, bones[0].rotation * childAxis[0]).normalized;

        float lift = stepper.IsStepping ? Mathf.Sin(stepper.StepProgress * Mathf.PI) : 0f;
        float amplitude = settings.waveAmplitude + settings.waveStepAmplitude * lift;

        // Répartition proportionnelle : si la courbe (lissée) est un peu plus courte ou plus longue
        // que la tentacule, les os s'étalent quand même sur toute la courbe au lieu de s'empiler au bout.
        float curveToChain = sampleLen[CurveSamples] / chainLength;
        int s = 1;
        for (int k = 0; k < bones.Length; k++)
        {
            float target = cumLen[k] * curveToChain;
            while (s < CurveSamples && sampleLen[s] < target) s++;
            float span = sampleLen[s] - sampleLen[s - 1];
            float f = span > 1e-6f ? (target - sampleLen[s - 1]) / span : 0f;
            Vector3 p = Vector3.Lerp(samples[s - 1], samples[s], f);

            // Onde qui court le long de la tentacule, nulle à la racine et au pied
            float u = cumLen[k] / chainLength;
            float envelope = Mathf.Sin(u * Mathf.PI);
            float wave = wavePhase - u * settings.waveFrequency * 2f * Mathf.PI;
            p += (side * Mathf.Sin(wave) + up * 0.5f * Mathf.Cos(wave)) * amplitude * envelope;

            joints[k] = p;
        }
    }

    void SolveLengths(Vector3 root, Vector3 foot)
    {
        int n = joints.Length;
        for (int it = 0; it < settings.fabrikIterations; it++)
        {
            joints[n - 1] = foot;
            for (int k = n - 2; k >= 0; k--)
                joints[k] = joints[k + 1] + (joints[k] - joints[k + 1]).normalized * segLen[k];

            joints[0] = root;
            for (int k = 1; k < n; k++)
                joints[k] = joints[k - 1] + (joints[k] - joints[k - 1]).normalized * segLen[k - 1];
        }
    }

    static float CurveLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float len = 0f;
        Vector3 prev = p0;
        for (int s = 1; s <= SearchSamples; s++)
        {
            Vector3 p = Bezier(p0, p1, p2, p3, s / (float)SearchSamples);
            len += Vector3.Distance(prev, p);
            prev = p;
        }
        return len;
    }

    static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }
}
