using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------------------------
// DemoCameraController
// ---------------------------------------------------------------------------------------------
// A third-person orbit camera for testing HUD features in the demo scenes (for example
// SheepInventory). It is NOT the game's real camera.
//
// How it works, in short:
//   1. A "pivot" point floats at shoulder height above the player and smoothly follows them.
//   2. The camera sits a set distance away from that pivot and always looks at it.
//   3. Two angles decide where around the pivot the camera sits:
//        - yaw:   turning left and right around the player
//        - pitch: tilting up and down (clamped so it never flips over or goes underground)
//   4. Moving the mouse changes the angles ONLY while the right mouse button is held, so the
//      cursor stays free for clicking HUD and inventory UI. The gamepad right stick always works.
//   5. A sphere cast from the pivot to the camera stops the camera from going through the
//      terrain or walls by pulling it closer when something is in the way.
//
// Setup in the Editor:
//   - Add this script to the Main Camera in the scene.
//   - Drag the Player into Target (or leave it empty and it finds the DemoPlayerController).
//   - Drag Player/Look from Assets/InputSystem_Actions into Look Action.
//   - Set Collision Mask to the layers the camera should not pass through (for example
//     Default and Terrain). Leave the player's and sheep's layers out.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Third-person camera that follows a target, orbits around it with the Look input while the
/// right mouse button is held (or with the gamepad right stick), and moves in closer when
/// terrain or walls block the view.
/// </summary>
public class DemoCameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object to follow. Leave empty to find the DemoPlayerController in the scene.")]
    [SerializeField] private Transform target;

    [Tooltip("Height above the target's position that the camera looks at, in metres. " +
             "About shoulder height feels natural.")]
    [SerializeField] private float pivotHeight = 1.6f;

    [Header("Input")]
    [Tooltip("Player/Look: mouse movement or the gamepad right stick.")]
    [SerializeField] private InputActionReference lookAction;

    // The shared InputSystem_Actions asset has no right-mouse action, and we should not change
    // shared assets. So this action is defined here in code instead. Because it is a serialized
    // InputAction, it also shows in the Inspector, where the binding can be changed if needed.
    [Tooltip("Button that must be held for the mouse to orbit the camera. Defaults to right mouse.")]
    [SerializeField] private InputAction orbitButtonAction =
        new InputAction("Orbit", InputActionType.Button, "<Mouse>/rightButton");

    [Tooltip("Mouse orbit speed, in degrees per pixel of mouse movement.")]
    [SerializeField] private float mouseSensitivity = 0.2f;

    [Tooltip("Gamepad orbit speed, in degrees per second at full stick.")]
    [SerializeField] private float stickSensitivity = 180f;

    [Tooltip("Turn this on to flip up and down when orbiting.")]
    [SerializeField] private bool invertPitch = false;

    [Header("Orbit")]
    [Tooltip("How far the camera sits from the pivot, in metres, when nothing is in the way.")]
    [SerializeField] private float distance = 6f;

    [Tooltip("Lowest the camera can tilt, in degrees. Negative values look up at the player.")]
    [SerializeField] private float minPitch = -10f;

    [Tooltip("Highest the camera can tilt, in degrees. 90 would be straight down.")]
    [SerializeField] private float maxPitch = 70f;

    [Tooltip("Time in seconds the camera takes to catch up with the player. 0 is instant.")]
    [SerializeField] private float followSmoothTime = 0.1f;

    [Header("Collision")]
    [Tooltip("Layers the camera cannot pass through. Leave the player's and sheep's layers out.")]
    [SerializeField] private LayerMask collisionMask = ~0;

    [Tooltip("Radius of the sphere used to test for walls. A bit bigger than the camera's " +
             "near clip plane stops walls from being cut off on screen.")]
    [SerializeField] private float collisionRadius = 0.3f;

    [Tooltip("Closest the camera may get to the pivot when pushed in by a wall, in metres.")]
    [SerializeField] private float minDistance = 1f;

    [Tooltip("How fast the camera moves back out after a wall stops blocking it, in metres per second.")]
    [SerializeField] private float zoomOutSpeed = 8f;

    // Current orbit angles in degrees. We store the angles ourselves and rebuild the rotation from
    // them every frame. That is simpler and safer than adding small rotations to transform.rotation.
    private float yaw;
    private float pitch;

    // The smoothed pivot position, and the velocity SmoothDamp needs to remember between frames.
    private Vector3 smoothedPivot;
    private Vector3 pivotVelocity;

    // The distance actually used this frame. It may be shorter than "distance" when a wall is in the way.
    private float currentDistance;

    /// <summary>
    /// Finds the target if needed and sets the starting angles from the camera's current rotation,
    /// so the view does not jump when Play starts.
    /// </summary>
    private void Start()
    {
        if (target == null)
        {
            // Searching the scene is slow, so it only happens once, and only as a fallback.
            DemoPlayerController player = FindAnyObjectByType<DemoPlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("DemoCameraController: no Target assigned and no DemoPlayerController found.", this);
            }
        }

        // Start from the angles the camera already has in the scene.
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;

        // eulerAngles gives 0 to 360. A camera tilted slightly up reads as about 350, so convert
        // anything over 180 into a negative angle before clamping.
        pitch = startAngles.x > 180f ? startAngles.x - 360f : startAngles.x;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        currentDistance = distance;

        if (target != null)
        {
            smoothedPivot = GetPivotTarget();
        }
    }

    /// <summary>
    /// Turns the input actions on while this component is enabled.
    /// </summary>
    private void OnEnable()
    {
        orbitButtonAction.Enable();

        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Enable();
        }
    }

    /// <summary>
    /// Turns the input actions off when this component is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        orbitButtonAction.Disable();

        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Disable();
        }
    }

    /// <summary>
    /// Updates the camera after everything else has moved this frame. Using LateUpdate instead
    /// of Update means the player has already moved, so the camera never lags a frame behind
    /// and does not jitter.
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateOrbitAngles();

        // Smoothly move the pivot towards the player. SmoothDamp eases in and out, like a spring,
        // so quick player movements do not jerk the camera.
        smoothedPivot = Vector3.SmoothDamp(smoothedPivot, GetPivotTarget(), ref pivotVelocity, followSmoothTime);

        // Build the camera's rotation from the two angles. Quaternion.Euler(pitch, yaw, 0) means
        // "turn yaw degrees around the vertical axis, then tilt pitch degrees". The last value
        // (roll) stays 0 so the horizon is always level.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // rotation * Vector3.back is the direction from the pivot to the camera, behind and
        // (because of pitch) above the player.
        Vector3 directionToCamera = rotation * Vector3.back;

        UpdateDistance(directionToCamera);

        transform.position = smoothedPivot + directionToCamera * currentDistance;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Reads the Look input and changes yaw and pitch. Mouse input only counts while the orbit
    /// button is held; gamepad stick input always counts.
    /// </summary>
    private void UpdateOrbitAngles()
    {
        if (lookAction == null || lookAction.action == null)
        {
            return;
        }

        Vector2 look = lookAction.action.ReadValue<Vector2>();
        if (look == Vector2.zero)
        {
            return;
        }

        // The Look action has both mouse and gamepad bindings. activeControl tells us which
        // device produced this input, because the two need different handling.
        InputControl activeControl = lookAction.action.activeControl;
        bool isPointer = activeControl != null && activeControl.device is Pointer;

        Vector2 angleChange;
        if (isPointer)
        {
            // Without the orbit button held, the mouse is free for clicking UI, so ignore it.
            if (!orbitButtonAction.IsPressed())
            {
                return;
            }

            // Mouse delta is already "pixels moved this frame", so we do NOT multiply by
            // Time.deltaTime. Doing so would make the camera slower at high frame rates.
            angleChange = look * mouseSensitivity;
        }
        else
        {
            // A stick gives a value from -1 to 1 that stays the same while held, so it is a
            // speed. Multiplying by Time.deltaTime turns it into "degrees this frame".
            angleChange = look * stickSensitivity * Time.deltaTime;
        }

        yaw += angleChange.x;

        // Moving the mouse up gives positive y, but a positive pitch tilts the camera down.
        // Subtracting makes "mouse up" look up, which is what most players expect.
        float pitchDirection = invertPitch ? 1f : -1f;
        pitch += angleChange.y * pitchDirection;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Keep yaw between 0 and 360 so the number does not grow without limit over a long session.
        yaw = Mathf.Repeat(yaw, 360f);
    }

    /// <summary>
    /// Works out how far from the pivot the camera can be this frame, pulling it in when
    /// something is between the pivot and the camera. The direction given points from the
    /// pivot to the camera.
    /// </summary>
    private void UpdateDistance(Vector3 directionToCamera)
    {
        float allowedDistance = distance;

        // SphereCast pushes an invisible ball from the pivot towards the camera. If it hits
        // something, the camera has to stay in front of that point. A sphere works better than
        // a thin ray because the camera has width (its near clip plane), so a ray could slip
        // past an edge and still show the inside of a wall.
        // Triggers are ignored because they are invisible zones, not solid walls.
        if (Physics.SphereCast(smoothedPivot, collisionRadius, directionToCamera, out RaycastHit hit,
                distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            allowedDistance = Mathf.Max(hit.distance, minDistance);
        }

        if (allowedDistance < currentDistance)
        {
            // Snap in straight away, otherwise the camera would be inside the wall for a few frames.
            currentDistance = allowedDistance;
        }
        else
        {
            // Ease back out slowly once the way is clear, which looks much less jumpy.
            currentDistance = Mathf.MoveTowards(currentDistance, allowedDistance, zoomOutSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Returns the point the camera should look at: the target's position raised by the pivot height.
    /// </summary>
    private Vector3 GetPivotTarget()
    {
        return target.position + Vector3.up * pivotHeight;
    }
}
