// Assets/Scripts/TentacleStepper.cs
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Après TPSController (ordre 0) : le corps a déjà bougé ce frame quand on replace le pied.
// Le Chain IK est résolu plus tard, dans la passe d'animation, donc il voit la bonne cible.
[DefaultExecutionOrder(100)]
public class TentacleStepper : MonoBehaviour
{
    [Header("Références")]
    public ChainIKConstraint chainConstraint;
    public Transform ikTarget;
    public Transform body;
    public Transform restAnchor;
    public LayerMask groundLayer;

    [Header("Configuration partagée")]
    public TentacleIKSettings settings;

    [Header("Groupe")]
    public TentacleGroupManager groupManager;
    public int groupId;

    // Position MONDE du pied. C'est la source de vérité : ikTarget est enfant du corps,
    // donc on la réécrit chaque frame, sinon le pied glisse avec le corps au lieu de rester planté.
    private Vector3 footPos;
    private bool planted;           // pied posé sur du sol (faux quand il pend au-dessus du vide)

    private Vector3 stepStartPos;
    private Vector3 stepStartLocal;  // point de décollage, en espace du corps
    private Vector3 stepEndPos;
    private bool stepEndValid;
    private float stepTimer;
    private bool isStepping;

    private Vector3 lastAnchorPos;
    private Vector3 anchorVelocity; // vitesse horizontale lissée de l'ancre (translation + rotation du corps)
    private float settleTimer;
    private float chainLength;      // longueur de la chaîne d'os, racine -> bout

    public Vector3 FootPosition => footPos;
    public bool IsPlanted => planted && !isStepping;
    public bool IsStepping => isStepping;
    public float StepProgress => isStepping ? Mathf.Clamp01(stepTimer) : 0f;
    public Vector3 StepDirection { get; private set; } // direction horizontale du pas en cours

    // Centre du corps, calculé par le TentacleGroupManager à partir des racines des 6 tentacules
    public Vector3 BodyCenter => body.TransformPoint(groupManager.BodyCenterLocal);

    // Écarte un point (à plat) du centre du corps jusqu'à `radius`. La poussée se fait du côté de la
    // tentacule : les tentacules de gauche contournent par la gauche, celles de droite par la droite.
    // Continue au bord du cercle : pas de saut quand un point y entre.
    public Vector3 PushOutOfBody(Vector3 point, float radius)
    {
        Vector3 center = BodyCenter;
        Vector3 flat = Vector3.ProjectOnPlane(point - center, Vector3.up);
        float dist = flat.magnitude;
        if (dist >= radius) return point;

        Vector3 side = Vector3.ProjectOnPlane(chainConstraint.data.root.position - center, Vector3.up).normalized;
        float depth = 1f - dist / radius; // 0 au bord, 1 au centre
        Vector3 dir = (dist > 1e-4f ? flat / dist : side) * (1f - depth) + side * depth;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : side;

        Vector3 pushed = center + dir * radius;
        pushed.y = point.y;
        return pushed;
    }

    void Start()
    {
        ApplyChainSettings();
        lastAnchorPos = restAnchor.position;
        if (chainConstraint != null)
            groupManager.RegisterRoot(body.InverseTransformPoint(chainConstraint.data.root.position));

        planted = TryGetGround(restAnchor.position, out footPos);
        if (!planted) footPos = restAnchor.position;
        ikTarget.position = footPos;
    }

    void OnDisable()
    {
        // Ne pas laisser le groupe bloqué "en l'air" si la tentacule est désactivée en plein pas
        if (isStepping && groupManager != null) groupManager.EndStep(groupId);
        isStepping = false;
    }

    void ApplyChainSettings()
    {
        if (chainConstraint == null) return;
        chainConstraint.data.chainRotationWeight = settings.chainRotationWeight;
        chainConstraint.data.tipRotationWeight = settings.tipRotationWeight;
        chainConstraint.data.maxIterations = settings.maxIterations;
        chainConstraint.data.tolerance = settings.tolerance;

        chainLength = 0f;
        for (Transform t = chainConstraint.data.tip; t != null && t != chainConstraint.data.root; t = t.parent)
            chainLength += Vector3.Distance(t.position, t.parent.position);
    }

