using System;
using UnityEngine;

// SheepInventory
// ------------------
// Keeps track of how many of each sheep type the player is carrying.
//
// How the counts are stored:
// One int per sheep type, kept in an array. The enum value is used as the
// array index, because every SheepTypes value is a number underneath:
//
//     sheepCounts[0] -> Normal
//     sheepCounts[1] -> Heavy
//     sheepCounts[2] -> Bouncy
//     sheepCounts[3] -> Tall
//     sheepCounts[4] -> Projectile
//
// The array size is read from the enum itself, so adding a new sheep type to
// SheepTypes automatically gives it a slot here.
//
// Listening for changes (example for another script, such as a HUD):
//
//     [SerializeField] private SheepInventory inventory;
//
//     private void OnEnable()  { inventory.SheepCountChanged += HandleSheepCountChanged; }
//     private void OnDisable() { inventory.SheepCountChanged -= HandleSheepCountChanged; }
//
//     private void HandleSheepCountChanged(SheepTypes sheepType, int newCount)
//     {
//         // Update the number shown for this sheep type.
//     }
//
// Setup:
//   - Add this component to the Player object.
//   - Change Starting Count in the Inspector if you want a different default.

/// <summary>
/// Stores how many of each sheep type the player has. The player starts with
/// a set number of every type (5 by default). Other scripts can read, add,
/// and remove sheep through the public methods, and can listen to the
/// SheepCountChanged event to find out when a count changes.
/// </summary>
public class SheepInventory : MonoBehaviour
{
    [Tooltip("How many of each sheep type the player starts with.")]
    // Min(0) stops the Inspector from accepting a negative starting count.
    [Min(0)]
    [SerializeField] private int startingCount = 3;

    /// <summary>
    /// Raised whenever the count of a sheep type changes. Listeners receive the
    /// sheep type that changed and its new count.
    /// </summary>
    public event Action<SheepTypes, int> SheepCountChanged;

    // One count per sheep type, indexed by the enum's number.
    // Private so the only way to change a count is through the methods below,
    // which keep counts valid and always raise SheepCountChanged.
    private int[] sheepCounts;

    /// <summary>
    /// Creates the count array and fills every slot with the starting count.
    /// Runs in Awake so the inventory is ready before any other script's Start
    /// tries to read from it.
    /// </summary>
    private void Awake()
    {
        // Enum.GetValues returns every value in SheepTypes, so Length is
        // the number of sheep types (5 right now).
        int sheepTypeCount = Enum.GetValues(typeof(SheepTypes)).Length;
        sheepCounts = new int[sheepTypeCount];

        for (int i = 0; i < sheepCounts.Length; i++)
        {
            sheepCounts[i] = startingCount;
        }

        // We do not raise SheepCountChanged here. Other scripts usually
        // subscribe in their own OnEnable/Start, which may run after this
        // Awake, so nobody would hear it. Listeners should call GetCount once
        // when they start to read the initial values.
    }

    /// <summary>
    /// Returns how many sheep of the given type the player has.
    /// </summary>
    public int GetCount(SheepTypes sheepType)
    {
        return sheepCounts[ToIndex(sheepType)];
    }

    /// <summary>
    /// Returns true if the player has at least one sheep of the given type.
    /// </summary>
    public bool HasSheep(SheepTypes sheepType)
    {
        return GetCount(sheepType) > 0;
    }

    /// <summary>
    /// Adds the given number of sheep of one type to the inventory. The amount
    /// must be greater than zero; to take sheep away, use TryRemoveSheep.
    /// </summary>
    public void AddSheep(SheepTypes sheepType, int amount = 1)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[SheepInventory] AddSheep needs a positive amount, got {amount}.", this);
            return;
        }

        SetCount(sheepType, GetCount(sheepType) + amount);
    }

    /// <summary>
    /// Removes the given number of sheep of one type, but only if the player
    /// has enough. Returns true if the sheep were removed, or false (and
    /// changes nothing) if there were not enough.
    /// </summary>
    public bool TryRemoveSheep(SheepTypes sheepType, int amount = 1)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[SheepInventory] TryRemoveSheep needs a positive amount, got {amount}.", this);
            return false;
        }

        int currentCount = GetCount(sheepType);

        // Refuse instead of going below zero. Returning false lets the caller
        // decide what to do (for example, show "out of sheep" in the HUD).
        if (currentCount < amount)
        {
            Debug.Log($"[SheepInventory] Not enough {sheepType} sheep: have {currentCount}, need {amount}.", this);
            return false;
        }

        SetCount(sheepType, currentCount - amount);
        return true;
    }

    // The single place where a count actually changes. Both AddSheep and
    // TryRemoveSheep go through here, so the log and the event can never be
    // forgotten in one of them.
    private void SetCount(SheepTypes sheepType, int newCount)
    {
        sheepCounts[ToIndex(sheepType)] = newCount;

        Debug.Log($"[SheepInventory] {sheepType} sheep: {newCount}", this);

        // The ?. means "only call Invoke if at least one script is listening".
        // Without it, raising an event with no listeners would throw a
        // NullReferenceException.
        SheepCountChanged?.Invoke(sheepType, newCount);
    }

    // Turns a sheep type into its array index. Normally that is just the
    // enum's number, but C# allows casting any int to an enum, for example
    // (SheepTypes)99. This check turns that mistake into a clear error
    // message instead of a confusing IndexOutOfRangeException.
    private int ToIndex(SheepTypes sheepType)
    {
        int index = (int)sheepType;

        if (index < 0 || index >= sheepCounts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(sheepType), sheepType,
                "Unknown sheep type. Was a value added to SheepTypes without a number?");
        }

        return index;
    }

    // Temporary test: right-click the component header in the Inspector
    // during Play mode and choose "Test Remove One Heavy".
    [ContextMenu("Test Remove One Heavy")]
    private void TestRemoveOneHeavy()
    {
        TryRemoveSheep(SheepTypes.Heavy);
    }
}