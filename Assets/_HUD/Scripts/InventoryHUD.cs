using UnityEngine;

// InventoryHUD
// ----------------
// The "brain" of the demo inventory HUD. It remembers which sheep type the
// player has selected, and it holds the reference to the player's sheep
// inventory so the buttons can find it.
//
// Why does the HUD hold the inventory reference?
// The inventory lives on the Player, which is not a parent of the buttons, so
// the buttons cannot find it with GetComponentInParent. Instead of assigning
// the Player on all five buttons, we assign it once here, and each button
// reads it from the HUD (which they already find automatically).
//
// Setup:
//   - Add this component to the InventoryHUD GameObject in the scene.
//   - Drag the scene's Player object into Sheep Inventory.
//   - The sheep buttons (InventoryButton) call SelectSheepType when clicked.

/// <summary>
/// Keeps track of which sheep type is selected in the demo inventory HUD,
/// reacts when a sheep button is clicked, and gives the buttons access to the
/// player's sheep inventory.
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    [Tooltip("The player's sheep spawner. Drag the Player object from the scene here.")]
    [SerializeField] private PlayerSheepSpawner sheepSpawner;

    // The sheep type that is currently selected. Private so that other scripts
    // cannot change it directly; they must go through SelectSheepType, which
    // means every change passes through one place we control.
    private SheepEnum selectedSheepType = SheepEnum.Normal;

    // The player's sheep inventory that this HUD displays, taken from the
    // spawner. Is null if the spawner was not assigned in the Inspector.
    public PlayerSheepInventory Inventory => sheepSpawner != null ? sheepSpawner.Inventory : null;

    // The sheep type that is currently selected. Other scripts can read this
    // value but cannot change it directly.
    public SheepEnum SelectedSheepType => selectedSheepType;

    /// <summary>
    /// Warns early if the inventory was not assigned, so the problem shows up
    /// as one clear message instead of buttons silently showing nothing.
    /// </summary>
    private void Awake()
    {
        if (sheepSpawner == null)
        {
            Debug.LogWarning("[InventoryHUD] Sheep Spawner is not assigned. " +
                             "Drag the Player object into the Sheep Spawner field.", this);
        }
    }

    /// <summary>
    /// Selects the given sheep type and writes it to the Console. Called by the
    /// sheep buttons when they are clicked.
    /// </summary>
    public void SelectSheepType(SheepEnum sheepType)
    {
        selectedSheepType = sheepType;
        SpawnSheep(selectedSheepType);
    }

    public void SpawnSheep(SheepEnum sheepType)
    {
        if (sheepSpawner == null) return;

        if (sheepSpawner.TrySpawnSheep(sheepType))
        {
            Debug.Log($"[InventoryHUD] Spawned a {sheepType} sheep.", this);
        }
    }
}