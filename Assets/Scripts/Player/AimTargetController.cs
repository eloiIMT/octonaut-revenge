using UnityEngine;

public class AimTargetController : MonoBehaviour
{

    public Camera mainCamera;
    public float maxDistance = 50f;
    public LayerMask aimLayerMask = ~0;

    void Update()
    {
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, aimLayerMask))
        {
            transform.position = hit.point;
        }
        else
        {
            transform.position = ray.GetPoint(maxDistance);
        }        
    }
}
