namespace HealAssist.Data;

/// <summary>
/// Static job data. Job row IDs and action IDs are stable across patches, so they are
/// hard-coded rather than read from the Excel sheets (which change shape between API levels).
/// </summary>
public static class JobTable
{
    /// <summary>Status ID applied to a corpse with a resurrection already inbound or waiting to be accepted.</summary>
    public const uint RaisePendingStatusId = 148;

    /// <summary>Swiftcast, shared across all casters.</summary>
    public const uint SwiftcastActionId = 7561;

    private static readonly Dictionary<uint, RoleType> Roles = new()
    {
        // Tanks
        [1] = RoleType.Tank,  // GLA
        [3] = RoleType.Tank,  // MRD
        [19] = RoleType.Tank, // PLD
        [21] = RoleType.Tank, // WAR
        [32] = RoleType.Tank, // DRK
        [37] = RoleType.Tank, // GNB

        // Healers
        [6] = RoleType.Healer,  // CNJ
        [24] = RoleType.Healer, // WHM
        [28] = RoleType.Healer, // SCH
        [33] = RoleType.Healer, // AST
        [40] = RoleType.Healer, // SGE

        // Melee DPS
        [2] = RoleType.MeleeDps,  // PGL
        [4] = RoleType.MeleeDps,  // LNC
        [29] = RoleType.MeleeDps, // ROG
        [20] = RoleType.MeleeDps, // MNK
        [22] = RoleType.MeleeDps, // DRG
        [30] = RoleType.MeleeDps, // NIN
        [34] = RoleType.MeleeDps, // SAM
        [39] = RoleType.MeleeDps, // RPR
        [41] = RoleType.MeleeDps, // VPR

        // Physical ranged DPS
        [5] = RoleType.PhysicalRangedDps,  // ARC
        [23] = RoleType.PhysicalRangedDps, // BRD
        [31] = RoleType.PhysicalRangedDps, // MCH
        [38] = RoleType.PhysicalRangedDps, // DNC

        // Magical ranged DPS
        [7] = RoleType.MagicalRangedDps,  // THM
        [26] = RoleType.MagicalRangedDps, // ACN
        [25] = RoleType.MagicalRangedDps, // BLM
        [27] = RoleType.MagicalRangedDps, // SMN
        [35] = RoleType.MagicalRangedDps, // RDM
        [36] = RoleType.MagicalRangedDps, // BLU
        [42] = RoleType.MagicalRangedDps, // PCT
    };

    /// <summary>Default resurrection action for each job that has one. Verified against XIVAPI.</summary>
    private static readonly Dictionary<uint, uint> RaiseActions = new()
    {
        [6] = 125,     // CNJ — Raise
        [24] = 125,    // WHM — Raise
        [26] = 173,    // ACN — Resurrection
        [27] = 173,    // SMN — Resurrection
        [28] = 173,    // SCH — Resurrection
        [33] = 3603,   // AST — Ascend
        [35] = 7523,   // RDM — Verraise
        [36] = 18317,  // BLU — Angel Whisper
        [40] = 24287,  // SGE — Egeiro
    };

    private static readonly Dictionary<uint, string> JobAbbreviations = new()
    {
        [1] = "GLA", [2] = "PGL", [3] = "MRD", [4] = "LNC", [5] = "ARC", [6] = "CNJ", [7] = "THM",
        [19] = "PLD", [20] = "MNK", [21] = "WAR", [22] = "DRG", [23] = "BRD", [24] = "WHM",
        [25] = "BLM", [26] = "ACN", [27] = "SMN", [28] = "SCH", [29] = "ROG", [30] = "NIN",
        [31] = "MCH", [32] = "DRK", [33] = "AST", [34] = "SAM", [35] = "RDM", [36] = "BLU",
        [37] = "GNB", [38] = "DNC", [39] = "RPR", [40] = "SGE", [41] = "VPR", [42] = "PCT",
    };

    /// <summary>All raise action IDs, used to detect that somebody else is already casting a raise.</summary>
    public static readonly HashSet<uint> AllRaiseActionIds = [.. RaiseActions.Values];

    /// <summary>Jobs that can resurrect, in display order, for the settings UI.</summary>
    public static readonly uint[] RaiseCapableJobs = [24, 28, 33, 40, 27, 35, 36];

    public static RoleType GetRole(uint jobId) =>
        Roles.TryGetValue(jobId, out var role) ? role : RoleType.Unknown;

    public static uint GetDefaultRaiseAction(uint jobId) =>
        RaiseActions.TryGetValue(jobId, out var action) ? action : 0u;

    public static string GetAbbreviation(uint jobId) =>
        JobAbbreviations.TryGetValue(jobId, out var abbr) ? abbr : $"Job{jobId}";
}
