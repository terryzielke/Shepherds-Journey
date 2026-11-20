using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------------------------
// DemoPlayerController
// ---------------------------------------------------------------------------------------------
// A simple third-person movement controller for testing HUD features in the demo scenes
// (for example SheepInventory). It is NOT the game's real player controller.
//
// How it works, in short:
//   1. Each frame we read the Move action (WASD / left stick) as a Vector2.
//   2. We turn that 2D input into a 3D direction relative to the camera, so "forward" always
//      means "away from the camera", whichever way the camera is facing.
//   3. We rotate the player to face that direction and move them with a CharacterController.
//   4. Gravity pulls the player down every frame; Jump gives them an upward velocity.
//
// Setup in the Editor:
//   - Add this script to the ROOT of the Player prefab. Unity adds a CharacterController
//     automatically because of the RequireComponent attribute below.
//   - Set the CharacterController to Height 2, Radius 0.5, Center (0, 1, 0) to match the capsule.
//   - Remove any other collider on the player's children (the CharacterController is the collider).
//   - Drag Player/Move, Player/Sprint and Player/Jump from Assets/InputSystem_Actions into
//     the matching fields in the Inspector.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Moves the demo player with the new Input System and a CharacterController. Movement is
/// relative to the camera, the player turns to face the way they walk, and they can sprint
/// and jump.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DemoPlayerController : MonoBehaviour
{
    [Header("Input (drag from InputSystem_Actions)")]
    [Tooltip("Player/Move: a Vector2 from WASD or the left stick.")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Player/Sprint: held down to move faster.")]
    [SerializeField] private InputActionReference sprintAction;

    [Tooltip("Player/Jump: pressed to jump.")]
    [SerializeField] private InputActionReference jumpAction;

    [Header("Movement")]
    [Tooltip("Walking speed in metres per second.")]
    [SerializeField] private float walkSpeed = 4f;

    [Tooltip("Speed in metres per second while Sprint is held.")]
    [SerializeField] private float sprintSpeed = 7f;

    [Tooltip("How fast the player turns to face their movement direction, in degrees per second.")]
    [SerializeField] private float turnSpeed = 720f;

    [Header("Jumping and Gravity")]
    [Tooltip("How high a jump goes, in metres.")]
    [SerializeField] private float jumpHeight = 1.2f;

    [Tooltip("Downward acceleration in metres per second squared. Must be negative. " +
             "Real gravity is -9.81, but games often use more so jumps feel less floaty.")]
    [SerializeField] private float gravity = -20f;

    [Header("Camera")]
    [Tooltip("The camera that movement is relative to. Leave empty to use the Main Camera.")]
    [SerializeField] private Transform cameraTransform;

    // Small constant downward speed used while grounded. It keeps the CharacterController
    // pressed against the ground so isGrounded stays reliable on slopes and small bumps.
    private const float GroundedStickSpeed = -2f;

    // Cached reference to our CharacterController, fetched once in Awake instead of every frame.
    private CharacterController characterController;

    // Current vertical speed (up is positive). We track it ourselves because a CharacterController
    // has no physics; it only moves when we call Move().
    private float verticalVelocity;

    /// <summary>
    /// Caches the CharacterController when the object is created.
    /// </summary>
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Finds the Main Camera if no camera was assigned in the Inspector.
    /// </summary>
    private void Start()
    {
        // Camera.main searches the scene, so we only call it once here and keep the result.
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// Turns the input actions on while this component is enabled. Input actions do nothing
    /// until they are enabled.
    /// </summary>
    private void OnEnable()
    {
        SetActionEnabled(moveAction, true);
        SetActionEnabled(sprintAction, true);
        SetActionEnabled(jumpAction, true);
    }

    /// <summary>
    /// Turns the input actions off when this component is disabled or destroyed, so a disabled
    /// player stops listening for input.
    /// </summary>
    private void OnDisable()
    {
        SetActionEnabled(moveAction, false);
        SetActionEnabled(sprintAction, false);
        SetActionEnabled(jumpAction, false);
    }

    /// <summary>
    /// Runs once per frame: reads input, works out horizontal and vertical movement, and
    /// moves the player.
    /// </summary>
    private void Update()
    {
        Vector3 horizontalVelocity = CalculateHorizontalVelocity();
        RotateTowards(horizontalVelocity);
        ApplyGravityAndJump();

        // Combine horizontal and vertical movement into a single Move call. Multiplying by
        // Time.deltaTime turns "metres per second" into "metres this frame", so the player
        // moves at the same speed whatever the frame rate is.
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Reads the Move and Sprint input and returns the horizontal velocity (metres per second)
    /// in world space, relative to the camera's facing direction.
    /// </summary>
    private Vector3 CalculateHorizontalVelocity()
    {
        Vector2 input = ReadMoveInput();

        // Work out which way is "forward" and "right" from the camera's point of view.
        // If there is no camera, fall back to world axes so the controller still works.
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            // Flatten the camera's directions onto the ground (y = 0). Without this, a camera
            // looking down at the player would make "forward" point into the ground.
            forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();
        }

        // input.y is forward/back (W/S) and input.x is left/right (A/D).
        Vector3 direction = forward * input.y + right * input.x;

        // Clamp the length to 1 so moving diagonally is not faster than moving straight.
        // ClampMagnitude (not Normalize) keeps gentle stick pushes slow, which feels right on a gamepad.
        direction = Vector3.ClampMagnitude(direction, 1f);

        bool isSprinting = sprintAction != null && sprintAction.action.IsPressed();
        float speed = isSprinting ? sprintSpeed : walkSpeed;

        return direction * speed;
    }

    /// <summary>
    /// Smoothly turns the player to face the direction they are moving. Does nothing when the
    /// player is standing still, so they keep facing the last way they walked.
    /// </summary>
    private void RotateTowards(Vector3 horizontalVelocity)
    {
        // sqrMagnitude avoids a square root and is cheaper than magnitude. We only need to know
        // whether the player is moving at all.
        if (horizontalVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity, Vector3.up);

        // RotateTowards turns at most turnSpeed * deltaTime degrees this frame, giving a steady
        // turn speed instead of snapping straight to the new direction.
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Updates the vertical velocity: keeps the player on the ground, starts a jump if Jump was
    /// pressed while grounded, and applies gravity.
    /// </summary>
    private void ApplyGravityAndJump()
    {
        bool isGrounded = characterController.isGrounded;

        // When grounded and not already moving up, reset the fall speed. Without this,
        // verticalVelocity would keep growing while standing still, and walking off a ledge
        // would drop the player instantly at a huge speed.
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = GroundedStickSpeed;
        }

        // WasPressedThisFrame is true only on the frame the button goes down, so holding
        // Jump does not make the player bounce over and over.
        if (isGrounded && jumpAction != null && jumpAction.action.WasPressedThisFrame())
        {
            // Physics formula for the starting speed needed to reach a given height:
            //   v = sqrt(2 * height * g)
            // Our gravity is negative, so we use -2 to make the value inside the root positive.
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity is an acceleration, so it changes the velocity by gravity * time each frame.
        verticalVelocity += gravity * Time.deltaTime;
    }

    /// <summary>
    /// Returns the Move input as a Vector2, or zero if no Move action is assigned.
    /// </summary>
    private Vector2 ReadMoveInput()
    {
        if (moveAction == null)
        {
            return Vector2.zero;
        }

        return moveAction.action.ReadValue<Vector2>();
    }

    /// <summary>
    /// Enables or disables the input action inside a reference, safely skipping any reference
    /// that has not been assigned in the Inspector.
    /// </summary>
    private static void SetActionEnabled(InputActionReference reference, bool enable)
    {
        if (reference == null || reference.action == null)
        {
            return;
        }

        if (enable)
        {
            reference.action.Enable();
        }
        else
        {
            reference.action.Disable();
        }
    }
}
