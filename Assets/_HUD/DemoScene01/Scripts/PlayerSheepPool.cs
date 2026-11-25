using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// ---------------------------------------------------------------------------------------------
// SheepPool
// ---------------------------------------------------------------------------------------------
// Setup:
//   - Add this to the ROOT of the Player prefab (next to PlayerSheepSpawner).
//   - Drag the Sheep prefab from the Project window into Sheep Prefab.

/// <summary>
/// Hands out sheep and takes them back, reusing hidden sheep instead of creating and destroying
/// them. Use Get in place of Instantiate and Release in place of Destroy.
/// </summary>
public class PlayerSheepPool : MonoBehaviour
{
    [Tooltip("The Sheep prefab the pool makes copies of.")]
    [SerializeField] private SheepType sheepPrefab;

    [Tooltip("How many sheep to create at the start. The pool can grow if needed.")]
    [SerializeField] private int prewarmCount = 5;

    [Tooltip("How many sheep to keep hidden when they are not in use. The pool can shrink if needed.")]
    [SerializeField] private int maxHiddenCount = 30;

    // The pool that creates, reuses, and destroys sheep.
    private ObjectPool<SheepType> pool;

    // Empty game object that holds all the hidden sheep.
    private Transform container;

    // True if the pool has been initialized.
    public bool IsReady => pool != null;

    /// <summary>
    /// Creates the pool and fills it with a few hidden sheep.
    /// </summary>
    private void Awake()
    {
        if (sheepPrefab == null)
        {
            Debug.LogWarning("[PlayerSheepPool] Sheep Prefab is not assigned. Drag the Sheep prefab into the field.", this);
            return;
        }

        container = new GameObject("Sheep Pool").transform;

        pool = new ObjectPool<SheepType>(
            createFunc: CreateSheep,
            actionOnGet: null,
            actionOnRelease: HandleRelease,
            actionOnDestroy: HandleDestroy,
            collectionCheck: true,
            defaultCapacity: prewarmCount,
            maxSize: maxHiddenCount);

        Prewarm();
    }

    /// <summary>
    /// Destroys the hidden sheep and their container when the Player is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        // Clear call destroys all the hidden sheep, and then we destroy the container itself.
        pool?.Clear();

        if (container != null)
        {
            Destroy(container.gameObject);
        }
    }

    /// <summary>
    /// Takes a sheep from the pool, or makes a new one if none are hidden.
    /// </summary>
    public SheepType Get(Vector3 position, Quaternion rotation)
    {
        if (pool == null) return null;

        SheepType sheep = pool.Get();
        Transform sheepTransform = sheep.transform;

        // Move into position before making it visible.
        sheepTransform.SetParent(null);
        sheepTransform.SetPositionAndRotation(position, rotation);

        // Make it visible and active in the scene.
        sheep.gameObject.SetActive(true);

        // Clear the sheep's velocity so it does not keep moving from its last use.
        if(sheep.TryGetComponent(out Rigidbody body) && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        return sheep;
    }

    /// <summary>
    /// Hides the given sheep and keeps it for reuse.
    /// </summary>
    public void Release(SheepType sheep)
    {
        if (sheep == null) return;

        if (pool == null)
        {
            Destroy(sheep.gameObject);
            return;
        }
        pool.Release(sheep);
    }

    /// <summary>
    /// Create the starting sheep.
    /// </summary>
    private void Prewarm()
    {
        int count = Mathf.Min(prewarmCount, maxHiddenCount);
        List<SheepType> created = new List<SheepType>(count);

        for (int i = 0; i < count; i++)
        {
            created.Add(pool.Get());
        }

        foreach(SheepType sheep in created)
        {
            pool.Release(sheep);
        }
    }

    /// <summary>
    /// Create Sheep function. Called by the pool when it is empty and someone asks for a sheep.
    /// </summary>
    private SheepType CreateSheep()
    {
        SheepType sheep = Instantiate(sheepPrefab, container);
        sheep.gameObject.SetActive(false);
        return sheep;
    }

    /// <summary>
    /// Called by the pool when a sheep comes back.
    /// </summary>
    private void HandleRelease(SheepType sheep)
    {
        sheep.gameObject.SetActive(false);
        sheep.transform.SetParent(container);
    }

    /// <summary>
    /// Called by the pool when a sheep is destroyed.
    /// </summary>
    private void HandleDestroy(SheepType sheep)
    {
        if(sheep != null)
        {
            Destroy(sheep.gameObject);
        }
    }
}