using UnityEngine;

// PlayerSheepSpawner
// ----------------
// Setup:
//   - Add this component to the Player object (it needs a PlayerSheepInventory on
//     the same object; Unity adds one automatically if it is missing).
//   - Drag the Main Camera into Spawn Camera, or leave it empty to use the
//     camera tagged MainCamera.
//   - Set Ground Mask to the ground or terrain layers. Leave out the Player and
//     Sheep layers so the ray does not land on top of them.
//   - The Sheep prefab is set on the SheepPool component (also on the Player). Sheep are taken
//     from that pool instead of being created with Instantiate, so they can be reused.

/// <summary>
/// Spawns sheep on the ground behind the camera. Each spawn uses up one sheep
/// of that type from the player's PlayerSheepInventory, and nothing spawns if the
/// player has none left.
/// </summary>
[RequireComponent(typeof(PlayerSheepInventory))]
[RequireComponent(typeof(PlayerSheepCarrier))]
[RequireComponent(typeof(PlayerSheepPool))]
public class PlayerSheepSpawner : MonoBehaviour
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

    private PlayerSheepInventory inventory;
    private PlayerSheepCarrier carrier;
    private PlayerSheepPool sheepPool;

    /// <summary>
    /// The player's sheep inventory, on the same GameObject as this spawner.
    /// Safe to read at any time, even from another script's Awake.
    /// </summary>
    public PlayerSheepInventory Inventory
    {
        get
        {
            // Lazy lookup: we find the inventory the first time someone asks, instead of in our own Awake.
            if (inventory == null)
            {
                inventory = GetComponent<PlayerSheepInventory>();
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
        carrier = GetComponent<PlayerSheepCarrier>();
        sheepPool = GetComponent<PlayerSheepPool>();

        if (spawnCamera == null)
        {
            spawnCamera = Camera.main;
        }

        if (spawnCamera == null)
        {
            Debug.LogWarning("[PlayerSheepSpawner] Spawn Camera is not assigned. Drag the Player's PlayerSheepSpawner child into it.", this);
        }
    }

    /// <summary>
    /// Tries to spawn one sheep of the given type at the spawn point. Returns
    /// true if a sheep was spawned and one was taken from the inventory.
    /// Returns false, and changes nothing, if the spawner is not set up or the
    /// player has none of that type left.
    /// </summary>
    public bool TrySpawnSheep(SheepEnum SheepEnum)
    {

        if (!sheepPool.IsReady)
        {
            return false;
        }

        if(!TryGetSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            return false;
        }

        if (!Inventory.TryRemoveSheep(SheepEnum))
        {
            return false;
        }
        // Take a sheep from the pool instead of creating a new one with Instantiate, so it can be reused.
        SheepType newSheep = sheepPool.Get(spawnPosition, spawnRotation);

        // Turn this copy into the chosen type
        newSheep.SetSheepEnum(SheepEnum);

        // Rename the sheep so it is clear in the Hierarchy what type it is
        newSheep.name = $"{SheepEnum} Sheep";

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
            Debug.LogWarning($"[PlayerSheepSpawner] The sheep prefab {newSheep.name} has no SheepCarriable component. Drag the SheepCarriable script onto the prefab so it can be carried.", newSheep);
        }

        return true;
    }

    /// <summary>
    /// Works out the spot behind the camera: on the ground, a set distance behind the camera,
    /// facing the way the camera faces. New sheep spawn here, and PlayerSheepRecaller sends returning
    /// sheep back here, so spawning and returning always agree on the spot. Gives back the
    /// position and facing, and returns false only if there is no camera.
    /// </summary>
    public bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation)
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