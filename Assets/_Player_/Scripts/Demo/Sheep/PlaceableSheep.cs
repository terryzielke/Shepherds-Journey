using UnityEngine;



/// <summary>
/// Sabine's component can read this interface and answer with her own
/// </summary>
public interface ISheepSurfaceRules
{
    bool AllowsSurface(PlaceableSheep sheep, RaycastHit hit);
}



[DisallowMultipleComponent]

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]



public sealed class PlaceableSheep : MonoBehaviour
{

    public enum PlacementState
    {
        Available, 
        Held, 
        Placed
    }

    // Sabine's component that uses ISheepSurfaceRules will be dragged into this slot.
    // For the demo, there is a demo inventory that supplies the rule.
    [SerializeField] private MonoBehaviour surfaceRuleSource;


    // OPTIONAL! Only needed if there is an animation for when the sheep is placed.
    [SerializeField] private Animator visualAnimator;
    private static readonly int PlacedParameter = Animator.StringToHash("Placed");


    private Rigidbody body;
    private BoxCollider bodyCollider;
    private ISheepSurfaceRules surfaceRules;


    // State is independent of the collider reference. A placed sheep
    // stays Placed even if the puzzle surface gets destroyed.
    public PlacementState State { get; private set; } = PlacementState.Available;
    public bool IsHeld { get; private set; }
    public Collider PlacedSurface { get; private set; }


    // This checks if the sheep fits
    internal Vector3 LocalBoxCenter { get; private set; }
    internal Vector3 BoxHalfSize { get; private set; }



    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<BoxCollider>();

        // Dimensions are based off the sheeps pivot point, so for the love of god
        // put the pivot in the middle.
        LocalBoxCenter = bodyCollider.center;
        BoxHalfSize = bodyCollider.size * 0.5f;

        if (surfaceRuleSource == null)
            return;

        // Cast checks against Mr.Unitys 'component refrence', and makes sure that component
        // has the data to answer the placement questions. 
        surfaceRules = surfaceRuleSource as ISheepSurfaceRules;

        if (surfaceRules == null)
        {
            Debug.Log("Surface Rules Source must use ISheepSurfaceRules, i.e. drag and drop baby.", this);
            enabled = false;
        }
    }



    /// <summary>Attaches the sheep to the player's carry socket.</summary>
    /// <param name="socket">The player's carry point.</param>
    /// <param name="fallbackRules">Backup surface rules.</param>
    /// <returns>True if successfully attached.</returns>
    internal bool TryAttach(Transform socket, ISheepSurfaceRules fallbackRules)
    {
        if (!isActiveAndEnabled || IsHeld)
            return false;

        // It will always want to use the rule from Sabine's prefab first, but whilst testing
        // this is a fallback. It also keeps the chosen rule with this instance for future
        // pickups.
        ISheepSurfaceRules rules = surfaceRules ?? fallbackRules;
        if (rules == null)
            return false;

        surfaceRules = rules;

        // If the sheep is being held, it's position and rotation is being saved in the pose
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None;
        bodyCollider.enabled = false;

        State = PlacementState.Held;
        PlacedSurface = null;

        // Makes the player the sheeps daddy, so the sheep follows the players walk/jump.
        // Local 0 puts its pivot right on the carry socket (where the player grabs)
        transform.SetParent(socket, true);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        SetPlacedAnimation(false);
        return true;
    }



    /// <summary>Checks if the sheep can be placed on a surface.</summary>
    /// <param name="hit">The detected surface.</param>
    /// <returns>True if placement is allowed.</returns>
    internal bool AllowsSurface(RaycastHit hit)
    {
        return IsRuleAvailable(surfaceRules) && surfaceRules.AllowsSurface(this, hit);
    }



    /// <summary>Places the sheep and re-enables its collider.</summary>
    /// <param name="pose">The placement position and rotation.</param>
    /// <param name="surface">The collider the sheep is placed on.</param>
    internal void Place(Pose pose, Collider surface)
    {
        // Pose holds a postion and rotation
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(pose.position, pose.rotation);

        State = PlacementState.Placed;
        PlacedSurface = surface;

        // Sheepy stays kinimatic, the collider is solid again so it can
        // be clicked, or a puzzle can use it as a 'platform'.
        bodyCollider.enabled = true;
        SetPlacedAnimation(true);
    }



    private static bool IsRuleAvailable(ISheepSurfaceRules rules)
    {
        // Interface reference can outlive its unity component, so it 
        // compares object type so destoryed providers fail safley
        if (rules is Object unityObject)
            return unityObject != null;

        return rules != null;
    }


    /// <summary>Updates the sheep's placement animation.</summary>
    /// <param name="placed">Whether the sheep is placed.</param>
    private void SetPlacedAnimation(bool placed)
    {
        // Animates the child so sheep can animate "idle" and leaves the root
        // and collider in the exact same placement position. 
        if (visualAnimator != null)
            visualAnimator.SetBool(PlacedParameter, placed);
    }
}
