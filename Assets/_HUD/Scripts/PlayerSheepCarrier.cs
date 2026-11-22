using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// ---------------------------------------------------------------------------------------------
// PlayerSheepCarrier
// ---------------------------------------------------------------------------------------------
// The player's side of carrying sheep. It knows where the SheepHolder is, which sheep is being
// held, and which sheep is on its way. It also listens for the drop button.
//
// Two sheep slots:
//   incomingSheep - a sheep that is running or jumping towards the holder.
//   heldSheep     - the sheep sitting on the holder right now.
//
// Setup:
//   - Add this to the ROOT of the Player prefab (next to PlayerSheepSpawner).
//   - Drag the Player's SheepHolder child into Sheep Holder.
//   - Drag Player/Interact from Assets/InputSystem_Actions into Drop Action.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Lets the player carry one sheep at a time on the SheepHolder. Starts a sheep's run to the
/// holder, swaps out the old sheep when a new one arrives, and drops the held sheep in front of
/// the player when the drop button is pressed.
/// </summary>
public class PlayerSheepCarrier : MonoBehaviour
{
    [Header("Holding")]
    [Tooltip("Where a carried sheep sits. Drag the Player's SheepHolder child here.")]
    [SerializeField] private Transform sheepHolder;

    [Header("Input")]
    [Tooltip("Drops the held sheep, or picks up a nearby one if none is held. Use Player/Interact.")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Picking Up")]
    [Tooltip("How close a roaming sheep must be to get picked up, in metres.")]
    [SerializeField] private float pickupRadius = 2.5f;

    [Tooltip("How high above the player's feet the pickup search is centred, in metres. About waist height, so sheep on small slopes are still found.")]
    [SerializeField] private float pickupCenterHeight = 1f;

    [Tooltip("Layers to search for sheep. Everything works for now. Once the team adds a Sheep layer, select only that, so the search skips unrelated colliders.")]
    [SerializeField] private LayerMask sheepLayerMask = ~0;

    [Header("Dropping")]
    [Tooltip("How far in front of the player a dropped sheep appears, in metres. Keep it more than the player's radius plus the sheep's radius, so they do not overlap.")]
    [SerializeField] private float dropDistance = 1.5f;

    [Tooltip("Forward speed given to a dropped sheep, in metres per second.")]
    [SerializeField] private float dropForwardSpeed = 2f;

    [Tooltip("Upward speed given to a dropped sheep, in metres per second. Makes it hop off.")]
    [SerializeField] private float dropUpwardSpeed = 2f;

    // The most colliders one pickup search can return.
    private const int MaxOverlapResults = 32;

    // One array, created once and reused for every pickup search.
    private readonly Collider[] overlapResults = new Collider[MaxOverlapResults];
    private SheepCarriable heldSheep;
    private SheepCarriable incomingSheep;
    public Transform SheepHolder => sheepHolder;
    public bool IsHoldingSheep => heldSheep != null;

    // The centre of the pickup search: above the player's feet by Pickup Center Height.
    // Used by both the search and the gizmo, so the drawn circle always matches the real one.
    private Vector3 PickupCenter => transform.position + Vector3.up * pickupCenterHeight;

    /// <summary>
    /// Warns early about a missing Sheep Holder, so the mistake shows up once at the start.
    /// </summary>
    private void Awake()
    {
        if (sheepHolder == null)
        {
            Debug.LogWarning("[PlayerSheepCarrier] Sheep Holder is not assigned. Drag the Player's SheepHolder child into it.", this);
        }
    }

    /// <summary>
    /// Starts listening for the drop button whenever this component is enabled.
    /// </summary>
    private void OnEnable()
    {
        if(interactAction == null || interactAction.action == null) return;

        interactAction.action.performed += HandleInteractPerformed;
        interactAction.action.Enable();
    }

    /// <summary>
    /// Stops listening for the drop button whenever this component is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        if (interactAction == null || interactAction.action == null) return;
        interactAction.action.performed -= HandleInteractPerformed;
    }

    /// <summary>
    /// Sends the given sheep running to the holder. If another sheep is still on its way, that
    /// one is cancelled first. A sheep already on the holder stays there until the new sheep
    /// starts its jump. Called by PlayerSheepSpawner right after it spawns a sheep.
    /// </summary>
    public void Carry(SheepCarriable sheep)
    {
        // A busy sheep is already held, already on its way, or returning to the inventory.
        if (sheep == null || sheep.IsBusy) return;

        if (sheepHolder == null) return;

        if(incomingSheep != null)
        {
            incomingSheep.CancelCarry();
        }

        incomingSheep = sheep;
        sheep.BeginCarry(this);
    }

