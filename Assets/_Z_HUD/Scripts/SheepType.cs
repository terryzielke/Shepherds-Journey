using UnityEngine;

public class SheepType : MonoBehaviour
{
    [Tooltip("What kind of sheep this is. Used by the inventory and HUD.")]
    // SerializeField lets Unity show and save this private field in the
    // Inspector. Because it is an enum, Unity draws it as a dropdown.
    [SerializeField] private SheepEnum sheepType = SheepEnum.Normal;

    /// <summary>
    /// The sheep type of this sheep, as chosen in the Inspector. Other scripts
    /// can read it, but cannot change it.
    /// </summary>
    public SheepEnum SheepEnum => sheepType;

    /// <summary>
    /// Changes this sheep to the given sheep type. The spawner calls this right
    /// after creating a sheep from the prefab.
    /// </summary>
    public void SetSheepEnum(SheepEnum newSheepEnum)
    {
        sheepType = newSheepEnum;
    }
}
