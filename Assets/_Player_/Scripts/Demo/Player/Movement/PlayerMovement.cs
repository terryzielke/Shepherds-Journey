using System;
using UnityEngine;


[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]


// Any one can use this script (hence why its PUBLIC) but no other class
// can inherit from it (hence the SEALED).
// Basically, it locks it up tighter than a nun's *echem* so that others
// can call and use it as-is, without altering (breaking) its behavior.
public sealed class PlayerMovement : MonoBehaviour
{
    // Player states that can be accessed by anyone, basically 0 = Grounded,
    // 1 = Airborne... but they have pretty names that we can recognise.
    public enum LocomotionState
    {
        Grounded,
        Airborne
    }


    [Header("Walking + Turning")]
    [SerializeField, Min(0f)] private float walkSpeed = 4f;
    [SerializeField, Min(0f)] private float groundAcceleration = 28f;
    [SerializeField, Min(0f)] private float groundBraking = 32f;
    [SerializeField, Min(0f)] private float airAcceleration = 8f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;


    [Header("Jumping + Falling")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 4f;
    [SerializeField, Min(0.1f)] private float gravity = 24f;
    [SerializeField, Min(1f)] private float maxFallSpeed = 35f;


    [Header("Refrences")]
    [SerializeField] private Transform movementCamera = null;


    private const float GroundContactSpeed = -2f;
    private const float DirectionThreshold = 0.0001f;


    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;


    // Other scripts can read these values, but they can't change em.
    public LocomotionState State { get; private set; } = LocomotionState.Airborne;
    public float PlanarSpeed { get; private set; }
    public event Action<LocomotionState> StateChanged;



    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }



    /// <summary>
    /// Handles player movement, acceleration, rotation, jumping, and collisions.
    /// Updates movement speed and locomotion state based on the results.
    /// </summary>
    /// <param name="moveInput">The player's movement direction.</param>
    /// <param name="jumpPressed">Whether the player has pressed jump.</param>
    /// <param name="deltaTime">Time since the last update.</param>
    public void Simulate(Vector2 moveInput, bool jumpPressed, float deltaTime)
    {
        // If the gameobject is broke, or the controller is broke, or
        // if time ain't moving, nothing will happen.
        if (!isActiveAndEnabled || !controller.enabled || deltaTime <= 0f)
            return;

        // Gets the state before we do all the physics stuff.
        bool wasGrounded = State == LocomotionState.Grounded;

        // Clamp diagonal input while keeping some input for slow walkers.
        Vector3 direction = GetWorldDirection(Vector2.ClampMagnitude(moveInput, 1f));
        bool hasMoveInput = direction.sqrMagnitude > DirectionThreshold;

        // Sets responsivness based on what the player is doing.
        float acceleration = airAcceleration;
        if (wasGrounded) acceleration = hasMoveInput ? groundAcceleration : groundBraking;

        // This just adds smoothness to transitions between speeds
        planarVelocity = Vector3.MoveTowards(planarVelocity, direction * walkSpeed, acceleration * deltaTime);

        // Turns toward requested direction, and stay the same direction
        // if the player stops (Idle) 
        if (hasMoveInput)
        {
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * deltaTime);
        }

        // Sends one combined movement to Mr.Unity, it flags it and tells us what.
        float verticalDistance = GetVerticalDistance(wasGrounded, jumpPressed, deltaTime);
        Vector3 displacement = planarVelocity * deltaTime + Vector3.up * verticalDistance;
        Vector3 startPosition = transform.position;
        CollisionFlags collisions = controller.Move(displacement);

        // Something above will stop, and below will signal a landing.
        if (verticalDistance > 0f && (collisions & CollisionFlags.Above) != 0) verticalVelocity = 0f;
        bool grounded = verticalDistance <= 0f && (collisions & CollisionFlags.Below) != 0;
        if (grounded) verticalVelocity = GroundContactSpeed;

        // Report actual speed after collisions so animation can stop.
        // i.e our player won't keep running at a wall like a dumb dumb.
        Vector3 travelled = transform.position - startPosition;
        travelled.y = 0f;
        PlanarSpeed = travelled.magnitude / deltaTime;

        // Updates the locomotion state based on whether the player is grounded,
        // and triggers an event if the state changes.
        LocomotionState nextState = grounded ? LocomotionState.Grounded : LocomotionState.Airborne;
        if (State != nextState)
        {
            State = nextState;
            StateChanged?.Invoke(State);
        }
    }



    /// <summary>
    /// Calculates vertical movement for jumping, falling, and ground contact.
    /// Applies gravity and returns the vertical distance travelled.
    /// </summary>
    /// <param name="grounded">Whether the player is on the ground.</param>
    /// <param name="jumpPressed">Whether the player is attempting to jump.</param>
    /// <param name="deltaTime">Time elapsed since the last update.</param>
    /// <returns>The vertical distance to move this frame.</returns>
    private float GetVerticalDistance(bool grounded, bool jumpPressed, float deltaTime)
    {
        // Keeps the player grounded if not jumping, so we don't accidentally
        // flag airborne or have jumping animations where they shouldn't be. 
        if (grounded && !jumpPressed)
        {
            verticalVelocity = GroundContactSpeed;
            return verticalVelocity * deltaTime;
        }

        // Work out how fast to launch upward to reach the chosen jump height.
        if (grounded)
        {
            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        float previousVelocity = verticalVelocity;

        // Apply gravity, then cap how fast the player can fall.
        verticalVelocity = Mathf.Max(verticalVelocity - gravity * deltaTime, - maxFallSpeed);

        // Use the average vertical speed to find the distance.
        return (previousVelocity + verticalVelocity) * 0.5f * deltaTime;
    }



    /// <summary>
    /// Converts player input into a movement direction relative to the camera.
    /// Keeps movement horizontal regardless of the camera's angle.
    /// </summary>
    /// <param name="input">The player's horizontal and vertical movement input.</param>
    /// <returns>The calculated world-space movement direction.</returns>
    private Vector3 GetWorldDirection(Vector2 input)
    {
        // Flattens camera so looking down doesn't push the player
        Vector3 forward = Vector3.forward;
        if (movementCamera != null)
        {
            forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up);
            if (forward.sqrMagnitude < DirectionThreshold)
                forward = Vector3.ProjectOnPlane(movementCamera.up, Vector3.up);
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return forward * input.y + right * input.x;
    }
} 