    /// <summary>
    /// Picks up the nearest roaming sheep within Pickup Radius. Returns true if one was found
    /// and sent running to the holder, or false if no free sheep was in range.
    /// </summary>
    public bool TryPickUpNearbySheep()
    {
        // OverlapSphereNonAlloc finds every collider touching an invisible sphere. It writes
        // them into OUR array and returns how many it found. Only the first "hitCount" slots
        // hold results from this search. Anything after that is left over from older searches
        // and must be ignored. QueryTriggerInteraction.Ignore skips trigger zones.
        int hitCount = Physics.OverlapSphereNonAlloc(
            PickupCenter, pickupRadius, overlapResults, sheepLayerMask, QueryTriggerInteraction.Ignore);

        SheepCarriable nearestSheep = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            // The collider is on the sheep's Sphere child, but SheepCarriable is on the root.
            // GetComponentInParent checks this object first, then walks UP the hierarchy until
            // it finds one. The ground, the player and so on simply return null.
            SheepCarriable sheep = overlapResults[i].GetComponentInParent<SheepCarriable>();

            // Skip non-sheep, and busy sheep.
            if (sheep == null || sheep.IsBusy) continue;

            // Squared distance is enough for "which is closest" and skips a square root.
            float sqrDistance = (sheep.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestSheep = sheep;
            }
        }

        if (nearestSheep == null)
        {
            return false;
        }

        Carry(nearestSheep);
        return true;
    }

    /// <summary>
    /// Drops the held sheep in front of the player with a small hop. Returns true if a sheep
    /// was dropped, or false if the player was not holding one.
    /// </summary>
    public bool TryDropHeldSheep()
    {
        // "out _" is a discard: we must pass something for the out parameter, but we do not
        // need the value here, so the underscore throws it away.
        return TryDropHeldSheep(out _);
    }

    /// <summary>
    /// Drops the held sheep in front of the player with a small hop, and hands back the sheep
    /// that was dropped so the caller can give it a new command. Returns true if a sheep was
    /// dropped, or false (and no sheep) if the player was not holding one.
    /// </summary>
    public bool TryDropHeldSheep(out SheepCarriable droppedSheep)
    {
        droppedSheep = heldSheep;

        if (heldSheep == null)
        {
            return false;
        }

        // Flat forward, so the drop spot is level even if the player is ever tilted.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        // In front of the player at holder height. The sheep then falls to the ground.
        Vector3 dropPosition = transform.position + forward * dropDistance;
        dropPosition.y = sheepHolder.position.y;

        Vector3 launchVelocity = forward * dropForwardSpeed + Vector3.up * dropUpwardSpeed;

        // Clear our reference BEFORE telling the sheep to drop.
        heldSheep = null;
        droppedSheep.Drop(dropPosition, launchVelocity);

        return true;
    }

    /// <summary>
    /// Called by CarriableSheep when it starts its jump onto the holder. Drops the sheep that is
    /// currently held, so the new one can take its place.
    /// </summary>
    public void HandleSheepJumping(SheepCarriable sheep)
    {
        if (sheep != incomingSheep) return;
        TryDropHeldSheep();
    }

    /// <summary>
    /// Called by CarriableSheep when it lands on the holder. That sheep becomes the held sheep.
    /// </summary>
    public void HandleSheepAttached(SheepCarriable sheep)
    {
        if (sheep != incomingSheep) return;
        heldSheep = sheep;
        incomingSheep = null;
    }

    /// <summary>
    /// Called by SheepCarriable when it stops being carried for its own reasons, such as being
    /// recalled to the inventory. Forgets the sheep, whether it was held or still on its way.
    /// </summary>
    public void HandleSheepLeaving(SheepCarriable sheep)
    {
        if (sheep == heldSheep)
        {
            heldSheep = null;
        }

        if (sheep == incomingSheep)
        {
            incomingSheep = null;
        }
    }

    // Called by the Input System when the interact action is performed. The parameter type must
    // match the event: Action<InputAction.CallbackContext>. We do not need its data here.
    private void HandleInteractPerformed(InputAction.CallbackContext context)
    {
        if (IsHoldingSheep)
        {
            TryDropHeldSheep();

        }
        else
        {
            TryPickUpNearbySheep();
        }
    }

    /// <summary>
    /// Draws the pickup range as a cyan wire sphere in the Scene view when the Player is
    /// selected, so Pickup Radius can be tuned by eye. Editor only; not seen in the game.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(PickupCenter, pickupRadius);
    }
}