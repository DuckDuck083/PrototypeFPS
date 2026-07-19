using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 8f;
    [SerializeField, Min(0f)] private float crouchSpeed = 2.5f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Looking")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Range(1f, 90f)] private float verticalLookLimit = 85f;

    [Header("Crouching")]
    [SerializeField, Min(0.1f)] private float standingHeight = 2f;
    [SerializeField, Min(0.1f)] private float crouchingHeight = 1.2f;
    [SerializeField, Min(0f)] private float crouchTransitionSpeed = 12f;
    [SerializeField] private LayerMask obstructionMask = ~0;

    private CharacterController characterController;
    private PlayerVitals playerVitals;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private float verticalVelocity;
    private float cameraPitch;
    private float standingCameraHeight;
    private bool isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerVitals = GetComponent<PlayerVitals>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        standingCameraHeight = cameraTransform != null ? cameraTransform.localPosition.y : 0.8f;

        moveAction = inputActions.FindAction("Player/Move", true);
        lookAction = inputActions.FindAction("Player/Look", true);
        jumpAction = inputActions.FindAction("Player/Jump", true);
        sprintAction = inputActions.FindAction("Player/Sprint", true);
        crouchAction = inputActions.FindAction("Player/Crouch", true);
    }

    private void OnEnable()
    {
        inputActions.FindActionMap("Player", true).Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        inputActions.FindActionMap("Player", false)?.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        HandleLook();
        HandleCrouch();
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>() * mouseSensitivity;
        transform.Rotate(Vector3.up, lookInput.x);

        cameraPitch = Mathf.Clamp(cameraPitch - lookInput.y, -verticalLookLimit, verticalLookLimit);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 horizontalMovement = transform.right * input.x + transform.forward * input.y;
        horizontalMovement = Vector3.ClampMagnitude(horizontalMovement, 1f);

        bool wantsToSprint = !isCrouching && input.sqrMagnitude > 0.01f && sprintAction.IsPressed();
        bool isSprinting = wantsToSprint && (playerVitals == null || playerVitals.UseSprintStamina());
        float speed = isCrouching ? crouchSpeed : isSprinting ? sprintSpeed : walkSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpAction.WasPressedThisFrame() && characterController.isGrounded && !isCrouching)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = horizontalMovement * speed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        if (crouchAction.IsPressed())
            isCrouching = true;
        else if (CanStandUp())
            isCrouching = false;

        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        characterController.height = Mathf.MoveTowards(
            characterController.height,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime);
        characterController.center = Vector3.up * (characterController.height * 0.5f);

        float targetCameraHeight = standingCameraHeight - (standingHeight - targetHeight);
        Vector3 cameraPosition = cameraTransform.localPosition;
        cameraPosition.y = Mathf.MoveTowards(
            cameraPosition.y,
            targetCameraHeight,
            crouchTransitionSpeed * Time.deltaTime);
        cameraTransform.localPosition = cameraPosition;
    }

    private bool CanStandUp()
    {
        float radius = characterController.radius * 0.9f;
        // Only test the space above the crouched controller so this query does
        // not detect the player's own CharacterController.
        Vector3 bottom = transform.position + Vector3.up * (crouchingHeight + radius);
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
        return !Physics.CheckCapsule(bottom, top, radius, obstructionMask, QueryTriggerInteraction.Ignore);
    }
}
