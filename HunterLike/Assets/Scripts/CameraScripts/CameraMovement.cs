using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraRig : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Orbit")]
    [SerializeField] private float distance = 4.5f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float stickSensitivityDegPerSec = 180f;

    [Header("Collision (spring arm)")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float minDistance = 0.8f;
    [SerializeField] private LayerMask collisionMask; // exclude the Player's own layer!

    [Header("Smoothing")]
    [SerializeField] private float pivotFollowTime = 0.06f;
    [SerializeField] private float distancePushOutTime = 0.08f;

    [Header("Auto Recenter")]
    [SerializeField] private bool autoRecenter = true;
    [SerializeField] private float recenterDelay = 2.5f;
    [SerializeField] private float recenterSpeedDegPerSec = 90f;

    private InputAction lookMouseAction;
    private InputAction lookStickAction;

    private float yaw;
    private float pitch = 10f;
    private Vector3 pivotPosition;
    private Vector3 pivotVelocity;
    private float currentDistance;
    private float distanceVelocity;
    private float lastLookInputTime;

    private void Awake()
    {
        lookMouseAction = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
        lookStickAction = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

        currentDistance = distance;
        pivotPosition = target.position + pivotOffset;
        yaw = target.eulerAngles.y;
    }

    private void OnEnable()
    {
        lookMouseAction.Enable();
        lookStickAction.Enable();
    }

    private void OnDisable()
    {
        lookMouseAction.Disable();
        lookStickAction.Disable();
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;

        ReadLookInput(dt);
        FollowTarget(dt);
        ApplyOrbitRotationAndCollision();
    }

    private void ReadLookInput(float dt)
    {
        Vector2 mouseDelta = lookMouseAction.ReadValue<Vector2>();
        Vector2 stickInput = lookStickAction.ReadValue<Vector2>();

        // Mouse delta is already "movement this frame" — no dt needed.
        // Stick input is a held rate — must be scaled by dt to be framerate-independent.
        Vector2 lookDelta = mouseDelta * mouseSensitivity + stickInput * stickSensitivityDegPerSec * dt;

        if (lookDelta.sqrMagnitude > 0.0001f)
        {
            yaw += lookDelta.x;
            pitch -= lookDelta.y;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            lastLookInputTime = Time.time;
        }
        else if (autoRecenter && Time.time - lastLookInputTime > recenterDelay)
        {
            float targetYaw = target.eulerAngles.y;
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, recenterSpeedDegPerSec * dt);
        }
    }

    private void FollowTarget(float dt)
    {
        Vector3 desiredPivot = target.position + pivotOffset;
        pivotPosition = Vector3.SmoothDamp(pivotPosition, desiredPivot, ref pivotVelocity, pivotFollowTime);
    }

    private void ApplyOrbitRotationAndCollision()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredCameraPos = pivotPosition - rotation * Vector3.forward * distance;

        float targetDistance = distance;
        if (Physics.SphereCast(pivotPosition, collisionRadius, (desiredCameraPos - pivotPosition).normalized,
                out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Max(minDistance, hit.distance);
        }

        // Snap IN instantly to avoid ever clipping through geometry;
        // ease back OUT smoothly once the obstruction is gone.
        if (targetDistance < currentDistance)
            currentDistance = targetDistance;
        else
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, distancePushOutTime);

        transform.position = pivotPosition - rotation * Vector3.forward * currentDistance;
        transform.rotation = rotation;
    }
}
