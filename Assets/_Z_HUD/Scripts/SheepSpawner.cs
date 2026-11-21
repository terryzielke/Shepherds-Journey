using UnityEngine;

// SheepSpawner
// ----------------
// Spawns a sheep of a chosen type at the player's spawn point, and uses up one
// sheep of that type from the player's SheepInventory.
//
// Why is this on the Player and not on the HUD?
// The Player owns both the spawn point and the inventory. The HUD's job is
// only to show buttons and pass on clicks. Keeping spawning here means other
// things (a keyboard shortcut, a quest, a cutscene) can spawn sheep too,
// without needing the HUD.
//
// Setup:
//   - Add this component to the Player object (it needs a SheepInventory on
//     the same object; Unity adds one automatically if it is missing).
//   - Drag the Player's SheepSpawner child into Spawn Point.
//   - Drag the Sheep prefab from the Project window into Sheep Prefab.

/// <summary>
/// Spawns sheep at the player's spawn point. Each spawn uses up one sheep of
/// that type from the player's SheepInventory, and nothing spawns if the
/// player has none left.
/// </summary>
[RequireComponent(typeof(SheepInventory))]
public class SheepSpawner : MonoBehaviour
{
    [Tooltip("Where new sheep appear. Drag the Player's SheepSpawner child here.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("The Sheep prefab to spawn. Its type is set after it is created.")]
    [SerializeField] private SheepSelectType sheepPrefab;

    // The inventory on this same GameObject. Found on first use (see Inventory).
    private SheepInventory inventory;

    /// <summary>
    /// The player's sheep inventory, on the same GameObject as this spawner.
    /// Safe to read at any time, even from another script's Awake.
    /// </summary>
    public SheepInventory Inventory
    {
        get
        {
            // "Lazy" lookup: we find the inventory the first time someone asks,
            // instead of in our own Awake. Unity does not promise which
            // object's Awake runs first, and the HUD buttons read this property
            // in THEIR Awake. If we filled it in our Awake, a button could get
            // null. GetComponent works before Awake, so this is always safe.
            if (inventory == null)
            {
                inventory = GetComponent<SheepInventory>();
            }

            return inventory;
        }
    }

    /// <summary>
    /// Warns early about missing Inspector references, so setup mistakes show
    /// up as one clear message at the start instead of on the first click.
    /// </summary>
    private void Awake()
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning("[SheepSpawner] Spawn Point is not assigned. " +
                             "Drag the Player's SheepSpawner child into it.", this);
        }

        if (sheepPrefab == null)
        {
            Debug.LogWarning("[SheepSpawner] Sheep Prefab is not assigned. " +
                             "Drag the Sheep prefab into it.", this);
        }
    }

    /// <summary>
    /// Tries to spawn one sheep of the given type at the spawn point. Returns
    /// true if a sheep was spawned and one was taken from the inventory.
    /// Returns false, and changes nothing, if the spawner is not set up or the
    /// player has none of that type left.
    /// </summary>
    public bool TrySpawnSheep(SheepTypes sheepType)
    {
        if (spawnPoint == null || sheepPrefab == null)
        {
            return false;
        }

        if (!Inventory.TryRemoveSheep(sheepType))
        {
            return false;
        }

        // Create the sheep
        SheepSelectType newSheep = Instantiate(sheepPrefab, spawnPoint.position, spawnPoint.rotation);

        // Turn this copy into the chosen type
        newSheep.SetSheepType(sheepType);

        // Rename the sheep so it is clear in the Hierarchy what type it is
        newSheep.name = $"Sheep ({sheepType})";

        return true;
    }
}