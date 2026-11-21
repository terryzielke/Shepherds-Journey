using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SheepTypeButton
// -------------------
// Connects one UI Button to one sheep type. It does two jobs:
//   1. When clicked, it tells the InventoryHUD which sheep type it stands for.
//   2. It shows how many of that sheep the player has, and greys itself out
//      (stops being clickable) when the count reaches 0.
//
// Why a script instead of the OnClick list in the Inspector?
// The Inspector's OnClick list can only pass simple values (int, float,
// string, bool, or an Object). It cannot pass an enum like SheepTypes.
//
// How the count stays up to date:
// The button subscribes to SheepInventroy.SheepCountChanged. The event
// fires for every sheep type, so each button checks whether the change is for
// its own type and ignores the rest. The event only fires on a change, so the
// button also reads its starting count once in Start.
//
// Setup:
//   1. Add this component to a Button GameObject.
//   2. Pick the button's sheep type from the Sheep Type dropdown.
//   3. Drag the button's CountText child into Count Text.
//   4. (Optional) Drag the InventoryHUD object into Inventory HUD. If you leave
//      it empty, the script finds the InventoryHUD on a parent object.
//   5. Do NOT also add an entry to the Button's OnClick list, or clicks run twice.

/// <summary>
/// Links a UI Button to one sheep type. Sends its sheep type to the
/// InventoryHUD when clicked, shows how many of that sheep the player
/// has, and becomes unclickable when the player has none left.
/// </summary>
// RequireComponent guarantees a Button is on this GameObject, so
// GetComponent<Button>() below never returns null.
[RequireComponent(typeof(Button))]
public class SheepTypeButton : MonoBehaviour
{
    [Tooltip("The sheep type this button selects.")]
    [SerializeField] private SheepTypes sheepType = SheepTypes.Normal;

    [Tooltip("The text that shows how many of this sheep the player has.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("The HUD to notify on click. Leave empty to find it on a parent object.")]
    [SerializeField] private InventoryHUD inventoryHUD;

    // The Button component on this same GameObject, cached in Awake.
    private Button button;

    // The player's sheep inventory, taken from the HUD in Awake.
    private SheepInventroy inventory;

    // Becomes true once Start has run. Used by OnEnable to know whether it is
    // safe to read the inventory yet (see the timing notes in OnEnable).
    private bool hasStarted;

    /// <summary>
    /// Caches the Button, finds the HUD if it was not assigned, and gets the
    /// inventory from the HUD. Awake runs once, before OnEnable.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();

        // Only search if nothing was assigned in the Inspector. GetComponentInParent
        // checks this object first, then walks up the hierarchy:
        // Button -> SheepButtonBar -> Canvas -> InventoryHUD.
        if (inventoryHUD == null)
        {
            inventoryHUD = GetComponentInParent<InventoryHUD>();
        }

        if (inventoryHUD == null)
        {
            Debug.LogWarning($"[SheepTypeButton] No InventoryHUD found for {name}. " +
                             "Assign one in the Inspector or place this button under InventoryHUD.", this);
            return;
        }

        // Reading a serialized field from another component is safe in Awake,
        // because Unity loads Inspector values before any Awake runs.
        inventory = inventoryHUD.Inventory;

        if (countText == null)
        {
            Debug.LogWarning($"[SheepTypeButton] Count Text is not assigned on {name}.", this);
        }
    }

    /// <summary>
    /// Starts listening for clicks and inventory changes whenever this button
    /// becomes active.
    /// </summary>
    private void OnEnable()
    {
        button.onClick.AddListener(HandleClick);

        if (inventory != null)
        {
            // Subscribing is always safe, even if the inventory's Awake has
            // not run yet. We are only adding our method to its list.
            inventory.SheepCountChanged += HandleSheepCountChanged;
        }

        // TIMING TRAP: when the scene first loads, this OnEnable can run BEFORE
        // the inventory's Awake has created its count array, so calling
        // GetCount here would crash. On that first load we wait for Start.
        // But if the button is turned off and on again later (Start does not
        // run a second time), counts may have changed while it was off and no
        // events reached it. So after the first Start, OnEnable refreshes.
        if (hasStarted)
        {
            RefreshCount();
        }
    }

    /// <summary>
    /// Shows the starting count. Unity runs every Awake in the scene before
    /// any Start, so the inventory is guaranteed to be ready here.
    /// </summary>
    private void Start()
    {
        hasStarted = true;
        RefreshCount();
    }

    /// <summary>
    /// Stops listening for clicks and inventory changes whenever this button
    /// is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        // Every subscribe needs a matching unsubscribe. Without these, a
        // disabled or destroyed button would still be on the lists and could
        // be called again, causing double clicks or errors.
        button.onClick.RemoveListener(HandleClick);

        if (inventory != null)
        {
            inventory.SheepCountChanged -= HandleSheepCountChanged;
        }
    }

    // Called by the Button every time it is clicked.
    private void HandleClick()
    {
        if (inventoryHUD == null)
        {
            // Already warned in Awake; skip quietly so we do not spam the Console.
            return;
        }

        inventoryHUD.SelectSheepType(sheepType);
    }

    // Called by SheepInventroy whenever ANY sheep count changes.
    // The parameters must match the event's type: Action<SheepTypes, int>.
    private void HandleSheepCountChanged(SheepTypes changedSheepType, int newCount)
    {
        // All five buttons hear every change. Only the matching button reacts.
        if (changedSheepType != sheepType)
        {
            return;
        }

        UpdateDisplay(newCount);
    }

    // Reads this button's count straight from the inventory and shows it.
    private void RefreshCount()
    {
        if (inventory == null)
        {
            return;
        }

        UpdateDisplay(inventory.GetCount(sheepType));
    }

    // Shows the count on the label and greys the button out at 0.
    // Kept in one method so the event path and the refresh path always
    // display the count the same way.
    private void UpdateDisplay(int count)
    {
        if (countText != null)
        {
            // SetText with a "{0}" placeholder is a TextMeshPro performance
            // trick: it writes the number straight into the text without
            // creating a new string each time, so it makes no garbage for
            // the garbage collector to clean up later.
            countText.SetText("{0}", count);
        }

        // A non-interactable Button ignores clicks and switches to its
        // Disabled Color (set in the Button's Inspector), which is what
        // makes it look greyed out.
        button.interactable = count > 0;
    }
}