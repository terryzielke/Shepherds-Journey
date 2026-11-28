using UnityEngine;

[CreateAssetMenu(
    fileName = "New Sheep Definition",
    menuName = "NPCs/Sheep/Sheep Definition"
)]

public class SheepDefinition : ScriptableObject
{
    public enum SheepAbility        //only sheep with the specific ability in their definition will have access to their ability
    {                               //normal sheep are considered the "default"
        Normal,
        Heavy,
        Bouncy,
        Magnetic,
        Projectile
    }

    [Header("Identity and UI")]
    public string sheepName;
    public Sprite icon;
    [TextArea] public string description;
    public GameObject sheepPrefab;

    [Header("Behaviour")]
    public bool canFlee = true;         //for use in the combat scenes
    public bool canFollow = true;       //for use in general pick up


    [Header("Movement")]
    public float movementSpeed = 3f;
    public float wanderRadius = 5f;

    [Header("Abilities")]
    public SheepAbility ability;
    public float abilityForce = 10f;    //how much force is appllied to bounce OR how much is applied to projectile
    public float abilityRadius = 3f;    //how far away you can place from Shepherd

    [Header("Placement")]
    public float placementRadius = 0.5f;    //Overlap check when placing
    public LayerMask validPlacementLayers;  //Designates WHERE they can be placed! Specific floors or walls.

}
