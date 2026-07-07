// Assets/Scripts/TentacleStepper.cs
using UnityEngine;
using UnityEngine.Animations.Rigging;

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

    private Vector3 stepStartPos;
    private Vector3 stepEndPos;
    private float stepTimer;
    private bool isStepping;
    private Vector3 lastGroundPos;

    void Start()
    {
        ApplyChainSettings();
        lastGroundPos = GetGroundPoint(restAnchor.position);
        ikTarget.position = lastGroundPos;
    }

    void ApplyChainSettings()
    {
        if (chainConstraint == null) return;
        chainConstraint.data.chainRotationWeight = settings.chainRotationWeight;
        chainConstraint.data.tipRotationWeight = settings.tipRotationWeight;
        chainConstraint.data.maxIterations = settings.maxIterations;
        chainConstraint.data.tolerance = settings.tolerance;
    }

    Vector3 GetGroundPoint(Vector3 anchorPos)
    {
        Vector3 rayOrigin = anchorPos + Vector3.up * settings.raycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, settings.raycastDistance, groundLayer))
            return hit.point;
        return lastGroundPos;
    }

    void Update()
    {
        Vector3 desiredGroundPos = GetGroundPoint(restAnchor.position);

        if (!isStepping)
        {
            float dist = Vector3.Distance(ikTarget.position, desiredGroundPos);
            if (dist > settings.stepThreshold && groupManager.CanStep(groupId))
                StartStep(desiredGroundPos);
        }
        else
        {
            stepTimer += Time.deltaTime / settings.stepDuration;
            float t = Mathf.Clamp01(stepTimer);

            Vector3 flatPos = Vector3.Lerp(stepStartPos, stepEndPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * settings.stepHeight;
            ikTarget.position = flatPos + Vector3.up * arc;

            if (t >= 1f)
            {
                isStepping = false;
                lastGroundPos = stepEndPos;
                groupManager.EndStep(groupId);
            }
        }
    }

    void StartStep(Vector3 targetPos)
    {
        isStepping = true;
        stepTimer = 0f;
        stepStartPos = ikTarget.position;

        Vector3 bodyVelocity = body.GetComponent<CharacterController>().velocity;
        Vector3 anticipated = targetPos + bodyVelocity.normalized * settings.forwardStepBias * bodyVelocity.magnitude;
        stepEndPos = GetGroundPoint(anticipated);

        groupManager.BeginStep(groupId);
    }
}