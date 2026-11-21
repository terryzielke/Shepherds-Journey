using UnityEngine;

// SheepSpawner
// ----------------
// Spawns a sheep of a chosen type on the ground behind the camera, and uses up
// one sheep of that type from the player's SheepInventory.
//
// Why is this on the Player and not on the HUD?
// The Player owns the inventory. The HUD's job is only to show buttons and pass
// on clicks. Keeping spawning here means other things (a keyboard shortcut, a
// quest, a cutscene) can spawn sheep too, without needing the HUD.
//
// How the spawn spot is found (see TryGetSpawnPose):
//   1. Take the camera's forward direction and flatten it so it is level with
//      the ground. The orbit camera tilts down at the player, so without this,
//      "behind the camera" would point up into the sky.
//   2. Step backwards from the camera along that flat direction.
//   3. Fire a ray straight down from above that point to find the ground, so
//      the sheep stands on the terrain instead of floating at camera height.
//   4. If the ray hits nothing (for example, no ground layer was set), fall
//      back to the player's own height so the sheep still appears.
//
// Setup:
//   - Add this component to the Player object (it needs a SheepInventory on
//     the same object; Unity adds one automatically if it is missing).
//   - Drag the Main Camera into Spawn Camera, or leave it empty to use the
//     camera tagged MainCamera.
//   - Set Ground Mask to the ground or terrain layers. Leave out the Player and
//     Sheep layers so the ray does not land on top of them.
//   - Drag the Sheep prefab from the Project window into Sheep Prefab.

/// <summary>
/// Spawns sheep on the ground behind the camera. Each spawn uses up one sheep
/// of that type from the player's SheepInventory, and nothing spawns if the
/// player has none left.
/// </summary>
[RequireComponent(typeof(SheepInventory))]
[RequireComponent(typeof(SheepCarrier))]
public class SheepSpawner : MonoBehaviour
{
    [Header("Spawn Position")]
    [Tooltip("The camera that sheep spawn behind. Leave empty to use the camera tagged MainCamera.")]
    [SerializeField] private Camera spawnCamera;

    [Tooltip("How far behind the camera sheep appear, in metres, measured along the ground.")]
    [SerializeField] private float distanceBehindCamera = 2f;

    [Tooltip("Layers that count as ground. Leave out the Player and Sheep layers.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("How far above the spawn point the downward ray starts, in metres. Starting above means the ray still finds the ground on hills.")]
    [SerializeField] private float raycastStartHeight = 10f;

    [Tooltip("How far the downward ray travels, in metres, before giving up.")]
    [SerializeField] private float raycastLength = 50f;

    [Header("Sheep")]
    [Tooltip("The Sheep prefab to spawn. Its type is set after it is created.")]
    [SerializeField] private SheepSelectType sheepPrefab;

    private SheepInventory inventory;
    private SheepCarrier carrier;

    /// <summary>
    /// The player's sheep inventory, on the same GameObject as this spawner.
    /// Safe to read at any time, even from another script's Awake.
    /// </summary>
    public SheepInventory Inventory
    {
        get
        {
            // Lazy lookup: we find the inventory the first time someone asks, instead of in our own Awake.
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
        carrier = GetComponent<SheepCarrier>();

        if (spawnCamera == null)
        {
            spawnCamera = Camera.main;
        }

        if (spawnCamera == null)
        {
            Debug.LogWarning("[SheepSpawner] Spawn Camera is not assigned. Drag the Player's SheepSpawner child into it.", this);
        }

        if (sheepPrefab == null)
        {
            Debug.LogWarning("[SheepSpawner] Sheep Prefab is not assigned. Drag the Sheep prefab into it.", this);
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
        if (sheepPrefab == null)
        {
            return false;
        }

        if(!TryGetSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            return false;
        }

        if (!Inventory.TryRemoveSheep(sheepType))
        {
            return false;
        }

        // Create the sheep
        SheepSelectType newSheep = Instantiate(sheepPrefab, spawnPosition, spawnRotation);

        // Turn this copy into the chosen type
        newSheep.SetSheepType(sheepType);

        // Rename the sheep so it is clear in the Hierarchy what type it is
        newSheep.name = $"{sheepType} Sheep";

        // Hand the new sheep to the carrier, which sends it running to the SheepHolder.
        // Instantiate has already run the sheep's Awake, so it is ready for this call.
        // TryGetComponent returns false instead of throwing if the prefab has no
        // SheepCarriable, so a setup mistake gives a warning and the sheep still spawns.
        if(newSheep.TryGetComponent(out SheepCarriable carriable))
        {
            carrier.Carry(carriable);
        }
        else
        {
            Debug.LogWarning($"[SheepSpawner] The sheep prefab {sheepPrefab.name} has no SheepCarriable component. Drag the SheepCarriable script onto the prefab so it can be carried.", newSheep);
        }

        return true;
    }

    // Works out the position and facing for a new sheep: on the ground, a set
    // distance behind the camera, facing the same way the camera faces.
    // Returns false only if there is no camera to spawn behind.
    private bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        // An "out" parameter must always be given a value before the method returns.
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if(spawnCamera == null)
        {
            return false;
        }

        Transform cameraTransform = spawnCamera.transform;

        // Flatten the camera's forward direction. Not normalizing it because depending on the camera's tilt,
        // the flat forward vector can be shorter than 1.0, and we want to step back that exact distance along the ground.
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);

        if(flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
        }

        // Now we normalize it, so we can step back a set distance along the ground.
        flatForward.Normalize();

        Vector3 pointBehindCamera = cameraTransform.position - flatForward * distanceBehindCamera;

        // Find the ground.
        Vector3 rayStart = pointBehindCamera + Vector3.up * raycastStartHeight;

        if(Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point;
        }
        else
        {
            // If the ray hits nothing, fall back to the player's own height so the sheep still appears.
            position = new Vector3(pointBehindCamera.x, transform.position.y, pointBehindCamera.z);
        }

        rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        return true;
    }
}