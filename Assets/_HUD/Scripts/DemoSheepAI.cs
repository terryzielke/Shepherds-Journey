using UnityEngine;

// ---------------------------------------------------------------------------------------------
// DemoSheepAI
// ---------------------------------------------------------------------------------------------
// Simple sheep behaviour for testing HUD features in the demo scenes. It is NOT the game's real
// sheep AI (that belongs in Sabine's _S_Sheep folder).
//
// What the sheep does:
//   - Grazes (stands still) for a random few seconds.
//   - Wanders slowly to a random spot nearby, then grazes again. Repeat forever.
//   - Whenever the player comes closer than its personal space (2 m by default), it stops what
//     it is doing and walks away until it is comfortable again. It never follows the player.
//
// How it is built: a tiny "state machine".
//   A state machine means the sheep is always in exactly ONE state, and each state has its own
//   small piece of logic plus a rule for when to switch to another state:
//
//       Idle   --(timer runs out)-->           Wander
//       Wander --(arrived, or took too long)--> Idle
//
//   Fleeing from the player is checked BEFORE the state machine every physics step. If the
//   player is too close, fleeing wins and the state machine is skipped for that step. When the
//   sheep has escaped, it goes back to Idle and the normal cycle carries on.
//
// Why move with the Rigidbody instead of the Transform?
//   The Sheep prefab has a Rigidbody, so the physics engine owns its position. If we moved
//   transform.position directly, we would be fighting physics: sheep could jitter, slide
//   through walls, or ignore collisions. Instead we tell the Rigidbody how fast to go
//   (its velocity) and let physics do the actual moving. That is why the movement code runs
//   in FixedUpdate, which runs in step with the physics engine, instead of Update.
//
// Setup in the Editor:
//   - Add this script to the ROOT of the Sheep prefab (the object that has the Rigidbody).
//   - Leave Player empty. Sheep are spawned at runtime and a prefab cannot point at an object in
//     a scene, so each sheep finds the DemoPlayerController by itself when it spawns.
//   - Rotation constraints, interpolation, and a frictionless physics material are all set by
//     this script in Awake, so the Rigidbody and collider settings do not need to be changed by
//     hand.
//
// Why frictionless?
//   Rotation is frozen so the sheep cannot roll, which means it has to SLIDE over the ground.
//   With Unity's default friction (0.6), sliding brakes the sheep at about 5.9 m/s per second,
//   which is stronger than our acceleration, so the sheep could never get moving. This script
//   already controls speeding up and slowing down itself, so friction is not needed.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Demo sheep behaviour. The sheep grazes, wanders slowly to random nearby spots, and walks
/// away from the player whenever the player gets closer than its personal space. Movement is
/// done through the Rigidbody so it works with physics and gravity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DemoSheepAI : MonoBehaviour
{
    // The states the sheep can be in. An enum gives each state a readable name instead of a
    // magic number, and the compiler stops us from using a state that does not exist.
    private enum SheepState
    {
        Idle,
        Wander
    }

    [Header("Player")]
    [Tooltip("The player to keep away from. Leave empty to find the DemoPlayerController in the scene.")]
    [SerializeField] private Transform player;

    [Tooltip("How close the player may get before the sheep walks away, in metres.")]
    [SerializeField] private float personalSpace = 2f;

    [Tooltip("Extra distance the sheep keeps walking after leaving its personal space, in metres. " +
             "Stops the sheep stuttering back and forth right on the edge.")]
    [SerializeField] private float personalSpaceBuffer = 0.5f;

    [Header("Speed")]
    [Tooltip("Walking speed while wandering, in metres per second. Sheep are slow.")]
    [SerializeField] private float wanderSpeed = 1f;

    [Tooltip("Walking speed while moving away from the player, in metres per second.")]
    [SerializeField] private float fleeSpeed = 1.5f;

    [Tooltip("How quickly the sheep speeds up and slows down, in metres per second, per second. " +
             "Lower values make it ease in and out of walking more gently.")]
    [SerializeField] private float acceleration = 3f;

    [Tooltip("How quickly the sheep turns to face where it is walking, in degrees per second.")]
    [SerializeField] private float turnSpeed = 180f;

    [Header("Wandering")]
    [Tooltip("Furthest a wander spot can be from where the sheep is standing, in metres.")]
    [SerializeField] private float wanderRadius = 5f;

    [Tooltip("Shortest time the sheep grazes before wandering again, in seconds.")]
    [SerializeField] private float minIdleTime = 1f;

    [Tooltip("Longest time the sheep grazes before wandering again, in seconds.")]
    [SerializeField] private float maxIdleTime = 4f;

    [Tooltip("How close the sheep must get to its wander spot to count as arrived, in metres.")]
    [SerializeField] private float arriveDistance = 0.3f;

    [Tooltip("Longest the sheep keeps trying to reach a wander spot, in seconds. If it is blocked " +
             "by something, it gives up after this time instead of walking into a wall forever.")]
    [SerializeField] private float maxWanderTime = 8f;

    // How many random spots we try when looking for a wander spot that is not too close to the
    // player. A few tries is plenty; if they all fail, the sheep just grazes a bit longer.
    private const int WanderSpotAttempts = 10;

    // Movement directions shorter than this are treated as "not moving", so the sheep does not
    // spin on the spot trying to face a tiny leftover direction.
    private const float MinDirectionSqrMagnitude = 0.0001f;

    // One zero-friction material shared by EVERY sheep.
    // "static" means this field belongs to the DemoSheepAI class itself, not to each sheep.
    // There is only one copy of it in the whole game, no matter how many sheep exist. The first
    // sheep to wake up creates the material, and every sheep after that reuses it, instead of
    // each sheep making its own identical copy.
    private static PhysicsMaterial frictionlessMaterial;

    // Cached Rigidbody, fetched once in Awake instead of every physics step.
    private Rigidbody body;

    // The state the sheep is in right now.
    private SheepState currentState;

    // Counts down while grazing (Idle) or wandering. Its meaning depends on the current state.
    private float stateTimer;

    // Where the sheep is walking to while in the Wander state.
    private Vector3 wanderTarget;

    // True while the sheep is walking away from the player.
    private bool isFleeing;

    /// <summary>
    /// Caches the Rigidbody and sets it up for a walking animal: no tipping over, and smooth
    /// movement on screen.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Freeze tipping forward/back (X) and side to side (Z). The sheep's collider is a sphere,
        // so without this it would roll like a ball when pushed. Y stays free so it can turn.
        // The |= keeps any constraints already set on the prefab and adds ours on top.
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Physics runs at a fixed rate (50 times a second by default), but the screen usually
        // draws faster. Interpolation smooths the sheep's drawn position between physics steps,
        // so it does not look jittery when the camera moves.
        body.interpolation = RigidbodyInterpolation.Interpolate;

        RemoveFriction();
    }

    // Puts the shared zero-friction material on all of the sheep's solid colliders, so it can
    // slide over the ground (see "Why frictionless?" at the top of this file).
    private void RemoveFriction()
    {
        // Create the shared material only once. Unity's "== null" also returns true if the
        // material was destroyed (for example, when leaving Play Mode), so it is safely
        // re-created when needed.
        if (frictionlessMaterial == null)
        {
            frictionlessMaterial = new PhysicsMaterial("Sheep Frictionless")
            {
                dynamicFriction = 0f, // friction while sliding
                staticFriction = 0f,  // friction that holds a standing object in place

                // When two colliders touch, Unity combines their two friction values into one.
                // The default is Average, so 0 on the sheep and 0.6 on the ground would give
                // 0.3. Minimum picks the lower value, so the sheep always gets 0, whatever
                // the ground's material is.
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }

        // The collider is on the Sphere child, not the root, so we search the children too.
        // GetComponentsInChildren also checks this object, in case a collider is added here later.
        foreach (Collider sheepCollider in GetComponentsInChildren<Collider>())
        {
            // Triggers are invisible zones that never touch the ground, so leave them alone.
            if (sheepCollider.isTrigger)
            {
                continue;
            }

            // sharedMaterial assigns our one shared material directly. Using ".material"
            // instead would make Unity quietly create a separate copy for every collider,
            // which wastes memory and defeats the point of sharing it.
            sheepCollider.sharedMaterial = frictionlessMaterial;
        }
    }

    /// <summary>
    /// Resets the sheep to a fresh start (grazing, not fleeing) every time it is switched on.
    /// This runs when the sheep first appears, when the SheepPool shows a reused sheep, and
    /// when SheepCarriable switches this script back on after a drop.
    /// </summary>
    private void OnEnable()
    {
        isFleeing = false;
        EnterIdle();
    }

    /// <summary>
    /// Finds the player if needed. Start runs only once per sheep, even if the sheep is hidden
    /// and reused by the SheepPool, so this slow scene search happens once per sheep instead of
    /// once per spawn. The fresh-start setup lives in OnEnable, which runs on every reuse.
    /// </summary>
    private void Start()
    {
        if (player == null)
        {
            // Searching the scene is slow, so it only happens once per sheep.
            DemoPlayerController playerController = FindAnyObjectByType<DemoPlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
            else
            {
                Debug.LogWarning("[DemoSheepAI] No Player assigned and no DemoPlayerController found. " +
                                 "The sheep will wander but will not avoid anyone.", this);
            }
        }
    }

    /// <summary>
    /// Runs once per physics step. Decides which way the sheep wants to go, then moves and
    /// turns it through the Rigidbody.
    /// </summary>
    private void FixedUpdate()
    {
        Vector3 desiredVelocity;

        // Fleeing is checked first, so it always wins over grazing and wandering.
        if (TryGetFleeDirection(out Vector3 fleeDirection))
        {
            desiredVelocity = fleeDirection * fleeSpeed;
        }
        else
        {
            desiredVelocity = UpdateStateMachine();
        }

        ApplyMovement(desiredVelocity);
    }

    // Checks whether the player is too close. Returns true, and gives the direction straight away
    // from the player, if the sheep should flee this step.
    private bool TryGetFleeDirection(out Vector3 fleeDirection)
    {
        fleeDirection = Vector3.zero;

        if (player == null)
        {
            return false;
        }

        // Only the flat (horizontal) distance matters. The player's position is at their feet,
        // but the sheep's might not be, and we do not want height to change the result.
        Vector3 awayFromPlayer = transform.position - player.position;
        awayFromPlayer.y = 0f;
        float distance = awayFromPlayer.magnitude;

        // Hysteresis: we START fleeing at personalSpace, but only STOP once the sheep is a bit
        // further away (personalSpace + buffer). With a single line for both, a sheep standing
        // right on the edge would flip between fleeing and not fleeing every step and stutter.
        float limit = isFleeing ? personalSpace + personalSpaceBuffer : personalSpace;

        if (distance >= limit)
        {
            if (isFleeing)
            {
                // Just escaped. Go back to grazing, which restarts the normal cycle.
                isFleeing = false;
                EnterIdle();
            }

            return false;
        }

        isFleeing = true;

        // If the player is standing exactly on the sheep, "away" has no direction, so just walk
        // the way the sheep is already facing.
        if (awayFromPlayer.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            fleeDirection = transform.forward;
        }
        else
        {
            // Dividing by the length turns it into a direction of length 1 (normalizing).
            // We already have the length, so this saves Unity working it out again.
            fleeDirection = awayFromPlayer / distance;
        }

        return true;
    }

    // Runs the logic for the current state, switches state when needed, and returns the velocity
    // the sheep wants this step.
    private Vector3 UpdateStateMachine()
    {
        // A switch on the state keeps each state's logic in its own clear, separate block.
        switch (currentState)
        {
            case SheepState.Idle:
                return UpdateIdle();

            case SheepState.Wander:
                return UpdateWander();

            default:
                return Vector3.zero;
        }
    }

    // Idle: stand still and graze until the timer runs out, then try to start wandering.
    private Vector3 UpdateIdle()
    {
        // Inside FixedUpdate, time passes in fixed steps, so we count down by fixedDeltaTime.
        stateTimer -= Time.fixedDeltaTime;

        if (stateTimer <= 0f)
        {
            if (TryPickWanderTarget(out Vector3 target))
            {
                EnterWander(target);
            }
            else
            {
                // No good spot found (the player is in the way everywhere we tried). Graze a
                // little longer and try again later.
                EnterIdle();
            }
        }

        // Grazing means standing still.
        return Vector3.zero;
    }

    // Wander: walk slowly towards the wander spot. Go back to grazing on arrival, or if it is
    // taking too long (probably blocked by something).
    private Vector3 UpdateWander()
    {
        stateTimer -= Time.fixedDeltaTime;

        Vector3 toTarget = wanderTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= arriveDistance || stateTimer <= 0f)
        {
            EnterIdle();
            return Vector3.zero;
        }

        return toTarget.normalized * wanderSpeed;
    }

    // Switches to the Idle state with a fresh random grazing time.
    private void EnterIdle()
    {
        currentState = SheepState.Idle;
        stateTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    // Switches to the Wander state, heading for the given spot.
    private void EnterWander(Vector3 target)
    {
        currentState = SheepState.Wander;
        wanderTarget = target;
        stateTimer = maxWanderTime;
    }

    // Picks a random spot within wanderRadius of the sheep that is NOT inside the player's
    // personal space, so the sheep never wanders straight back towards the player.
    // Returns false if no such spot was found in a few tries.
    private bool TryPickWanderTarget(out Vector3 target)
    {
        for (int attempt = 0; attempt < WanderSpotAttempts; attempt++)
        {
            // insideUnitCircle gives a random point in a circle of radius 1 (x and y).
            // We scale it up to wanderRadius and use its y as our z, because the ground is the
            // X-Z plane in Unity (Y is up).
            Vector2 randomPoint = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = transform.position + new Vector3(randomPoint.x, 0f, randomPoint.y);

            if (!IsInsidePlayerSpace(candidate))
            {
                target = candidate;
                return true;
            }
        }

        target = transform.position;
        return false;
    }

    // Returns true if the given point is within the player's personal space (plus the buffer).
    private bool IsInsidePlayerSpace(Vector3 point)
    {
        if (player == null)
        {
            return false;
        }

        Vector3 offset = point - player.position;
        offset.y = 0f;

        // Comparing squared lengths avoids a square root, which is a small, common speed-up for
        // distance checks. (a * a < b * b gives the same answer as a < b for positive numbers.)
        float limit = personalSpace + personalSpaceBuffer;
        return offset.sqrMagnitude < limit * limit;
    }

    // Moves the sheep towards the desired velocity and turns it to face where it is going.
    private void ApplyMovement(Vector3 desiredVelocity)
    {
        float step = Time.fixedDeltaTime;

        // Split the current velocity into flat (horizontal) and vertical parts. We only steer
        // the flat part. The vertical part is left alone so gravity keeps working.
        Vector3 currentVelocity = body.linearVelocity;
        Vector3 flatVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        // MoveTowards changes the velocity by at most "acceleration * step" each physics step.
        // That makes the sheep ease into walking and ease to a stop, instead of snapping
        // instantly between standing and full speed.
        flatVelocity = Vector3.MoveTowards(flatVelocity, desiredVelocity, acceleration * step);

        body.linearVelocity = new Vector3(flatVelocity.x, currentVelocity.y, flatVelocity.z);

        // Turn to face the direction the sheep WANTS to go. Only turn while it actually wants to
        // move, otherwise a grazing sheep would snap back to some default direction.
        if (desiredVelocity.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity, Vector3.up);

            // RotateTowards turns by at most "turnSpeed * step" degrees, so the sheep turns
            // smoothly. MoveRotation is the Rigidbody way to rotate, which keeps physics and
            // interpolation happy (setting transform.rotation would bypass them).
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, targetRotation, turnSpeed * step));
        }
    }

    /// <summary>
    /// Draws helper shapes in the Scene view when the sheep is selected: a yellow circle for its
    /// wander range and a line to its current wander spot. Editor only; not seen in the game.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);

        // Only draw the target line while playing and wandering; otherwise wanderTarget is old.
        if (Application.isPlaying && currentState == SheepState.Wander && !isFleeing)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, wanderTarget);
            Gizmos.DrawSphere(wanderTarget, 0.15f);
        }
    }
}
