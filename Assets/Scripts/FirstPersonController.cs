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
    public float PerkSpeedMultiplier { get; set; } = 1f;
    public float MouseSensitivity { get => mouseSensitivity; set => mouseSensitivity = Mathf.Clamp(value, 0.03f, 0.4f); }

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

    public void RestoreControls()
    {
        if (characterController != null) characterController.enabled = true;
        inputActions.FindActionMap("Player", true).Enable();
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
        crouchAction?.Enable();
        verticalVelocity = -2f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // This component is disabled while the menu is open. If it is running,
        // gameplay must not remain frozen by a stale menu time scale.
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;
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
        if (Keyboard.current != null)
        {
            Vector2 keyboardInput = new Vector2(
                (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f),
                (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f));
            if (keyboardInput.sqrMagnitude > input.sqrMagnitude)
                input = keyboardInput;
        }
        Vector3 horizontalMovement = transform.right * input.x + transform.forward * input.y;
        horizontalMovement = Vector3.ClampMagnitude(horizontalMovement, 1f);

        bool sprintPressed = sprintAction.IsPressed() || (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
        bool wantsToSprint = !isCrouching && input.sqrMagnitude > 0.01f && sprintPressed;
        bool isSprinting = wantsToSprint && (playerVitals == null || playerVitals.UseSprintStamina());
        float weaponMovementMultiplier = GetComponent<SimpleRifle>() != null ? GetComponent<SimpleRifle>().MovementMultiplier : 1f;
        float speed = (isCrouching ? crouchSpeed : isSprinting ? sprintSpeed : walkSpeed) * weaponMovementMultiplier * PerkSpeedMultiplier;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        bool jumpPressed = jumpAction.WasPressedThisFrame() || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        if (jumpPressed && characterController.isGrounded && !isCrouching)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = horizontalMovement * speed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        bool crouchPressed = crouchAction.IsPressed() || (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed));
        if (crouchPressed)
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
