using System.Collections;
using System.Reflection;
using Unity.Hierarchy;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
// ---------------------------------------------------------------------------------------------
// CarriableSheep
// ---------------------------------------------------------------------------------------------
// Lets a sheep be picked up by the player's SheepCarrier. When the carrier calls BeginCarry,
// the sheep:
//   1. RUNS around the player (never through them) to a landing spot in front of the player.
//   2. JUMPS in an arc onto the carrier's SheepHolder.
//   3. ATTACHES to the holder (becomes its child), so it moves with the player.
// Later, the carrier can call Drop to put the sheep back into the world, or CancelCarry if a
// newer sheep takes over while this one is still on its way.
//
// Setup:
//   - Add this to the ROOT of the Sheep prefab (the object with the Rigidbody).
//   - Drag DemoSheepAI into Behaviours To Disable.
// ---------------------------------------------------------------------------------------------
/// <summary>
/// Lets a sheep be carried by a SheepCarrier. The sheep runs around to the front of the player,
/// jumps onto the carrier's holder and rides there until it is dropped. Other sheep behaviours
/// are switched off while it is carried and switched back on when it is dropped.
/// </summary>

[RequireComponent(typeof(Rigidbody))]
public class  SheepCarriable : MonoBehaviour
{
    [Header("Other Behaviours")]
    [Tooltip("Scripts on this sheep to turn off while it is carried, such as DemoSheepAI. They are turned back on when it is dropped.")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;

    [Header("Running")]
    [Tooltip("Running speed while heading to the player, in metres per second.")]
    [SerializeField] private float runSpeed = 5f;

    [Tooltip("How quickly the sheep speeds up and changes direction, in metres per second, per second.")]
    [SerializeField] private float acceleration = 25f;

    [Tooltip("How quickly the sheep turns to face where it is running, in degrees per second.")]
    [SerializeField] private float turnSpeed = 720f;

    [Tooltip("How far to the side of the player the sheep runs when going around them, in metres. Keep it wider than the player and sheep together, or the sheep will clip the player.")]
    [SerializeField] private float sidePointDistance = 2f;

    [Tooltip("How far in front of the player the sheep stops to jump, in metres.")]
    [SerializeField] private float landingSpotDistance = 2f;

    [Tooltip("How close the sheep must get to a point on its path to count as reaching it, in metres.")]
    [SerializeField] private float pointReachedDistance = 0.5f;

    [Tooltip("Longest the sheep keeps running, in seconds. If it gets stuck on something, it jumps from wherever it is when this time runs out.")]
    [SerializeField] private float maxRunTime = 6f;

    [Header("Jumping")]
    [Tooltip("How long the jump onto the holder takes, in seconds.")]
    [SerializeField] private float jumpDuration = 0.5f;

    [Tooltip("How high the jump arc rises above a straight line to the holder, in metres.")]
    [SerializeField] private float jumpHeight = 1.5f;

    // Directions shorter than this count as "no direction", so the sheep does not spin on the spot.
    private const float MinDirectionSqrMagnitude = 0.0001f;

    // Reused WaitForFixedUpdate to avoid allocating memory every frame while running to the player.
    private static readonly WaitForFixedUpdate WaitForPhysicsStep = new WaitForFixedUpdate();

    // Cached in Awake so we do not search for them every step.
    private Rigidbody body;
    private Collider[] solidColliders;
    private SheepCarrier carrier;

    // A handle to the running carry coroutine, so it can be stopped early (see ReturnToWorld).
    private Coroutine carryRoutine;

    private bool physicsSuspended;
    private RigidbodyInterpolation savedInterpolation;
    public bool IsAttached { get; private set; }

    /// <summary>
    /// Caches the Rigidbody and the solid colliders, and stops the sheep from tipping over
    /// while it runs.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Only SOLID colliders are switched off while riding, because those are the ones that
        // would block the player's CharacterController. Triggers never block anything.
        // The collider is on the Sphere child, so we search the children too.
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        solidColliders = System.Array.FindAll(allColliders, sheepCollider => !sheepCollider.isTrigger);
    }

