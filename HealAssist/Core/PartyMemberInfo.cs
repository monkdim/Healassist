using Dalamud.Game.ClientState.Objects.Types;
using HealAssist.Data;

namespace HealAssist.Core;

/// <summary>
/// Where a candidate came from. Ordering matters: candidates are ranked by this first, so your
/// own party always outranks the wider alliance, which outranks unaffiliated players nearby.
/// </summary>
public enum MemberSource
{
    Party = 0,
    Alliance = 1,
    Nearby = 2,
}

/// <summary>
/// A flattened snapshot of one candidate, taken on the framework thread so the UI can read it
/// safely from the render thread.
/// </summary>
public sealed class PartyMemberInfo
{
    public required IGameObject GameObject { get; init; }
    public required string Name { get; init; }
    public required uint JobId { get; init; }
    public required RoleType Role { get; init; }
    public required uint CurrentHp { get; init; }
    public required uint MaxHp { get; init; }
    public required float Distance { get; init; }
    public required int PartyIndex { get; init; }
    public required bool IsSelf { get; init; }
    public required MemberSource Source { get; init; }

    /// <summary>Has a resurrection already inbound or sitting in the accept/decline prompt.</summary>
    public required bool HasRaisePending { get; init; }

    /// <summary>Somebody else is visibly mid-cast on this corpse with a raise spell.</summary>
    public required bool IsBeingRaisedByOther { get; init; }

    public bool IsDead => CurrentHp == 0;

    public float HpPercent => MaxHp == 0 ? 0f : CurrentHp / (float)MaxHp * 100f;

    public string JobAbbreviation => JobTable.GetAbbreviation(JobId);
}
