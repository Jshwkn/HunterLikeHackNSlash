using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] private float topSpeed = 8f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float deceleration = 50f;
    [SerializeField] private float airControlMultiplier = 0.5f;
    [SerializeField] private float turnSpeedDegPerSec = 720f;
    [SerializeField] private bool movementLocked;

    [Header("Gravity & Jump")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float terminalFallSpeed = -40f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private float groundCheckDistance = 0.35f;
    [SerializeField] private LayerMask groundMask;

    private CharacterController controller;
    private Camera mainCamera;

    // --- Input System -----------------------------------------------------

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    private bool isGrounded;
    private float lastGroundedTime;
    private float lastJumpPressedTime;

    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");
        dashAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateGroundedState();

        Vector2 rawInput = moveAction.ReadValue<Vector2>();
        Vector3 inputDirection = CameraRelativeDirection(rawInput);

        if (movementLocked)
        {
            // horizontal velocity stays at zero (set by SetMovementLocked);
            // gravity/jump still process below so the player doesn't float mid-attack
        }
        else
        {
            HandleDashInput(inputDirection, dt);

            if (isDashing)
                TickDash(dt);
            else
                ApplyGroundedOrAirMovement(inputDirection, dt);
        }

        ApplyGravityAndJump(dt);

        if (!movementLocked)
            FaceMoveDirection(inputDirection, dt);

        Vector3 fullVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(fullVelocity * dt);
    }

    // --- Grounding ---------------------------------------------------

    private void UpdateGroundedState()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckRadius;
        isGrounded = Physics.SphereCast(origin, groundCheckRadius, Vector3.down,
            out _, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }
    }

    // --- Ground/Air movement ------------------------------------------

    private void ApplyGroundedOrAirMovement(Vector3 inputDirection, float dt)
    {
        Vector3 targetVelocity = inputDirection * topSpeed;
        float rate = inputDirection.sqrMagnitude > 0.01f ? acceleration : deceleration;
        if (!isGrounded) rate *= airControlMultiplier;

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * dt);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked)
            horizontalVelocity = Vector3.zero; // plant feet immediately when an attack starts
    }

    // --- Gravity / Jump ------------------------------------------------

    private void ApplyGravityAndJump(float dt)
    {
        if (jumpAction.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;

        bool withinCoyoteWindow = Time.time - lastGroundedTime <= coyoteTime;
        bool withinJumpBuffer = Time.time - lastJumpPressedTime <= jumpBufferTime;

        if (withinJumpBuffer && withinCoyoteWindow)
        {
            verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
        }
        else
        {
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, terminalFallSpeed);
        }
    }

    // --- Dash ------------------------------------------------------------

    private void HandleDashInput(Vector3 inputDirection, float dt)
    {
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - dt);

        if (!isDashing && dashCooldownTimer <= 0f && dashAction.WasPressedThisFrame())
        {
            Vector3 dashDir = inputDirection.sqrMagnitude > 0.01f ? inputDirection : transform.forward;
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            horizontalVelocity = dashDir * dashSpeed;
        }
    }

    private void TickDash(float dt)
    {
        dashTimer -= dt;
        if (dashTimer <= 0f)
            isDashing = false;
    }

    // --- Orientation -----------------------------------------------------

    private void FaceMoveDirection(Vector3 inputDirection, float dt)
    {
        Vector3 facingSource = isDashing || inputDirection.sqrMagnitude > 0.01f
            ? horizontalVelocity
            : Vector3.zero;

        if (facingSource.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(facingSource.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeedDegPerSec * dt);
    }

    // --- Input helpers -----------------------------------------------------

    private Vector3 CameraRelativeDirection(Vector2 rawInput)
    {
        if (rawInput.sqrMagnitude < 0.001f) return Vector3.zero;

        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camForward * rawInput.y + camRight * rawInput.x).normalized;
    }
}