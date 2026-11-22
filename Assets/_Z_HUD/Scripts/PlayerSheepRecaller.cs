using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------------------------
// PlayerSheepRecaller
// ---------------------------------------------------------------------------------------------
// Sends sheep home. A returned sheep runs around the player to the spot behind the camera
// (the same spot sheep spawn from), then goes back into the PlayerSheepInventory and is destroyed.
//
//   Return Held (R by default): the held sheep hops off and runs back.
//   Recall All  (Q by default): the held sheep, plus every sheep within Recall Radius that is
//                               not already returning, run back. Sheep still running to the
//                               holder are included.
//
// Why InputActionProperty instead of InputActionReference?
//   An InputActionProperty can hold EITHER an action defined right here in the Inspector, OR a
//   reference to an action in an Input Actions asset. The shared InputSystem_Actions has no
//   actions for these yet, so they start as local actions bound to R and Q. When the team adds
//   Player/ReturnSheep and Player/RecallSheep, tick "Use Reference" in the Inspector and drag
//   them in. No code changes.
//
// Not handled yet (later tasks):
//   - Returned sheep are destroyed and new ones are created on spawn. If that happens a lot,
//     object pooling (hiding and reusing sheep) would save performance.
//
// Setup:
//   - Add this to the ROOT of the Player prefab (it needs PlayerSheepCarrier, PlayerSheepSpawner and
//     PlayerSheepInventory on the same object; all three are already there).
//   - Change the keys or add gamepad bindings in the Inspector if needed.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Sends sheep back to the player's inventory. One button returns the held sheep, another
/// recalls every sheep within a wide radius. Returning sheep run around the player to the spot
/// behind the camera, and are added back to the inventory when they get there.
/// </summary>
[RequireComponent(typeof(PlayerSheepCarrier))]
[RequireComponent(typeof(PlayerSheepSpawner))]
[RequireComponent(typeof(PlayerSheepInventory))]
public class PlayerSheepRecaller : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Sends the held sheep back to the inventory. A local action on R for now; tick " +
             "Use Reference and drag in Player/ReturnSheep once the team adds it.")]
    [SerializeField]
    private InputActionProperty returnHeldAction =
        new InputActionProperty(new InputAction("Return Held Sheep", InputActionType.Button, "<Keyboard>/r"));

    [Tooltip("Sends every sheep within Recall Radius back to the inventory. A local action on Q " +
             "for now; tick Use Reference and drag in Player/RecallSheep once the team adds it.")]
    [SerializeField]
    private InputActionProperty recallAllAction =
        new InputActionProperty(new InputAction("Recall All Sheep", InputActionType.Button, "<Keyboard>/q"));

    [Header("Recall All")]
    [Tooltip("How far from the player a sheep can be and still be recalled, in metres.")]
    [SerializeField] private float recallRadius = 20f;

    // The other Player components this one works with. All cached in Awake.
    private PlayerSheepCarrier carrier;
    private PlayerSheepSpawner spawner;
    private PlayerSheepInventory inventory;

    // The last spot behind the camera that was found. If the camera is ever missing for a
    // moment, returning sheep keep heading here instead of stopping.
    private Vector3 lastReturnPoint;
    private bool hasReturnPoint;

    /// <summary>
    /// Caches the carrier, spawner and inventory on this same GameObject.
    /// </summary>
    private void Awake()
    {
        carrier = GetComponent<PlayerSheepCarrier>();
        spawner = GetComponent<PlayerSheepSpawner>();
        inventory = GetComponent<PlayerSheepInventory>();
    }

    /// <summary>
    /// Starts listening for both buttons whenever this component is enabled.
    /// </summary>
    private void OnEnable()
    {
        StartListening(returnHeldAction, HandleReturnHeldPerformed);
        StartListening(recallAllAction, HandleRecallAllPerformed);
    }

    /// <summary>
    /// Stops listening for both buttons whenever this component is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        StopListening(returnHeldAction, HandleReturnHeldPerformed);
        StopListening(recallAllAction, HandleRecallAllPerformed);
    }

    /// <summary>
    /// Sends the held sheep back to the inventory: it hops off in front of the player and runs
    /// back behind the camera. Returns true if a sheep was sent, or false if the player was not
    /// holding one or there is no camera to return behind.
    /// </summary>
    public bool TryReturnHeldSheep()
    {
        if (!carrier.IsHoldingSheep)
        {
            return false;
        }

        // Check BEFORE dropping, so a missing camera never leaves the sheep dropped but stuck.
        if (!TryRefreshReturnPoint())
        {
            Debug.LogWarning("[PlayerSheepRecaller] No camera to return sheep behind.", this);
            return false;
        }

        // Drop first, so the sheep hops clear of the player instead of starting inside them.
        if (!carrier.TryDropHeldSheep(out SheepCarriable sheep))
        {
            return false;
        }

        sheep.BeginReturn(this);
        return true;
    }

    /// <summary>
    /// Sends the held sheep and every sheep within Recall Radius back to the inventory. Sheep
    /// that are already returning are skipped. Returns how many sheep were sent.
    /// </summary>
    public int RecallAllNearbySheep()
    {
        if (!TryRefreshReturnPoint())
        {
            Debug.LogWarning("[PlayerSheepRecaller] No camera to return sheep behind.", this);
            return 0;
        }

        int recalledCount = 0;

        // The held sheep goes first, through the normal drop, so it hops clear of the player.
        if (TryReturnHeldSheep())
        {
            recalledCount++;
        }

        float sqrRadius = recallRadius * recallRadius;
        IReadOnlyList<SheepCarriable> allSheep = SheepCarriable.ActiveSheep;

        // Looping BACKWARDS is a safe habit when looping over a list that might change while we
        // loop. Nothing here removes sheep straight away, but if that ever changes, a forward
        // loop would skip a sheep each time one was removed.
        for (int i = allSheep.Count - 1; i >= 0; i--)
        {
            SheepCarriable sheep = allSheep[i];

            // Skip sheep already on their way home (including the held sheep we just sent).
            if (sheep == null || sheep.IsReturning)
            {
                continue;
            }

            if ((sheep.transform.position - transform.position).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            // BeginReturn also handles sheep that are still running to the holder.
            sheep.BeginReturn(this);
            recalledCount++;
        }

        return recalledCount;
    }

    /// <summary>
    /// Returns the spot behind the camera that returning sheep run to, worked out fresh from
    /// the camera's current position. Called by SheepCarriable every physics step while it
    /// runs back. If the spot cannot be found right now, the last known spot is used.
    /// </summary>
    public Vector3 GetReturnPoint()
    {
        TryRefreshReturnPoint();
        return lastReturnPoint;
    }

    /// <summary>
    /// Called by SheepCarriable when it reaches the spot behind the camera. Adds one sheep of
    /// its type back to the inventory and destroys the sheep.
    /// </summary>
    public void HandleSheepReturned(SheepCarriable sheep)
    {
        if (sheep == null)
        {
            return;
        }

        if (sheep.TryGetComponent(out SheepType sheepType))
        {
            // AddSheep raises SheepCountChanged, so the HUD buttons update by themselves.
            inventory.AddSheep(sheepType.SheepEnum);
        }
        else
        {
            Debug.LogWarning($"[PlayerSheepRecaller] {sheep.name} has no SheepType, so it could not be added to the inventory.", sheep);
        }

        // Destroy happens at the end of this frame, not instantly. The sheep's OnDisable then
        // removes it from the ActiveSheep list.
        Destroy(sheep.gameObject);
    }

    // Asks the spawner for the spot behind the camera and remembers it. Returns true if a spot
    // is known (found now, or found earlier).
    private bool TryRefreshReturnPoint()
    {
        // We only need the position, so the facing is thrown away with a discard (out _).
        if (spawner.TryGetSpawnPose(out Vector3 position, out _))
        {
            lastReturnPoint = position;
            hasReturnPoint = true;
        }

        return hasReturnPoint;
    }

    // Called when Return Held is pressed.
    private void HandleReturnHeldPerformed(InputAction.CallbackContext context)
    {
        TryReturnHeldSheep();
    }

    // Called when Recall All is pressed.
    private void HandleRecallAllPerformed(InputAction.CallbackContext context)
    {
        RecallAllNearbySheep();
    }

    // Subscribes to an action's "performed" event and makes sure the action is switched on.
    private static void StartListening(InputActionProperty property, Action<InputAction.CallbackContext> handler)
    {
        InputAction action = property.action;

        if (action == null)
        {
            return;
        }

        action.performed += handler;
        action.Enable();
    }

    // Unsubscribes from an action. Only switches the action off if it is our own local action.
    // A referenced action may be shared with other scripts, so we leave it on (same reason as
    // in PlayerSheepCarrier).
    private static void StopListening(InputActionProperty property, Action<InputAction.CallbackContext> handler)
    {
        InputAction action = property.action;

        if (action == null)
        {
            return;
        }

        action.performed -= handler;

        if (property.reference == null)
        {
            action.Disable();
        }
    }

    /// <summary>
    /// Draws the recall range as a yellow wire sphere in the Scene view when the Player is
    /// selected, so Recall Radius can be tuned by eye. Editor only; not seen in the game.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, recallRadius);
    }
}