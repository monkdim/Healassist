using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using HealAssist.Data;

namespace HealAssist;

public enum PriorityKind
{
    Role = 0,
    Player = 1,
}

/// <summary>One line in the raise priority list. Either "this role" or "this specific person".</summary>
public sealed class PriorityEntry
{
    public PriorityKind Kind { get; set; } = PriorityKind.Role;
    public RoleType Role { get; set; } = RoleType.Healer;
    public string PlayerName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public static PriorityEntry ForRole(RoleType role) => new() { Kind = PriorityKind.Role, Role = role };

    public static PriorityEntry ForPlayer(string name) =>
        new() { Kind = PriorityKind.Player, PlayerName = name.Trim() };

    public string Label => Kind == PriorityKind.Player
        ? (string.IsNullOrWhiteSpace(PlayerName) ? "(unnamed player)" : PlayerName)
        : Role.DisplayName();

    public PriorityEntry Clone() => new()
    {
        Kind = Kind,
        Role = Role,
        PlayerName = PlayerName,
        Enabled = Enabled,
    };
}

public sealed class Hotkey
{
    public bool Enabled { get; set; }
    public VirtualKey Key { get; set; } = VirtualKey.NO_KEY;
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }

    public bool IsBound => Enabled && Key != VirtualKey.NO_KEY;

    public string Describe()
    {
        if (Key == VirtualKey.NO_KEY)
            return "Not bound";

        var parts = new List<string>(4);
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key.ToString());
        return string.Join(" + ", parts);
    }

    public void Clear()
    {
        Key = VirtualKey.NO_KEY;
        Ctrl = Alt = Shift = false;
    }
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    // ---- Raise ----------------------------------------------------------

    /// <summary>Ordered highest-priority-first. The first entry that matches a dead member wins.</summary>
    public List<PriorityEntry> RaisePriority { get; set; } = DefaultPriority();

    /// <summary>Ignore dead members that match nothing in the priority list.</summary>
    public bool RaiseOnlyListed { get; set; }

    /// <summary>Skip corpses that already have a resurrection inbound or waiting to be accepted.</summary>
    public bool RaiseSkipPending { get; set; } = true;

    /// <summary>Skip corpses another player is visibly mid-raise on.</summary>
    public bool RaiseSkipBeingRaisedByOthers { get; set; } = true;

    /// <summary>Maximum distance in yalms. 0 disables the check. Raise spells reach 30y.</summary>
    public float RaiseMaxDistance { get; set; } = 30f;

    /// <summary>Also consider the other two alliance parties in 24-player content.</summary>
    public bool RaiseIncludeAlliance { get; set; }

    /// <summary>
    /// Also consider players who share the zone but are in no party of yours. This is what makes
    /// the raise button work in field operations like Occult Crescent, Bozja and Eureka.
    /// </summary>
    public bool RaiseIncludeNearby { get; set; }

    /// <summary>Opt-in: let the plugin fire the raise itself instead of relying on a macro line.</summary>
    public bool AutoCastRaise { get; set; }

    /// <summary>When auto-casting, use Swiftcast first if it is off cooldown and you are not already swift.</summary>
    public bool AutoCastUseSwiftcast { get; set; }

    /// <summary>
    /// Use the Occult Crescent phantom Revive when the game says it is available. It is instant and
    /// bypasses Resurrection Restriction, so it beats every job raise wherever it works.
    /// </summary>
    public bool PreferPhantomRevive { get; set; } = true;

    /// <summary>
    /// Red Mage only. Spend one short cast to generate Dualcast, then fire Verraise instantly,
    /// which turns a ten second resurrection into roughly two.
    /// </summary>
    public bool RdmSmartRaise { get; set; }

    /// <summary>Prefer Jolt over Vercure for the filler cast, so the global cooldown is not wasted.</summary>
    public bool RdmUseJolt { get; set; } = true;

    /// <summary>How long to keep trying to land the follow-up Verraise before giving up.</summary>
    public float RdmSequenceTimeoutSeconds { get; set; } = 6f;

    /// <summary>Per-job overrides for the raise action ID, in case an ID ever changes.</summary>
    public Dictionary<uint, uint> RaiseActionOverrides { get; set; } = new();

    // ---- Lowest HP ------------------------------------------------------

    public bool LowestIncludeSelf { get; set; } = true;

    /// <summary>Only consider members at or below this HP percentage. 100 considers everyone.</summary>
    public float LowestMaxHpPercent { get; set; } = 100f;

    /// <summary>Maximum distance in yalms. 0 disables the check.</summary>
    public float LowestMaxDistance { get; set; } = 30f;

    public bool LowestIncludeAlliance { get; set; }

    /// <summary>Also consider unaffiliated players in the zone, for field operations.</summary>
    public bool LowestIncludeNearby { get; set; }

    /// <summary>Keep the current target rather than clearing it when nobody qualifies.</summary>
    public bool LowestKeepTargetIfNoneFound { get; set; } = true;

    // ---- Feedback -------------------------------------------------------

    public bool ChatFeedbackOnSuccess { get; set; } = true;
    public bool ChatFeedbackOnFailure { get; set; } = true;

    // ---- Hotkeys --------------------------------------------------------

    public Hotkey RaiseHotkey { get; set; } = new();
    public Hotkey LowestHotkey { get; set; } = new();

    /// <summary>Ignore hotkeys while not in a duty or combat-relevant state is not checked; this only gates on being logged in.</summary>
    public bool HotkeysOnlyInCombat { get; set; }

    // ---------------------------------------------------------------------

    public static List<PriorityEntry> DefaultPriority() =>
    [
        PriorityEntry.ForRole(RoleType.Healer),
        PriorityEntry.ForRole(RoleType.Tank),
        PriorityEntry.ForRole(RoleType.MeleeDps),
        PriorityEntry.ForRole(RoleType.PhysicalRangedDps),
        PriorityEntry.ForRole(RoleType.MagicalRangedDps),
    ];

    public uint GetRaiseActionFor(uint jobId) =>
        RaiseActionOverrides.TryGetValue(jobId, out var overridden) && overridden != 0
            ? overridden
            : JobTable.GetDefaultRaiseAction(jobId);

    public void Save() => Svc.PluginInterface.SavePluginConfig(this);
}
