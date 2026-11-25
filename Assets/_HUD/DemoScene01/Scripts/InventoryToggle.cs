using System;
using UnityEngine;
using UnityEngine.InputSystem;

// InventoryHUDToggle
// -------------------
// Setup:
//   - Add a CanvasGroup component to the Canvas under InventoryHUD.
//   - Add this component to the InventoryHUD GameObject.
//   - Canvas Group can be left empty; it is found on a child automatically.
//   - The HUD is hidden when the game starts, so you can leave it visible in
//     the editor while you work on the layout.

/// <summary>
/// Shows the inventory HUD while the show key (Alt by default) is held and
/// hides it when the key is released. Hides the HUD by fading and blocking
/// the mouse, not by turning objects off, so the sheep button hotkeys still
/// work while the HUD is hidden.
/// </summary>
public class InventoryToggle : MonoBehaviour
{
    [Tooltip("Hold to show the inventory HUD.")]
    [SerializeField] private InputActionProperty showAction = new InputActionProperty(new InputAction("Show Inventory", InputActionType.Button, "<Keyboard>/alt"));

    [Tooltip("The CanvasGroup that controls the HUD's visibility and mouse blocking.")]
    [SerializeField] private CanvasGroup canvasGroup;

    public bool IsVisible { get; private set; }

    /// <summary>
    /// Caches the CanvasGroup reference when needed and initializes the inventory UI as hidden.
    /// </summary>
    private void Awake()
    {
        if (canvasGroup == null)
        {
            // True means "include inactive children" so we can find the CanvasGroup even if it is hidden at the start.
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (canvasGroup == null)
        {
            Debug.LogWarning("[InventoryToggle] Canvas Group is not assigned and could not be found on a child. Drag the Canvas Group into the field.", this);
            return;
        }

        SetVisible(false);
    }

    /// <summary>
    /// Starts listening for the show key being pressed and released whenever this component becomes active.
    /// </summary>
    private void OnEnable()
    {
        InputAction action = showAction.action;

        if (action == null) return;

        // Started, Performed, and Canceled are key actions for a button. We only care about Performed and Canceled, which are pressed and released.
        action.performed += HandleShowPerformed;
        action.canceled += HandleShowCanceled;
        action.Enable();

        // Check for the key current state.
        SetVisible(action.IsPressed());
    }

    /// <summary>
    /// Stip listening for the show key when this component becomes inactive.
    /// </summary> 
    private void OnDisable()
    {
        InputAction action = showAction.action;

        if (action == null) return;

        action.performed -= HandleShowPerformed;
        action.canceled -= HandleShowCanceled;

        if(showAction.reference == null)
        {
            action.Disable();
        }
    }

    /// <summary>
    /// Show the HUD when the show key is pressed.
    /// </summary>
    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;

        // Set every time in case it was unticked in the Inspector. The HUD should always block the mouse when visible.
        canvasGroup.interactable = true;
    }

    /// <summary>
    /// Called when the show key is pressed. Shows the HUD.
    /// </summary>
    private void HandleShowPerformed(InputAction.CallbackContext context)
    {
        SetVisible(true);
    }

    /// <summary>
    /// Called when the show key is released. Hides the HUD.
    /// </summary>
    private void HandleShowCanceled(InputAction.CallbackContext context)
    {
        SetVisible(false);
    }
}