    /// <summary>
    /// Starts the carry: the sheep turns off its other behaviours, runs around to the front of
    /// the given carrier's player, jumps onto the holder and attaches. Does nothing if the
    /// carrier is missing, has no holder, or this sheep is already being carried.
    /// </summary>
    public void BeginCarry(SheepCarrier newCarrier)
    {
        if(newCarrier == null || newCarrier.SheepHolder == null)
        {
            Debug.LogWarning("[CarriableSheep] BeginCarry needs a SheepCarrier with a Sheep Holder assigned.", this);
            return;
        }

        if(carrier != null)
        {
            Debug.LogWarning("[CarriableSheep] BeginCarry called while already being carried. Call CancelCarry first.", this);
            return;
        }

        carrier = newCarrier;
        SetBehavioursEnabled(false);
        carryRoutine = StartCoroutine(CarryRoutine());
    }

    /// <summary>
    /// Drops the sheep at the given world position and launches it with the given velocity.
    /// Physics and the sheep's other behaviours are switched back on. Called by SheepCarrier.
    /// </summary>
    public void Drop(Vector3 dropPosition, Vector3 launchVelocity)
    {
        if(carrier == null)
        {
            Debug.LogWarning("[CarriableSheep] Drop called while not being carried. Call BeginCarry first.", this);
            return;
        }
        ReturnToWorld(dropPosition, launchVelocity);
    }

    /// <summary>
    /// Stops the carry wherever the sheep is right now (running or mid-jump) and hands it back
    /// to its normal behaviours. Called by SheepCarrier when a newer sheep takes over.
    /// </summary>
    public void CancelCarry()
    {
        if (carrier == null) return;

        Vector3 keepVelocity = physicsSuspended ? Vector3.zero : body.linearVelocity;
        ReturnToWorld(transform.position, keepVelocity);
    }

    // The whole carry, written as one sequence. Each "yield return" pauses here and lets the
    // rest of the game carry on; Unity comes back to the same line later.
    private IEnumerator CarryRoutine()
    {
        // 1. Run around to the front of the player
        Transform player = carrier.transform;
        Vector3 toSheep = Flatten(body.position - player.position);

        // If we have a negative dot product, the sheep is behind the player and must use a side point.
        bool goAroundSide = Vector3.Dot(toSheep, player.forward) < 0f;

        // Pick a side once and stick with it, so the sheep does not zig-zag around the player.
        float side = Vector3.Dot(toSheep, player.right) < 0f ? -1f : 1f;

        float runTimer = 0f;

        while(runTimer < maxRunTime)
        {
            Vector3 target = goAroundSide ? GetSidePoint(player, side) : GetLandingSpot(player);
            Vector3 toTarget = Flatten(target - body.position);

            if(toTarget.sqrMagnitude < pointReachedDistance * pointReachedDistance)
            {
                if (goAroundSide)
                {
                    goAroundSide = false;
                    continue;
                }

                // The sheep has reached the landing spot in front of the player, so it can jump now.
                break;
            }

            ApplyRunVelocity(toTarget.normalized * runSpeed);
            runTimer += Time.fixedDeltaTime;

            // Wait for the next physics step before continuing the loop.
            yield return WaitForPhysicsStep;
        }

        // 2. Jump onto the holder
        carrier.HandleSheepJumping(this);

        SuspendPhysics();

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while(elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            // time goes from 0 to 1
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // Get the holders position every frame.
            Transform holder = carrier.SheepHolder;

            // Move in a straight line from the start to the holder.
            Vector3 straightLine = Vector3.Lerp(startPosition, holder.position, t);

            // Add a jump arc on top of the straight line.
            float arcHeight = 4f * jumpHeight * t * (1f - t);
            transform.SetPositionAndRotation(straightLine + Vector3.up * arcHeight, Quaternion.Slerp(startRotation, holder.rotation, t));

            // Wait for the next frame before continuing the loop.
            yield return null;
        }

        // 3. Attach to the holder
        transform.SetParent(carrier.SheepHolder);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        IsAttached = true;
        carryRoutine = null;
        carrier.HandleSheepAttached(this);
    }

