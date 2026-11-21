// IMPORTANT: Unity saves enum fields as their number, not their name.
// Always add new sheep types at the END of the list, and never reorder or
// remove existing ones. Otherwise every saved scene and prefab will quietly
// point at the wrong sheep type.

/// <summary>
/// The different kinds of sheep used by the HUD. Each value names one
/// sheep type, so scripts can store and compare sheep types safely.
/// </summary>
public enum SheepTypes
{
    /// <summary>
    /// A standard sheep with no special ability. This is the default value.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// A heavy sheep that weighs more than a normal sheep.
    /// </summary>
    Heavy = 1,

    /// <summary>
    /// A bouncy sheep that springs off surfaces.
    /// </summary>
    Bouncy = 2,

    /// <summary>
    /// A sticky sheep that sticks to surfaces.
    /// </summary>
    Sticky = 3,

    /// <summary>
    /// A projectile sheep that can be launched.
    /// </summary>
    Projectile = 4
}