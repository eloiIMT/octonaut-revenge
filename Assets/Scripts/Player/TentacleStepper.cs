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

    void Start()
    {
        ApplyChainSettings();
        lastAnchorPos = restAnchor.position;

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

        ikTarget.position = footPos;
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

        // Seuil mesuré à plat : la hauteur (arc, relief) ne doit pas masquer l'écart horizontal
        bool overdue = horizontal > settings.stepThreshold || vertical > settings.stepThreshold;
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
        stepEndValid = TryGetLandingPoint(out stepEndPos);
        groupManager.BeginStep(groupId);
    }

    void UpdateStep(float dt)
    {
        stepTimer += dt / settings.stepDuration;
        float t = Mathf.Clamp01(stepTimer);

        // L'ancre avance avec le corps pendant le pas : on recalcule le point d'atterrissage
        // pour que le pied se pose bien devant le corps à la fin, et pas là où il était au décollage.
        if (TryGetLandingPoint(out Vector3 landing))
        {
            stepEndPos = landing;
            stepEndValid = true;
        }
        StepDirection = Vector3.ProjectOnPlane(stepEndPos - stepStartPos, Vector3.up).normalized;

        // Horizontal adouci (décolle, avance vite, ralentit à l'atterrissage) + arc vertical
        float h = Mathf.SmoothStep(0f, 1f, t);
        Vector3 pos = Vector3.Lerp(stepStartPos, stepEndPos, h);
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

    // Point de repos décalé vers l'avant dans le sens du déplacement de l'ancre.
    // Le pied atterrit devant, puis le corps passe au-dessus : il recule jusqu'à stepThreshold
    // derrière avant le pas suivant. C'est ce va-et-vient qui donne une vraie foulée.
    bool TryGetLandingPoint(out Vector3 point)
    {
        Vector3 lead = Vector3.zero;
        float speed = anchorVelocity.magnitude;
        if (speed > 0.01f)
            lead = anchorVelocity / speed * settings.stepOvershoot * Mathf.Clamp01(speed / settings.fullStrideSpeed);

        return TryGetGround(restAnchor.position + lead, out point);
    }
}