    // Turns physics off for the jump and the ride. A kinematic Rigidbody ignores gravity and
    // forces and only moves when our code moves it. Colliders are turned off so the riding
    // sheep does not block the player's CharacterController.
    private void SuspendPhysics()
    {
        // Clear velocity before going kinematic.
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        // Save the interpolation mode so it can be restored later.
        savedInterpolation = body.interpolation;
        body.interpolation = RigidbodyInterpolation.None;

        body.isKinematic = true;
        SetCollidersEnabled(false);
        physicsSuspended = true;
    }

    // Undoes everything the carry did and puts the sheep back into the world at the given
    // position with the given velocity. Used by both Drop and CancelCarry, so the clean-up
    // can never be different between them.
    private void ReturnToWorld(Vector3 position, Vector3 velocity)
    {
        // Stops the coroutine.
        if(carryRoutine != null)
        {
            StopCoroutine(carryRoutine);
            carryRoutine = null;
        }

        // Detach from the holder and restore physics. True means world space, so the sheep does not move with the holder anymore.
        transform.SetParent(null, true);

        // Restore the Rigidbody to its original state.
        transform.position = position;

        if(physicsSuspended)
        {
            body.isKinematic = false;
            body.interpolation = savedInterpolation;
            SetCollidersEnabled(true);
            physicsSuspended = false;
        }

        // Give the sheep the requested velocity, which may be zero if CancelCarry was called.
        body.linearVelocity = velocity;
        body.angularVelocity = Vector3.zero;

        carrier = null;
        IsAttached = false;
        SetBehavioursEnabled(true);
    }

    // Steers the Rigidbody's flat velocity towards the desired velocity, and turns to face it.
    // The vertical part is left alone so gravity keeps working, and MoveTowards eases the change instead of snapping.
    private void ApplyRunVelocity(Vector3 desiredVelocity)
    {
        float step = Time.fixedDeltaTime;

        Vector3 currentVelocity = body.linearVelocity;
        Vector3 flatVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        flatVelocity = Vector3.MoveTowards(flatVelocity, desiredVelocity, acceleration * step);
        body.linearVelocity = new Vector3(flatVelocity.x, currentVelocity.y, flatVelocity.z);

        if(desiredVelocity.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * step);
        }
    }

    // The point beside the player that the sheep runs to when going around them.
    private Vector3 GetSidePoint(Transform player, float side)
    {
        return player.position + Flatten(player.right).normalized * (side * sidePointDistance);
    }
    // The point in front of the player where the sheep stops and jumps.
    private Vector3 GetLandingSpot(Transform player)
    {
        return player.position + Flatten(player.forward).normalized * landingSpotDistance;
    }

    // Turns every script in Behaviours To Disable on or off.
    private void SetBehavioursEnabled(bool enable)
    {
        if (behavioursToDisable == null)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in behavioursToDisable)
        {
            // Skip empty slots, and never switch off this script by mistake.
            if (behaviour != null && behaviour != this)
            {
                behaviour.enabled = enable;
            }
        }
    }

    // Turns the sheep's solid colliders on or off.
    private void SetCollidersEnabled(bool enable)
    {
        foreach (Collider sheepCollider in solidColliders)
        {
            sheepCollider.enabled = enable;
        }
    }

    // Removes the up/down part of a vector, so it lies flat on the ground.
    private static Vector3 Flatten(Vector3 vector)
    {
        return new Vector3(vector.x, 0f, vector.z);
    }
}