using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float characterRotationSpeed = 10f;
    

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalVelocity;

    [Header("Camera")]
    public Transform cameraTarget;
    public float rotationSpeed = 5f;
    public float minPitch = -40f;
    public float maxPitch = 70f;

    private float yaw;
    private float pitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    void Update()
    {
        HandleRotationAndMovement();
        ApplyGravity();
    }

    void HandleRotationAndMovement()
    {
    
        yaw += lookInput.x * rotationSpeed * Time.deltaTime;
        pitch -= lookInput.y * rotationSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            characterRotationSpeed * Time.deltaTime
        );

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (inputDir.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = cameraTarget.forward;
            Vector3 camRight   = cameraTarget.right;
            camForward.y = 0f;
            camRight.y   = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;
            moveDir.Normalize();

            controller.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}