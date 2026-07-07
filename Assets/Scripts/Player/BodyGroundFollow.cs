// Assets/Scripts/BodyGroundFollow.cs
using UnityEngine;

public class BodyGroundFollow : MonoBehaviour
{
    public Transform bodyVisual;   // le buste, séparé du CharacterController
    public TentacleStepper[] allTentacles;
    public float bodyHeightOffset = 0.6f;
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        float avgY = 0f;
        foreach (var t in allTentacles)
            avgY += t.transform.position.y; // ou stocke la ground pos si besoin

        avgY /= allTentacles.Length;

        Vector3 targetPos = bodyVisual.position;
        targetPos.y = Mathf.Lerp(bodyVisual.position.y, avgY + bodyHeightOffset, Time.deltaTime * smoothSpeed);
        bodyVisual.position = targetPos;
    }
}