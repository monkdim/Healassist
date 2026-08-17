namespace HealAssist.Data;

/// <summary>
/// Roles a party member can be sorted by in the raise priority list.
/// </summary>
public enum RoleType
{
    Unknown = 0,
    Tank = 1,
    Healer = 2,
    MeleeDps = 3,
    PhysicalRangedDps = 4,
    MagicalRangedDps = 5,
}

public static class RoleTypeExtensions
{
    public static string DisplayName(this RoleType role) => role switch
    {
        RoleType.Tank => "Tank",
        RoleType.Healer => "Healer",
        RoleType.MeleeDps => "Melee DPS",
        RoleType.PhysicalRangedDps => "Physical Ranged DPS",
        RoleType.MagicalRangedDps => "Magical Ranged DPS (Caster)",
        _ => "Unknown",
    };

    /// <summary>Roles that can be added to a priority list, in a sensible default order.</summary>
    public static readonly RoleType[] Selectable =
    [
        RoleType.Healer,
        RoleType.Tank,
        RoleType.MeleeDps,
        RoleType.PhysicalRangedDps,
        RoleType.MagicalRangedDps,
    ];
}
