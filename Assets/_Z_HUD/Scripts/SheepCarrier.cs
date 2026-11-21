using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------------------------
// SheepCarrier
// ---------------------------------------------------------------------------------------------
// The player's side of carrying sheep. It knows where the SheepHolder is, which sheep is being
// held, and which sheep is on its way. It also listens for the drop button.
//
// Two sheep slots:
//   incomingSheep - a sheep that is running or jumping towards the holder.
//   heldSheep     - the sheep sitting on the holder right now.
//
// Setup:
//   - Add this to the ROOT of the Player prefab (next to SheepSpawner).
//   - Drag the Player's SheepHolder child into Sheep Holder.
//   - Drag Player/Interact from Assets/InputSystem_Actions into Drop Action.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Lets the player carry one sheep at a time on the SheepHolder. Starts a sheep's run to the
/// holder, swaps out the old sheep when a new one arrives, and drops the held sheep in front of
/// the player when the drop button is pressed.
/// </summary>
public class SheepCarrier : MonoBehaviour
{
    [Header("Holding")]
    [Tooltip("Where a carried sheep sits. Drag the Player's SheepHolder child here.")]
    [SerializeField] private Transform sheepHolder;

    [Header("Input")]
    [Tooltip("The action that drops the held sheep. Use Player/Interact for now, and switch to Player/DropSheep once the team adds it.")]
    [SerializeField] private InputActionReference dropAction;

    [Header("Dropping")]
    [Tooltip("How far in front of the player a dropped sheep appears, in metres. Keep it more than the player's radius plus the sheep's radius, so they do not overlap.")]
    [SerializeField] private float dropDistance = 1.5f;

    [Tooltip("Forward speed given to a dropped sheep, in metres per second.")]
    [SerializeField] private float dropForwardSpeed = 2f;

    [Tooltip("Upward speed given to a dropped sheep, in metres per second. Makes it hop off.")]
    [SerializeField] private float dropUpwardSpeed = 2f;

    private SheepCarriable heldSheep;
    private SheepCarriable incomingSheep;
    public Transform SheepHolder => sheepHolder;
    public bool IsHoldingSheep => heldSheep != null;

    /// <summary>
    /// Warns early about a missing Sheep Holder, so the mistake shows up once at the start.
    /// </summary>
    private void Awake()
    {
        if (sheepHolder == null)
        {
            Debug.LogWarning("[SheepCarrier] Sheep Holder is not assigned. Drag the Player's SheepHolder child into it.", this);
        }
    }

    /// <summary>
    /// Starts listening for the drop button whenever this component is enabled.
    /// </summary>
    private void OnEnable()
    {
        if(dropAction == null || dropAction.action == null) return;

        dropAction.action.performed += HandleDropPerformed;
        dropAction.action.Enable();
    }

    /// <summary>
    /// Stops listening for the drop button whenever this component is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        if (dropAction == null || dropAction.action == null) return;
        dropAction.action.performed -= HandleDropPerformed;
    }

    /// <summary>
    /// Sends the given sheep running to the holder. If another sheep is still on its way, that
    /// one is cancelled first. A sheep already on the holder stays there until the new sheep
    /// starts its jump. Called by SheepSpawner right after it spawns a sheep.
    /// </summary>
    public void Carry(SheepCarriable sheep)
    {
        if (sheep == null || sheep == incomingSheep || sheep == heldSheep) return;

        if (sheepHolder == null) return;

        if(incomingSheep != null)
        {
            incomingSheep.CancelCarry();
        }

        incomingSheep = sheep;
        sheep.BeginCarry(this);
    }

    /// <summary>
    /// Drops the held sheep in front of the player with a small hop. Returns true if a sheep
    /// was dropped, or false if the player was not holding one.
    /// </summary>
    public bool TryDropHeldSheep()
    {
        if(heldSheep == null) return false;

        // Drop the sheep in front of the player with a small hop.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 dropPosition = transform.position + forward * dropDistance;
        dropPosition.y = sheepHolder.position.y;
        Vector3 launchVelocity = forward * dropForwardSpeed + Vector3.up * dropUpwardSpeed;

        // Clear the held sheep before calling Drop, so it does not try to call back into us.
        SheepCarriable sheepToDrop = heldSheep;
        heldSheep = null;
        sheepToDrop.Drop(dropPosition, launchVelocity);

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

    // Called by the Input System when the drop action is performed. The parameter type must
    // match the event: Action<InputAction.CallbackContext>. We do not need its data here.
    private void HandleDropPerformed(InputAction.CallbackContext context)
    {
        TryDropHeldSheep();
    }
}