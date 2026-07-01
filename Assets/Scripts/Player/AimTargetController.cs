using UnityEngine;

public class AimTargetController : MonoBehaviour
{
    [Header("Raycast")]
    public Camera mainCamera;
    public float maxDistance = 50f;
    public LayerMask aimLayerMask = ~0;

    [Header("Épaules")]
    public Transform shoulderLeft;
    public Transform shoulderRight;

    [Header("Portée des bras")]
    public float maxArmReachLeft = 1.2f;
    public float maxArmReachRight = 1.2f;

    [Header("Cibles clampées (à assigner dans TwoBoneIK)")]
    public Transform clampedTargetLeft;
    public Transform clampedTargetRight;

    void Update()
    {
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;
        Vector3 aimPoint;

        if (Physics.Raycast(ray, out hit, maxDistance, aimLayerMask))
            aimPoint = hit.point;
        else
            aimPoint = ray.GetPoint(maxDistance);

        transform.position = aimPoint;

        ClampTarget(shoulderLeft, clampedTargetLeft, maxArmReachLeft, aimPoint);
        ClampTarget(shoulderRight, clampedTargetRight, maxArmReachRight, aimPoint);
    }

    void ClampTarget(Transform shoulder, Transform clampedTarget, float maxReach, Vector3 aimPoint)
    {
        if (shoulder == null || clampedTarget == null) return;

        Vector3 direction = aimPoint - shoulder.position;
        if (direction.magnitude > maxReach)
            direction = direction.normalized * maxReach;

        clampedTarget.position = shoulder.position + direction;
    }
}