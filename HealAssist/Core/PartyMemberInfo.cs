using Dalamud.Game.ClientState.Objects.Types;
using HealAssist.Data;

namespace HealAssist.Core;

/// <summary>
/// A flattened snapshot of one party member, taken on the framework thread so the UI can read it
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
    public required bool IsAllianceMember { get; init; }

    /// <summary>Has a resurrection already inbound or sitting in the accept/decline prompt.</summary>
    public required bool HasRaisePending { get; init; }

    /// <summary>Somebody else is visibly mid-cast on this corpse with a raise spell.</summary>
    public required bool IsBeingRaisedByOther { get; init; }

    public bool IsDead => CurrentHp == 0;

    public float HpPercent => MaxHp == 0 ? 0f : CurrentHp / (float)MaxHp * 100f;

    public string JobAbbreviation => JobTable.GetAbbreviation(JobId);
}
