using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// InventoryButton
// -------------------
// Connects one UI Button to one sheep type. It does two jobs:
//   1. When clicked, it tells the InventoryHUD which sheep type it stands for.
//   2. It shows how many of that sheep the player has, and greys itself out
//      (stops being clickable) when the count reaches 0.
//   3. It lets a keyboard key (its hotkey) press the button, exactly as if
//      the player had clicked it with the mouse.
//
// Why a script instead of the OnClick list in the Inspector?
// The Inspector's OnClick list can only pass simple values (int, float,
// string, bool, or an Object). It cannot pass an enum like SheepEnum.
//
/// <summary>
/// Links a UI Button to one sheep type. Sends its sheep type to the
/// InventoryHUD when clicked or when its hotkey is pressed, shows how many of
/// that sheep the player has, and becomes unclickable when the player has
/// none left.
/// </summary>
[RequireComponent(typeof(Button))]
public class InventoryButton : MonoBehaviour
{
    [Tooltip("The sheep type this button selects.")]
    [SerializeField] private SheepEnum sheepType = SheepEnum.Normal;

    [Tooltip("The text that shows how many of this sheep the player has.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("The HUD to notify on click. Leave empty to find it on a parent object.")]
    [SerializeField] private InventoryHUD inventoryHUD;
    
    [Tooltip("The hotkey binding.")]
    [SerializeField] private InputActionProperty hotkeyAction = new InputActionProperty(new InputAction("Sheep Hotkey", InputActionType.Button));

    // The Button component on this same GameObject, cached in Awake.
    private Button button;

    // The player's sheep inventory, taken from the HUD in Awake.
    private PlayerSheepInventory inventory;

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

        // Warn if the hotkey action is not assigned or has no binding.
        WarnIfHotkeyMissing();

        // Only search if nothing was assigned in the Inspector. GetComponentInParent
        // checks this object first, then walks up the hierarchy:
        // Button -> SheepButtonBar -> Canvas -> InventoryHUD.
        if (inventoryHUD == null)
        {
            inventoryHUD = GetComponentInParent<InventoryHUD>();
        }

        if (inventoryHUD == null)
        {
            Debug.LogWarning($"[InventoryButton] No InventoryHUD found for {name}. Assign one in the Inspector or place this button under InventoryHUD.", this);
            return;
        }

        // Reading a serialized field from another component is safe in Awake,
        // because Unity loads Inspector values before any Awake runs.
        inventory = inventoryHUD.Inventory;

        if (countText == null)
        {
            Debug.LogWarning($"[InventoryButton] Count Text is not assigned on {name}.", this);
        }
    }

    /// <summary>
    /// Starts listening for clicks and inventory changes whenever this button
    /// becomes active.
    /// </summary>
    private void OnEnable()
    {
        button.onClick.AddListener(HandleClick);
        StartListening(hotkeyAction, HandleHotkeyPerformed);

        if (inventory != null)
        {
            // Subscribing is safe, even if the inventory's Awake has not run yet.
            inventory.SheepCountChanged += HandleSheepCountChanged;
        }

        // Refresh the count immediately, so the button shows the right number
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
        // Unsubscribe from the button and inventory events to avoid memory leaks and null reference errors.
        button.onClick.RemoveListener(HandleClick);
        StopListening(hotkeyAction, HandleHotkeyPerformed);

        if (inventory != null)
        {
            inventory.SheepCountChanged -= HandleSheepCountChanged;
        }
    }

    // Called by the Button every time it is clicked.
    private void HandleClick()
    {
        if (inventoryHUD == null) return;

        inventoryHUD.SelectSheepType(sheepType);
    }

    // Called by the Input System when this button's hotkey is pressed.
    private void HandleHotkeyPerformed(InputAction.CallbackContext context)
    {
        var eventData = new BaseEventData(EventSystem.current);
        ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.submitHandler);
    }

    // Called by SheepInventory whenever ANY sheep count changes.
    private void HandleSheepCountChanged(SheepEnum changedSheepType, int newCount)
    {
        // All five buttons hear every change. Only the matching button reacts.
        if (changedSheepType != sheepType) return;

        UpdateDisplay(newCount);
    }

    // Reads this button's count straight from the inventory and shows it.
    private void RefreshCount()
    {
        if (inventory == null) return;

        UpdateDisplay(inventory.GetCount(sheepType));
    }

    // Shows the count on the label and greys the button out at 0.
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

        // A non-interactable Button ignores clicks and switches to its Disabled Color.
        button.interactable = count > 0;
    }

    // Warns if the hotkey action is not assigned or has no binding.
    private void WarnIfHotkeyMissing()
    {
        InputAction action = hotkeyAction.action;
        
        if(action == null || action.bindings.Count == 0)
        {
            Debug.LogWarning($"[InventoryButton] Hotkey action is not assigned or has no binding on {name}. Assign one in the Inspector.", this);
        }
    }

    // Starts listening for the given action, if it is assigned.
    private static void StartListening(InputActionProperty property, Action<InputAction.CallbackContext> handler)
    {
        InputAction action = property.action;
        if (action == null) return;
        
        action.performed += handler;
        action.Enable();
    }

    // Stops listening for the given action, if it is assigned.
    private static void StopListening(InputActionProperty property, Action<InputAction.CallbackContext> handler)
    {
        InputAction action = property.action;
        if (action == null) return;

        action.performed -= handler;

        if(property.reference == null)
        {
            action.Disable();
        }
    }
}