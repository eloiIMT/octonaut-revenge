// Assets/Scripts/BodyGroundFollow.cs
using UnityEngine;

// Après les TentacleStepper (ordre 100) et avant la passe d'animation :
// l'IK est résolu avec la hauteur de buste à jour (en LateUpdate, il aurait un frame de retard).
[DefaultExecutionOrder(110)]
public class BodyGroundFollow : MonoBehaviour
{
    public Transform bodyVisual;   // le buste, séparé du CharacterController
    public CharacterController controller;
    public TentacleStepper[] allTentacles;
    [Tooltip("Décalage vertical additionnel du buste (m)")]
    public float bodyHeightOffset = 0f;
    public float smoothSpeed = 5f;

    private float baseLocalY;

    void Start()
    {
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        baseLocalY = bodyVisual.localPosition.y;
    }

    void Update()
    {
        // Hauteur moyenne des pieds POSÉS. Avant, on lisait t.transform.position :
        // l'objet IK_TentaculeX, enfant du corps, qui suit donc le corps et ne dit rien du sol.
        float sum = 0f;
        int count = 0;
        foreach (var t in allTentacles)
        {
            if (t == null || !t.IsPlanted) continue;
            sum += t.FootPosition.y;
            count++;
        }

        // Écart entre le sol sous les pieds et le sol sous le CharacterController (0 sur terrain plat)
        float targetY = baseLocalY + bodyHeightOffset;
        if (count > 0 && controller != null)
            targetY += sum / count - (controller.bounds.min.y - controller.skinWidth);

        Vector3 local = bodyVisual.localPosition;
        local.y = Mathf.Lerp(local.y, targetY, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
        bodyVisual.localPosition = local;
    }
}
