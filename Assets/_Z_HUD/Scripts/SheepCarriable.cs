using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------------------------
// SheepCarriable
// ---------------------------------------------------------------------------------------------
// Lets a sheep be commanded by the player. A sheep follows one command at a time:
//
//   CARRY (started by PlayerSheepCarrier.Carry):
//     1. Run around the player (never through them) to a landing spot in front of them.
//     2. Jump in an arc onto the carrier's SheepHolder.
//     3. Attach to the holder, so it moves with the player.
//     Later the carrier calls Drop, or CancelCarry if a newer sheep takes over.
//
//   RETURN (started by PlayerSheepRecaller):
//     1. Run around the player to the point behind the camera.
//     2. Tell the recaller it has arrived. The recaller adds it to the inventory and
//        destroys it.
//
// Shared run logic (RunAroundPlayer):
//   Both commands run to a target that can MOVE (the player walks, the camera turns). So
//   instead of a fixed position, RunAroundPlayer is given a FUNCTION that returns the target,
//   and it calls that function every physics step to get the latest position. If the player
//   is standing between the sheep and the target, the sheep first runs to a side point beside
//   the player, so it never runs through them.
//
// The list of all sheep (ActiveSheep):
//   Every enabled sheep adds itself to one shared, static list, and removes itself when it is
//   disabled or destroyed. PlayerSheepRecaller loops over this list to find nearby sheep, which is
//   cheaper and more reliable than a big physics search.
//
// Why turn off other behaviours?
//   While commanded, this script is in charge. Other sheep brains (DemoSheepAI now, Sabine's
//   real sheep AI later) would fight it. Put them in Behaviours To Disable. They are turned
//   back on when the sheep is dropped or a carry is cancelled. A returned sheep is destroyed.
//
// Setup:
//   - Add this to the ROOT of the Sheep prefab (the object with the Rigidbody).
//   - Drag DemoSheepAI into Behaviours To Disable.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Lets a sheep be carried by a PlayerSheepCarrier or sent back to the inventory by a PlayerSheepRecaller.
/// The sheep runs around the player to its target, switching off its other behaviours while it
/// follows a command. Also keeps a shared list of every active sheep in the scene.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SheepCarriable : MonoBehaviour
{
    [Header("Other Behaviours")]
    [Tooltip("Scripts on this sheep to turn off while it follows a command, such as DemoSheepAI. " +
             "They are turned back on when it is dropped.")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;

    [Header("Running")]
    [Tooltip("Running speed, in metres per second.")]
    [SerializeField] private float runSpeed = 5f;

    [Tooltip("How quickly the sheep speeds up and changes direction, in metres per second, per second.")]
    [SerializeField] private float acceleration = 25f;

    [Tooltip("How quickly the sheep turns to face where it is running, in degrees per second.")]
    [SerializeField] private float turnSpeed = 720f;

    [Tooltip("How far to the side of the player the sheep runs when going around them, in metres. " +
             "Keep it wider than the player and sheep together, or the sheep will clip the player.")]
    [SerializeField] private float sidePointDistance = 2f;

    [Tooltip("How close the sheep must get to the side point to count as reaching it, in metres.")]
    [SerializeField] private float pointReachedDistance = 0.5f;

    [Header("Carrying")]
    [Tooltip("How far in front of the player the sheep stops to jump, in metres.")]
    [SerializeField] private float landingSpotDistance = 2f;

    [Tooltip("Longest the sheep runs towards the player, in seconds. If it gets stuck, it jumps " +
             "from wherever it is when this time runs out.")]
    [SerializeField] private float maxCarryRunTime = 6f;

    [Tooltip("How long the jump onto the holder takes, in seconds.")]
    [SerializeField] private float jumpDuration = 0.5f;

    [Tooltip("How high the jump arc rises above a straight line to the holder, in metres.")]
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Returning")]
    [Tooltip("How close the sheep must get to the point behind the camera to count as home, in " +
             "metres. Wider than the side point, so a crowd of returning sheep does not get " +
             "stuck jostling for one exact spot.")]
    [SerializeField] private float returnReachedDistance = 1.5f;

    [Tooltip("Longest the sheep runs back, in seconds. If it gets stuck, it goes back into the " +
             "inventory from wherever it is when this time runs out, so no sheep is ever lost.")]
    [SerializeField] private float maxReturnRunTime = 12f;

    // Directions shorter than this count as "no direction", so the sheep does not spin on the spot.
    private const float MinDirectionSqrMagnitude = 0.0001f;

    // One shared WaitForFixedUpdate, reused forever instead of creating one every physics step.
    private static readonly WaitForFixedUpdate WaitForPhysicsStep = new WaitForFixedUpdate();

    // Every enabled sheep in the scene. "static" means there is ONE list shared by the whole
    // SheepCarriable class, not one list per sheep. Private, so only sheep can add or remove.
    private static readonly List<SheepCarriable> activeSheep = new List<SheepCarriable>();

    // Cached in Awake so we do not search for them every step.
    private Rigidbody body;
    private Collider[] solidColliders;

    // Who is commanding this sheep right now. At most one of these is set at a time.
    private PlayerSheepCarrier carrier;
    private PlayerSheepRecaller recaller;

    // True while physics is switched off for the jump and the ride (kinematic, no colliders).
    private bool physicsSuspended;

    // The Rigidbody's interpolation before the jump, so it can be put back exactly.
    private RigidbodyInterpolation savedInterpolation;

    /// <summary>
    /// Every enabled sheep in the scene. Read-only: other scripts can loop over it, but only
    /// sheep add or remove themselves.
    /// </summary>
    public static IReadOnlyList<SheepCarriable> ActiveSheep => activeSheep;

    /// <summary>
    /// True once the sheep has landed on the holder and is riding with the player.
    /// </summary>
    public bool IsAttached { get; private set; }

    /// <summary>
    /// True from the moment a carry starts until the sheep is dropped or the carry is
    /// cancelled. That covers running, jumping and riding.
    /// </summary>
    public bool IsBeingCarried => carrier != null;

    /// <summary>
    /// True while the sheep is running back to be returned to the inventory.
    /// </summary>
    public bool IsReturning => recaller != null;

    /// <summary>
    /// True if the sheep is following any command (being carried or returning). A busy sheep
    /// cannot be picked up or given a new carry.
    /// </summary>
    public bool IsBusy => IsBeingCarried || IsReturning;

    /// <summary>
    /// Caches the Rigidbody and the solid colliders, and stops the sheep from tipping over
    /// while it runs.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Only SOLID colliders are switched off while riding, because those are the ones that
        // would block the player's CharacterController. The collider is on the Sphere child.
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        solidColliders = Array.FindAll(allColliders, sheepCollider => !sheepCollider.isTrigger);
    }

    /// <summary>
    /// Adds this sheep to the shared list of active sheep.
    /// </summary>
    private void OnEnable()
    {
        activeSheep.Add(this);
    }

    /// <summary>
    /// Removes this sheep from the shared list. Also runs when the sheep is destroyed, so a
    /// destroyed sheep never stays in the list.
    /// </summary>
    private void OnDisable()
    {
        activeSheep.Remove(this);
    }

    // Empties the shared list when Play Mode starts. Static fields can survive between Play
    // Mode sessions if "Enter Play Mode Options" skips the domain reload, which would leave
    // destroyed sheep from the last session in the list. SubsystemRegistration runs very early,
    // before any scene object wakes up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveSheep()
    {
        activeSheep.Clear();
    }

    /// <summary>
    /// Starts the carry: the sheep turns off its other behaviours, runs around to the front of
    /// the given carrier's player, jumps onto the holder and attaches. Does nothing if the
    /// carrier is missing, has no holder, or this sheep is already busy.
    /// </summary>
    public void BeginCarry(PlayerSheepCarrier newCarrier)
    {
        if (newCarrier == null || newCarrier.SheepHolder == null)
        {
            Debug.LogWarning("[SheepCarriable] BeginCarry needs a PlayerSheepCarrier with a Sheep Holder assigned.", this);
            return;
        }

        if (IsBusy)
        {
            Debug.LogWarning("[SheepCarriable] BeginCarry called while the sheep is already busy.", this);
            return;
        }

        carrier = newCarrier;
        SetBehavioursEnabled(false);
        StartCoroutine(CarryRoutine());
    }

    /// <summary>
    /// Starts the return: the sheep runs around the player to the point behind the camera, then
    /// the given recaller puts it back into the inventory. If the sheep is being carried, the
    /// carry is stopped first and the carrier is told the sheep has left. Does nothing if the
    /// recaller is missing or the sheep is already returning.
    /// </summary>
    public void BeginReturn(PlayerSheepRecaller newRecaller)
    {
        if (newRecaller == null || IsReturning)
        {
            return;
        }

        if (carrier != null)
        {
            // Stop the carry where the sheep is right now. Running: keep its speed. Mid-jump or
            // riding: physics was off, so start from still and let gravity take over.
            Vector3 keepVelocity = physicsSuspended ? Vector3.zero : body.linearVelocity;
            ReleaseToPhysics(transform.position, keepVelocity);

            // Clear our reference BEFORE telling the carrier, then let it forget us too, so
            // neither side ever points at a command that has ended.
            PlayerSheepCarrier oldCarrier = carrier;
            carrier = null;
            oldCarrier.HandleSheepLeaving(this);
        }

        recaller = newRecaller;
        SetBehavioursEnabled(false);
        StartCoroutine(ReturnRoutine());
    }

    /// <summary>
    /// Drops the sheep at the given world position and launches it with the given velocity.
    /// Physics and the sheep's other behaviours are switched back on. Called by PlayerSheepCarrier.
    /// </summary>
    public void Drop(Vector3 dropPosition, Vector3 launchVelocity)
    {
        if (carrier == null)
        {
            return;
        }

        ReleaseToPhysics(dropPosition, launchVelocity);
        carrier = null;
        SetBehavioursEnabled(true);
    }

    /// <summary>
    /// Stops the carry wherever the sheep is right now (running or mid-jump) and hands it back
    /// to its normal behaviours. Called by PlayerSheepCarrier when a newer sheep takes over.
    /// </summary>
    public void CancelCarry()
    {
        if (carrier == null)
        {
            return;
        }

        Vector3 keepVelocity = physicsSuspended ? Vector3.zero : body.linearVelocity;
        ReleaseToPhysics(transform.position, keepVelocity);
        carrier = null;
        SetBehavioursEnabled(true);
    }

    // The carry sequence: run, then jump, then attach.
    private IEnumerator CarryRoutine()
    {
        Transform player = carrier.transform;

        // 1. Run around to the landing spot. "() => GetLandingSpot(player)" is a lambda: a tiny
        //    nameless function. RunAroundPlayer calls it every step to get the latest spot.
        //    "yield return" on another coroutine method waits here until that one finishes.
        yield return RunAroundPlayer(player, () => GetLandingSpot(player), pointReachedDistance, maxCarryRunTime);

        // 2. Jump onto the holder. Tell the carrier first, so it can drop the sheep it holds.
        carrier.HandleSheepJumping(this);
        SuspendPhysics();

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            // t goes from 0 (start of jump) to 1 (landed).
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // Read the holder's position every frame, so the jump lands even if the player moves.
            Transform holder = carrier.SheepHolder;
            Vector3 straightLine = Vector3.Lerp(startPosition, holder.position, t);

            // 4 * h * t * (1 - t) is a parabola: 0 at the start and end, h at the halfway point.
            float arcHeight = 4f * jumpHeight * t * (1f - t);

            transform.SetPositionAndRotation(
                straightLine + Vector3.up * arcHeight,
                Quaternion.Slerp(startRotation, holder.rotation, t));

            yield return null;
        }

        // 3. Attach to the holder, so the sheep moves with the player for free.
        transform.SetParent(carrier.SheepHolder);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        IsAttached = true;
        carrier.HandleSheepAttached(this);
    }

    // The return sequence: run back, then hand the sheep to the recaller.
    private IEnumerator ReturnRoutine()
    {
        // "recaller.GetReturnPoint" without brackets is a METHOD GROUP: we pass the method
        // itself, not its result, so RunAroundPlayer can call it again every step.
        yield return RunAroundPlayer(recaller.transform, recaller.GetReturnPoint, returnReachedDistance, maxReturnRunTime);

        // Clear our reference first, so the sheep never looks "still returning" after this.
        PlayerSheepRecaller finishedRecaller = recaller;
        recaller = null;

        // The recaller adds this sheep to the inventory and destroys it.
        finishedRecaller.HandleSheepReturned(this);
    }

    // Runs to a target that may be moving, going around the player if they are in the way.
    // getTarget is called every physics step to get the latest target position.
    // Finishes when the sheep is within reachedDistance of the target, or after maxTime seconds.
    private IEnumerator RunAroundPlayer(Transform player, Func<Vector3> getTarget, float reachedDistance, float maxTime)
    {
        Vector3 toSheep = Flatten(body.position - player.position);
        Vector3 towardTarget = GetFlatDirection(player.position, getTarget(), player.forward);

        // A negative dot product means the sheep is on the OPPOSITE side of the player from the
        // target, so running straight there would go through the player. Use a side point.
        bool goAroundSide = Vector3.Dot(toSheep, towardTarget) < 0f;

        // Cross(up, direction) gives the direction 90 degrees to its right. Checking which side
        // of that line the sheep is on tells us the shorter way around. Decided ONCE, so the
        // sheep does not zig-zag if the player turns.
        Vector3 sideAxis = Vector3.Cross(Vector3.up, towardTarget);
        float side = Vector3.Dot(toSheep, sideAxis) < 0f ? -1f : 1f;

        float runTimer = 0f;

        while (runTimer < maxTime)
        {
            Vector3 finalTarget = getTarget();
            Vector3 target = goAroundSide ? GetSidePoint(player, finalTarget, side) : finalTarget;
            float neededDistance = goAroundSide ? pointReachedDistance : reachedDistance;

            Vector3 toTarget = Flatten(target - body.position);

            if (toTarget.sqrMagnitude <= neededDistance * neededDistance)
            {
                if (goAroundSide)
                {
                    // Reached the side point. Head for the real target next, without waiting.
                    goAroundSide = false;
                    continue;
                }

                // Reached the target.
                yield break;
            }

            ApplyRunVelocity(toTarget.normalized * runSpeed);
            runTimer += Time.fixedDeltaTime;

            yield return WaitForPhysicsStep;
        }
    }

    // Turns physics off for the jump and the ride. A kinematic Rigidbody ignores gravity and
    // forces and only moves when our code moves it. Colliders are turned off so the riding
    // sheep does not block the player's CharacterController.
    private void SuspendPhysics()
    {
        // Clear velocity BEFORE going kinematic. Unity warns if you set a kinematic body's velocity.
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        savedInterpolation = body.interpolation;
        body.interpolation = RigidbodyInterpolation.None;

        body.isKinematic = true;
        SetCollidersEnabled(false);
        physicsSuspended = true;
    }

    // Stops whatever the sheep is doing, unparents it, and puts it back under normal physics at
    // the given position with the given velocity. Used by Drop, CancelCarry and BeginReturn, so
    // the physics clean-up is always the same. The caller decides who commands the sheep next.
    private void ReleaseToPhysics(Vector3 position, Vector3 velocity)
    {
        // Stops every coroutine this component started, including a RunAroundPlayer that is
        // running INSIDE CarryRoutine. A stopped coroutine never reaches its remaining lines,
        // which is why all the clean-up lives here and not at the end of the routines.
        StopAllCoroutines();

        // "true" keeps the sheep where it is in the world instead of jumping.
        transform.SetParent(null, true);

        // Teleport while physics is still off, so interpolation does not smear the move.
        transform.position = position;

        if (physicsSuspended)
        {
            body.isKinematic = false;
            body.interpolation = savedInterpolation;
            SetCollidersEnabled(true);
            physicsSuspended = false;
        }

        // Now that the body is not kinematic, it is safe to set its velocity.
        body.linearVelocity = velocity;
        body.angularVelocity = Vector3.zero;

        IsAttached = false;
    }

    // Steers the Rigidbody's flat velocity towards the desired velocity, and turns to face it.
    // The vertical part is left alone so gravity keeps working, and MoveTowards eases the change.
    private void ApplyRunVelocity(Vector3 desiredVelocity)
    {
        float step = Time.fixedDeltaTime;

        Vector3 currentVelocity = body.linearVelocity;
        Vector3 flatVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        flatVelocity = Vector3.MoveTowards(flatVelocity, desiredVelocity, acceleration * step);
        body.linearVelocity = new Vector3(flatVelocity.x, currentVelocity.y, flatVelocity.z);

        if (desiredVelocity.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity, Vector3.up);

            // MoveRotation is the Rigidbody way to turn. Setting transform.rotation would bypass
            // physics and interpolation, which can make the turn look jittery.
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, targetRotation, turnSpeed * step));
        }
    }

    // The point beside the player that the sheep runs to when going around them. "Beside" means
    // 90 degrees from the direction to the final target. side is +1 for right, -1 for left.
    private Vector3 GetSidePoint(Transform player, Vector3 finalTarget, float side)
    {
        Vector3 towardTarget = GetFlatDirection(player.position, finalTarget, player.forward);
        Vector3 sideAxis = Vector3.Cross(Vector3.up, towardTarget);
        return player.position + sideAxis * (side * sidePointDistance);
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

    // The flat direction from one point to another, with a length of 1. If the points are on
    // top of each other there is no direction, so the flattened fallback is used instead.
    private static Vector3 GetFlatDirection(Vector3 from, Vector3 to, Vector3 fallback)
    {
        Vector3 direction = Flatten(to - from);

        if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            direction = Flatten(fallback);
        }

        return direction.normalized;
    }

    // Removes the up/down part of a vector, so it lies flat on the ground.
    private static Vector3 Flatten(Vector3 vector)
    {
        return new Vector3(vector.x, 0f, vector.z);
    }
}