    // Pas de repli sur une ancienne position : si le raycast rate, l'appelant le sait.
    bool TryGetGround(Vector3 anchorPos, out Vector3 point)
    {
        Vector3 rayOrigin = anchorPos + Vector3.up * settings.raycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, settings.raycastDistance, groundLayer))
        {
            point = hit.point;
            return true;
        }
        point = anchorPos;
        return false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateAnchorVelocity(dt);

        if (isStepping)
            UpdateStep(dt);
        else if (TryGetGround(restAnchor.position, out Vector3 restPoint))
            TryStartStep(restPoint, dt);
        else
        {
            // Au-dessus du vide : le pied pend sous son ancre au lieu de viser un vieux point au sol
            planted = false;
            footPos = Vector3.Lerp(footPos, restAnchor.position, 1f - Mathf.Exp(-settings.danglingFollowSpeed * dt));
        }

        if (planted && !isStepping)
        {
            SlideFootOutOfBody();
            SlideFootWithinReach();
        }

        ikTarget.position = footPos;
    }

    // Un pied posé que le corps rattrape (il attend son tour) ne passe pas sous le buste :
    // il glisse sur le bord de la zone d'évitement, de son côté.
    void SlideFootOutOfBody()
    {
        Vector3 pushed = PushOutOfBody(footPos, settings.bodyClearance);
        if (pushed == footPos) return;
        footPos = TryGetGround(pushed, out Vector3 ground) ? ground : pushed;
    }

    // Un pied posé qui attend son tour ne doit jamais tendre la tentacule toute droite :
    // au bout de son allonge, il glisse au sol derrière le corps (comme une ventouse qui ripe).
    void SlideFootWithinReach()
    {
        if (chainLength <= 0f) return;

        Vector3 root = chainConstraint.data.root.position;
        float maxReach = chainLength * settings.slideReachRatio;
        if (Vector3.Distance(root, footPos) <= maxReach) return;

        float height = root.y - footPos.y;
        float maxFlat = Mathf.Sqrt(Mathf.Max(0f, maxReach * maxReach - height * height));
        Vector3 flat = Vector3.ProjectOnPlane(footPos - root, Vector3.up);
        Vector3 slid = root + flat.normalized * maxFlat;
        slid.y = footPos.y;

        footPos = TryGetGround(slid, out Vector3 ground) ? ground : slid;
    }

    void UpdateAnchorVelocity(float dt)
    {
        Vector3 delta = restAnchor.position - lastAnchorPos;
        lastAnchorPos = restAnchor.position;
        delta.y = 0f;
        anchorVelocity = Vector3.Lerp(anchorVelocity, delta / dt, 1f - Mathf.Exp(-settings.velocitySmoothing * dt));
    }

    void TryStartStep(Vector3 restPoint, float dt)
    {
        // Retour du vide : on atterrit tout de suite, sans attendre son tour
        if (!planted)
        {
            StartStep();
            return;
        }

        Vector3 offset = restPoint - footPos;
        float horizontal = new Vector2(offset.x, offset.z).magnitude;
        float vertical = Mathf.Abs(offset.y);

        bool moving = anchorVelocity.sqrMagnitude > 0.01f;
        settleTimer = moving ? 0f : settleTimer + dt;

        // Allonge : les ancres arrière sont déjà loin de la racine, la tentacule se tendait toute droite
        // avant d'atteindre stepThreshold. On déclenche aussi le pas quand elle approche de sa longueur max
        // (seulement si le point de repos est plus confortable, sinon elle ferait des pas en boucle).
        bool overReach = false;
        if (chainLength > 0f)
        {
            Vector3 root = chainConstraint.data.root.position;
            float footReach = Vector3.Distance(root, footPos) / chainLength;
            float restReach = Vector3.Distance(root, restPoint) / chainLength;
            overReach = footReach > settings.comfortReachRatio && footReach > restReach + 0.05f;

            // Le pied arrive sous le corps : il est temps de le déplacer
            Vector3 fromCenter = Vector3.ProjectOnPlane(footPos - BodyCenter, Vector3.up);
            if (fromCenter.magnitude < settings.bodyClearance + 0.05f) overReach = true;
        }

        // Seuil mesuré à plat : la hauteur (arc, relief) ne doit pas masquer l'écart horizontal
        bool overdue = horizontal > settings.stepThreshold || vertical > settings.stepThreshold || overReach;
        bool settle = settleTimer > settings.settleDelay && horizontal > settings.settleThreshold;
        bool joinGroup = horizontal > settings.stepThreshold * settings.groupJoinFraction
                         && groupManager.IsGroupOpen(groupId, settings.groupJoinWindow);
        // Sécurité : trop étirée, la tentacule n'atteint plus sa cible -> on ignore l'alternance
        bool overstretched = chainLength > 0f
            && Vector3.Distance(chainConstraint.data.root.position, footPos) > chainLength * settings.maxReachRatio;

        if (((overdue || settle) && groupManager.CanStep(groupId, settings.turnGrace)) || joinGroup || overstretched)
            StartStep();
    }

    void StartStep()
    {
        isStepping = true;
        stepTimer = 0f;
        stepStartPos = footPos;
        stepStartLocal = body.InverseTransformPoint(footPos);
        stepEndValid = TryGetLandingPoint(out stepEndPos);
        groupManager.BeginStep(groupId);
    }

    void UpdateStep(float dt)
    {
        stepTimer += dt / CurrentStepDuration();
        float t = Mathf.Clamp01(stepTimer);

        // L'ancre avance avec le corps pendant le pas : on recalcule le point d'atterrissage
        // pour que le pied se pose bien devant le corps à la fin, et pas là où il était au décollage.
        if (TryGetLandingPoint(out Vector3 landing))
        {
            stepEndPos = landing;
            stepEndValid = true;
        }
        // Le point de décollage suit le corps : avec un pas long et un corps rapide, un départ fixé au sol
        // restait loin derrière et la tentacule en l'air se tendait toute droite. Le balancement se fait
        // donc par rapport au corps, du point de décollage vers le point d'atterrissage.
        stepStartPos = body.TransformPoint(stepStartLocal);
        StepDirection = Vector3.ProjectOnPlane(stepEndPos - stepStartPos, Vector3.up).normalized;

        // Horizontal adouci (décolle, avance vite, ralentit à l'atterrissage) + arc vertical
        float h = Mathf.SmoothStep(0f, 1f, t);
        Vector3 pos = Vector3.Lerp(stepStartPos, stepEndPos, h);
        // Si la ligne droite passe sous le buste, le pied le contourne par le côté de la tentacule
        pos = PushOutOfBody(pos, settings.bodyClearance);
        pos.y += Mathf.Sin(t * Mathf.PI) * settings.stepHeight;
        footPos = pos;

        if (t >= 1f)
        {
            isStepping = false;
            footPos = stepEndPos;
            planted = stepEndValid;
            settleTimer = 0f;
            groupManager.EndStep(groupId);
        }
    }

    // Pendant qu'un groupe est en l'air, l'autre porte le corps et ses pieds reculent (par rapport au corps)
    // de vitesse × durée du pas. Si le pas dure plus longtemps que le temps nécessaire pour parcourir
    // une foulée, les pieds en attente sont traînés hors de portée. On raccourcit donc le pas avec la vitesse.
    float CurrentStepDuration()
    {
        float speed = anchorVelocity.magnitude;
        if (!settings.adaptStepToSpeed || speed < 0.01f) return settings.stepDuration;

        float stride = settings.stepThreshold + settings.stepOvershoot * Mathf.Clamp01(speed / settings.fullStrideSpeed);
        float maxAirTime = 0.9f * stride / speed;
        return Mathf.Clamp(maxAirTime, settings.minStepDuration, Mathf.Max(settings.stepDuration, settings.minStepDuration));
    }

    // Point de repos décalé vers l'avant dans le sens du déplacement de l'ancre.
    // Le pied atterrit devant, puis le corps passe au-dessus : il recule jusqu'à stepThreshold
    // derrière avant le pas suivant. C'est ce va-et-vient qui donne une vraie foulée.
    bool TryGetLandingPoint(out Vector3 point)
    {
        Vector3 lead = Vector3.zero;
        float speed = anchorVelocity.magnitude;
        if (speed > 0.01f)
            lead = anchorVelocity / speed * settings.stepOvershoot * Mathf.Clamp01(speed / settings.fullStrideSpeed);

        Vector3 candidate = restAnchor.position + lead;

        // Atterrir à portée : les ancres avant sont déjà loin de la racine, + l'avance, le pied se posait
        // à longueur max et la tentacule restait tendue. On ramène le point dans un rayon autour de la racine.
        if (chainLength > 0f)
        {
            Vector3 root = chainConstraint.data.root.position;
            float height = Mathf.Max(0f, root.y - restAnchor.position.y);
            float maxReach = chainLength * settings.landingReachRatio;
            float maxFlat = Mathf.Sqrt(Mathf.Max(0f, maxReach * maxReach - height * height));
            Vector3 flat = Vector3.ProjectOnPlane(candidate - root, Vector3.up);
            if (flat.magnitude > maxFlat)
            {
                candidate = root + flat.normalized * maxFlat;
                candidate.y = restAnchor.position.y;
            }
        }

        candidate = PushOutOfBody(candidate, settings.bodyClearance);
        return TryGetGround(candidate, out point);
    }
}
