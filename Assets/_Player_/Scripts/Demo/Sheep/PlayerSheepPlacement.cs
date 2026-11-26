using System;
using UnityEngine;



/// <summary>Defines whether a target can accept sheep placement.</summary>
/// <param name="sheep">The sheep being placed.</param>
/// <param name="hit">The detected placement surface.</param>
/// <returns>True if the target accepts the sheep.</returns>
public interface ISheepPlacementTarget
{
    bool CanAcceptSheep(PlaceableSheep sheep, RaycastHit hit);
}



[DisallowMultipleComponent]



public sealed class PlayerSheepPlacement : MonoBehaviour
{
    [Header("refrences")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform carrySocket;
    [SerializeField] private Transform reachOrigin;
    [SerializeField] private LineRenderer arc;


    [Header("Layers")]
    // Aim layers block the curson ray and the reach...placement laters support
    // sheep...clearance layers block the sheep.
    [SerializeField] private LayerMask aimLayers;
    [SerializeField] private LayerMask placementLayers;
    [SerializeField] private LayerMask clearanceLayers;


    [Header("Tuning")]
    [SerializeField, Min(0.1f)] private float placementRange = 7f;
    [SerializeField, Min(0.1f)] private float pickupRange = 7f;
    [SerializeField, Min(0.1f)] private float cameraRayDistance = 100f;
    [SerializeField, Min(0f)] private float closeArcHeight = 2f;


    private const int ArcPointCount = 25;


    // Small gap so it doesnt count towards sheep size.
    private const float SurfaceGap = 0.02f;


    // Keep one point buffer and rembember the previous values/
    private readonly Vector3[] arcPoints = new Vector3[ArcPointCount];
    private Vector3 lastArcStart;
    private Vector3 lastArcEnd;
    private float lastArcHeight;
    private bool lastArcWasValid;


    private PlaceableSheep heldSheep;
    private int attachedFrame = -1;


    //Player movement can read this and decide whether we wanna allow sprint/
    private bool IsHoldingSheep => heldSheep != null;
    public bool CanHold => isActiveAndEnabled && !IsHoldingSheep;


    //Puzzle listeners get the updated sheep and affected surface.
    public event Action<PlaceableSheep, Collider> SheepPlaced;
    public event Action<PlaceableSheep, Collider> SheepPickedUp;



    private void Awake()
    {
        if (aimCamera == null || carrySocket == null || reachOrigin == null || arc == null)
        {
            Debug.Log("Assign the aim cam, carry socket, reach origin and arc.", this);
            enabled = false;
            return;
        }

        arc.useWorldSpace = true;
        arc.positionCount = ArcPointCount;
        HidePreview();
    }



    /// <summary>Attempts to hold an available sheep.</summary>
    /// <param name="sheep">The sheep to hold.</param>
    /// <param name="fallbackRules">Optional backup surface rules.</param>
    /// <returns>True if successfully held.</returns>
    public bool TryHold(PlaceableSheep sheep, ISheepSurfaceRules fallbackRules = null)
    {
        // Terry's inventory can hand off an available sheep, then a placed sheep goes
        // throuhg the world pickup checks before it can be held again. 
        return TryTakeSheep(sheep, PlaceableSheep.PlacementState.Available, fallbackRules);
    }



    /// <summary>Attempts to hold a sheep and notifies listeners if previously placed.</summary>
    /// <param name="sheep">The sheep to hold.</param>
    /// <param name="expectedState">The required placement state.</param>
    /// <param name="fallbackRules">Optional backup surface rules.</param>
    /// <returns>True if successfully held.</returns>
    private bool TryTakeSheep(
        PlaceableSheep sheep, PlaceableSheep.PlacementState expectedState, ISheepSurfaceRules fallbackRules = null)
    {
        if (!CanHold || sheep == null || sheep.State != expectedState)
            return false;

        // saves the old surgace before TryAttach clears it
        Collider previousSurface = sheep.PlacedSurface;

        if (!sheep.TryAttach(carrySocket, fallbackRules))
            return false;

        heldSheep = sheep;
        attachedFrame = Time.frameCount;

        // Uses the old state to decide whether to notify the puzzle, i.e. 
        // if the sheep only gets picked up, to be placed on the same spot, Jon's 
        // systems don't need to know twice. 
        if (expectedState == PlaceableSheep.PlacementState.Placed)
            SheepPickedUp?.Invoke(sheep, previousSurface);
        return true;
    }



    /// <summary>Handles cursor-based sheep pickup and placement.</summary>
    /// <param name="cursorPosition">The current cursor position.</param>
    /// <param name="clicked">Whether the player clicked this frame.</param>
    public void UpdateAim(Vector2 cursorPosition, bool clicked)
    {
        if (!isActiveAndEnabled)
            return;

        bool carrying = IsHoldingSheep;

        // Both interactions use the cursor, when player is not holding anything, 
        // it waits for a clock before doing stuff.
        if ((!carrying && !clicked) || !TryGetCursorHit(cursorPosition, out RaycastHit hit))
        {
            HidePreview();
            return;
        }

        if (!carrying)
        {
            HidePreview();
            TryPickup(hit);
            return;
        }

        UpdatePlacement(hit, clicked);
    }



    /// <summary>Raycasts from the cursor to detect a valid target.</summary>
    /// <param name="cursorPosition">The current cursor position.</param>
    /// <param name="hit">The resulting raycast hit.</param>
    /// <returns>True if a collider is hit.</returns>
    private bool TryGetCursorHit(Vector2 cursorPosition, out RaycastHit hit)
    {
        hit = default;
        if (!aimCamera.pixelRect.Contains(cursorPosition))
            return false;

        // The nearest solid collider on Aim Layers gets the aim.
        Ray ray = aimCamera.ScreenPointToRay(cursorPosition);
        return Physics.Raycast(ray, out hit, cameraRayDistance, aimLayers, QueryTriggerInteraction.Ignore);
    }



    /// <summary>Attempts to pick up a placed sheep within range and without obstruction.</summary>
    /// <param name="hit">The detected pickup target.</param>
    private void TryPickup(RaycastHit hit)
    {
        Vector3 start = reachOrigin.position;

        // A squared distance check is coolasf as we only need Y or N
        if ((hit.point - start).sqrMagnitude > pickupRange * pickupRange)
            return;

        // Sheep pickup collider and this component share a PREFAB ROOT.
        if (!hit.collider.TryGetComponent(out PlaceableSheep sheep))
            return;

        // If there is a block between sheep and player, the pickup says
        // no, the line is only allowed to hit the sheep and itself. i.e.
        // you can't thunderc**t the sheep through a wall.
        if (Physics.Linecast(start, hit.point, out RaycastHit obstruction, aimLayers, QueryTriggerInteraction.Ignore)
            && obstruction.collider != hit.collider)
            return;

        TryTakeSheep(sheep, PlaceableSheep.PlacementState.Placed);
    }



    /// <summary>Updates the placement preview and places the sheep if valid.</summary>
    /// <param name="hit">The targeted placement surface.</param>
    /// <param name="clicked">Whether placement was requested.</param>
    private void UpdatePlacement(RaycastHit hit, bool clicked)
    {
        // Measure gameplay range from the players pivot, but draw line from hand
        Vector3 fromPlayer = reachOrigin.position;
        Vector3 toTarget = hit.point - fromPlayer;
        float distance = toTarget.magnitude;
        float rangeFraction = Mathf.Clamp01(distance / placementRange);
        bool inRange = distance <= placementRange;

        // If target is beyond radius, it draws until end of radius.
        Vector3 end = inRange ? hit.point : fromPlayer + toTarget * (placementRange / distance);

        // Same check does the line colour and placement.
        Pose pose = default;
        bool canPlace = inRange && TryGetPlacementPose(hit, out pose);

        // UI click won't mess with the sheep
        if (clicked && canPlace && Time.frameCount != attachedFrame)
        {
            CommitPlacement(pose, hit.collider);
            return;
        }

        float height = closeArcHeight * (1f - rangeFraction);
        DrawArc(carrySocket.position, end, height, canPlace);
    }



    /// <summary>Validates placement and calculates a surface-aligned pose.</summary>
    /// <param name="hit">The targeted placement surface.</param>
    /// <param name="pose">The calculated position and rotation.</param>
    /// <returns>True if placement is valid.</returns>
    private bool TryGetPlacementPose(RaycastHit hit, out Pose pose)
    {
        pose = default;

        //Reject failures before testing clearence and reach.
        int hitLayer = 1 << hit.collider.gameObject.layer;
        
        if ((placementLayers.value & hitLayer) == 0 || !heldSheep.AllowsSurface(hit))
            return false;

        if (hit.collider.TryGetComponent(out ISheepPlacementTarget target) && !target.CanAcceptSheep(heldSheep, hit))
            return false;

        // Use the surface normal as the sheep's up direction, soooo
        // if  player's forward direction points straight at a wall,
        // their right direction gives us a usable direction along it.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.right, hit.normal);

        Quaternion rotation = Quaternion.LookRotation(forward, hit.normal);
        Vector3 position = hit.point + hit.normal * SurfaceGap;

        // Rotate the local box pivot into world space and check the whole 
        // thang, even tho the mouse is aimed at one lil point.
        Vector3 boxCenter = position + rotation * heldSheep.LocalBoxCenter;

        if (Physics.Linecast(reachOrigin.position, boxCenter, aimLayers, QueryTriggerInteraction.Ignore)) 
            return false;

        // Curve = visual guide, a straight reach test prevents placement through shit
        if (Physics.Linecast(reachOrigin.position, boxCenter, aimLayers, QueryTriggerInteraction.Ignore))
            return false;

        pose = new Pose(position, rotation);
        return true;
    }



    /// <summary>Finalizes sheep placement and notifies puzzle listeners.</summary>
    /// <param name="pose">The placement position and rotation.</param>
    /// <param name="surface">The collider the sheep is placed on.</param>
    private void CommitPlacement(Pose pose, Collider surface)
    {
        //Finish all the placement stuff before talking to Jon's puzzles.
        PlaceableSheep sheep = heldSheep;
        sheep.Place(pose, surface);
        heldSheep = null;
        HidePreview();

        // Sync on commit for puzzle listeners that look for  the new collider
        // STAT, it don't need to be global. 
        Physics.SyncTransforms();
        SheepPlaced?.Invoke(sheep, surface);
    }



    /// <summary>Draws the placement arc and updates its colour based on validity.</summary>
    /// <param name="start">The arc's starting position.</param>
    /// <param name="end">The arc's target position.</param>
    /// <param name="height">The peak height of the arc.</param>
    /// <param name="canPlace">Whether placement is valid.</param>
    private void DrawArc(Vector3 start, Vector3 end, float height, bool canPlace)
    {
        bool wasVisable = arc.enabled;

        // validation still runs every frame, but skips the renderer upload
        // if the line is identical
        if (!wasVisable || !lastArcStart.Equals(start) || !lastArcEnd.Equals(end) || lastArcHeight != height)
        {
            Vector3 span = end - start;
            
            for (int i = 0; i < ArcPointCount; i++)
            {
                // t runs from player hand to target, and the arc should peak halfway.
                float t = i / (float)(ArcPointCount - 1);
                float lift = 4f * height * t * (1f - t);
                arcPoints[i] = start + span * t + Vector3.up * lift;
            }

            arc.SetPositions(arcPoints);
            lastArcStart = start;
            lastArcEnd = end;
            lastArcHeight = height;
        }

        // Obstacles can change validity without moving the arc.
        if (!wasVisable || lastArcWasValid != canPlace)
        {
            Color color = canPlace ? Color.green : Color.red;
            arc.startColor = color;
            arc.endColor = color;
            lastArcWasValid = canPlace;
        }

        if (!wasVisable)
            arc.enabled = true;
    }



    /// <summary>Hides the placement preview arc.</summary>
    public void HidePreview()
    {
        if (arc != null && arc.enabled)
            arc.enabled = false;
    }



    /// <summary>Hides the placement preview when disabled.</summary>
    private void OnDisable()
    {
        HidePreview();
    }
}