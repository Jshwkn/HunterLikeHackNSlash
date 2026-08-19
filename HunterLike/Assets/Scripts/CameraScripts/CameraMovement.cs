using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraRig : MonoBehaviour
{
    //im looking at you

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
        lookStickAction = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");// this just gets the mouse input so you can look around

        currentDistance = distance;// set the initial distance to the desired distance
        pivotPosition = target.position + pivotOffset;// set the initial pivot position to the target position aswell as the offset
        yaw = target.eulerAngles.y;// set the initial yaw to the target's y rotation
    }

    private void OnEnable()
    {
        lookMouseAction.Enable();// enables the mouse look action when the script is enabled and vice versa for the disabled state below
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

        ReadLookInput(dt);// read the input from the mouse and controller so you can schmoove the camera
        FollowTarget(dt);
        ApplyOrbitRotationAndCollision();// apply the rotation and collision to the camera
    }

    private void ReadLookInput(float dt)
    {
        Vector2 mouseDelta = lookMouseAction.ReadValue<Vector2>();// read the mouse delta input from the mouse
        Vector2 stickInput = lookStickAction.ReadValue<Vector2>();//same but for the controller

        // Mouse delta is already "movement this frame" — no dt needed.
        // Stick input is a held rate — must be scaled by dt to be framerate-independent.
        Vector2 lookDelta = mouseDelta * mouseSensitivity + stickInput * stickSensitivityDegPerSec * dt;

        if (lookDelta.sqrMagnitude > 0.0001f)// if the look delta is greater than 0.0001f then we can move the camera
        {
            yaw += lookDelta.x;// add the look delta to the yaw and pitch so you can move the camera around
            pitch -= lookDelta.y;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);// clamp the pitch so you can't look too far up or down
            lastLookInputTime = Time.time;// set the last look input time to the current time so we can use it for the auto recentering
        }
        else if (autoRecenter && Time.time - lastLookInputTime > recenterDelay)//if auto recenter is enabled and it has been long enough then we recenter the camera
        {
            float targetYaw = target.eulerAngles.y;// find the target yaw so we can recenter the camera to it
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, recenterSpeedDegPerSec * dt);
        }
    }

    private void FollowTarget(float dt)
    {
        Vector3 desiredPivot = target.position + pivotOffset;// find the desired pivot position based on the target's position and the offset
        pivotPosition = Vector3.SmoothDamp(pivotPosition, desiredPivot, ref pivotVelocity, pivotFollowTime);//slowly moves the pivot position to the desired pivot position
    }

    private void ApplyOrbitRotationAndCollision()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);// create a rotation based on the pitch and yaw so we can rotate the camera around the target
        Vector3 desiredCameraPos = pivotPosition - rotation * Vector3.forward * distance;// find the desired camera position based on the pivot position, rotation, and distance

        float targetDistance = distance;
        if (Physics.SphereCast(pivotPosition, collisionRadius, (desiredCameraPos - pivotPosition).normalized,
                out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))// if the sphere cast hits something then we set the target distance to the hit distance
        {
            targetDistance = Mathf.Max(minDistance, hit.distance);// set the target distance to the hit distance, clamped to the minimum distance
        }

        // Snap IN instantly to avoid ever clipping through geometry;
        // ease back OUT smoothly once the obstruction is gone.
        if (targetDistance < currentDistance)
            currentDistance = targetDistance;
        else
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, distancePushOutTime);// smoothly moves the current distance to the target distance

        transform.position = pivotPosition - rotation * Vector3.forward * currentDistance;
        transform.rotation = rotation;
    }
}
