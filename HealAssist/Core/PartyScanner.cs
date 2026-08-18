using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Statuses;
using HealAssist.Data;

namespace HealAssist.Core;

/// <summary>
/// Builds the list of players HealAssist can act on. Everything in here must run on the framework
/// thread, since <see cref="Svc.Objects"/> throws if touched from anywhere else.
/// </summary>
public sealed class PartyScanner(Configuration config)
{
    private const int MaxAllianceSlots = 16;

    /// <summary>
    /// Raise casts currently in flight, keyed by the cast target's object ID. Cast targets are
    /// reported as ulong GameObjectIds, which match a plain entity ID for ordinary players.
    /// </summary>
    private readonly HashSet<ulong> incomingRaises = [];

    /// <summary>Entity IDs already added, so the nearby sweep does not duplicate party members.</summary>
    private readonly HashSet<uint> seen = [];

    /// <param name="includeAlliance">Also read the other two parties in 24-player content.</param>
    /// <param name="includeNearby">
    /// Also sweep the object table for unaffiliated players. This is what makes the plugin useful
    /// in field operations like Occult Crescent, where the people who need raising are in the zone
    /// with you but not in your party or alliance.
    /// </param>
    public IReadOnlyList<PartyMemberInfo> Scan(bool includeAlliance, bool includeNearby)
    {
        var result = new List<PartyMemberInfo>(8);
        seen.Clear();

        var me = Svc.Me;
        if (me is null)
            return result;

        CollectIncomingRaises(me.EntityId);

        if (Svc.Party.Length == 0)
        {
            // Solo, or in content that has not populated the party list. You are still a legitimate
            // target for the lowest-HP command.
            Add(result, Build(me, me.CurrentHp, me.MaxHp, me.ClassJob.RowId, me.StatusList, me.Position,
                              me.Position, 0, true, MemberSource.Party));
        }
        else
        {
            for (var i = 0; i < Svc.Party.Length; i++)
            {
                var member = Svc.Party[i];
                if (member is null)
                    continue;

                Add(result, BuildFromMember(member, me.Position, i, MemberSource.Party, me.EntityId));
            }
        }

        if (includeAlliance)
            CollectAlliance(result, me.Position, me.EntityId);

        if (includeNearby)
            CollectNearby(result, me, me.EntityId);

        return result;
    }

    private void Add(List<PartyMemberInfo> result, PartyMemberInfo? info)
    {
        if (info is null || !seen.Add(info.GameObject.EntityId))
            return;

        result.Add(info);
    }

    private void CollectAlliance(List<PartyMemberInfo> result, Vector3 origin, uint myEntityId)
    {
        // The alliance list is only populated in 24-player content. Reading an empty slot yields
        // a null address, so the loop simply finds nothing outside of alliance raids.
        for (var i = 0; i < MaxAllianceSlots; i++)
        {
            IPartyMember? member;
            try
            {
                var address = Svc.Party.GetAllianceMemberAddress(i);
                if (address == nint.Zero)
                    continue;
                member = Svc.Party.CreateAllianceMemberReference(address);
            }
            catch (Exception ex)
            {
                Svc.Log.Debug(ex, "Alliance slot {Slot} could not be read", i);
                continue;
            }

            if (member is null)
                continue;

            Add(result, BuildFromMember(member, origin, Svc.Party.Length + i, MemberSource.Alliance, myEntityId));
        }
    }

    /// <summary>
    /// Every other player object in range that is not already accounted for. In a full field
    /// operation this is a few dozen objects, walked once per command press.
    /// </summary>
    private void CollectNearby(List<PartyMemberInfo> result, IBattleChara me, uint myEntityId)
    {
        var limit = Math.Max(config.RaiseMaxDistance, config.LowestMaxDistance);

        foreach (var player in Svc.Objects.PlayerObjects)
        {
            if (player.EntityId == myEntityId || seen.Contains(player.EntityId))
                continue;

            // A corpse you cannot target is a corpse you cannot raise.
            if (!player.IsTargetable)
                continue;

            // The nearby sweep is the one unbounded source, so cut it by distance up front rather
            // than building snapshots for the whole zone.
            var distance = Vector3.Distance(me.Position, player.Position);
            if (limit > 0 && distance > limit)
                continue;

            Add(result, Build(player, player.CurrentHp, player.MaxHp, player.ClassJob.RowId,
                              player.StatusList, player.Position, me.Position,
                              int.MaxValue, false, MemberSource.Nearby));
        }
    }

    private PartyMemberInfo? BuildFromMember(IPartyMember member, Vector3 origin, int index, MemberSource source, uint myEntityId)
    {
        // Out of object-table range. There is nothing to target, so there is nothing to do.
        if (member.GameObject is null)
            return null;

        return Build(member.GameObject, member.CurrentHP, member.MaxHP, member.ClassJob.RowId,
                     member.Statuses, member.Position, origin, index,
                     member.GameObject.EntityId == myEntityId, source);
    }

    private PartyMemberInfo? Build(
        IGameObject gameObject,
        uint currentHp,
        uint maxHp,
        uint jobId,
        StatusList? statuses,
        Vector3 position,
        Vector3 origin,
        int index,
        bool isSelf,
        MemberSource source)
    {
        // 0 and the "empty" sentinel both mean the slot points at nothing targetable.
        if (gameObject.EntityId is 0 or 0xE000_0000)
            return null;

        return new PartyMemberInfo
        {
            GameObject = gameObject,
            Name = gameObject.Name.TextValue,
            JobId = jobId,
            Role = JobTable.GetRole(jobId),
            CurrentHp = currentHp,
            MaxHp = maxHp,
            Distance = Vector3.Distance(origin, position),
            PartyIndex = index,
            IsSelf = isSelf,
            Source = source,
            HasRaisePending = HasRaisePending(statuses),
            IsBeingRaisedByOther = incomingRaises.Contains(gameObject.EntityId),
        };
    }

    private static bool HasRaisePending(StatusList? statuses)
    {
        if (statuses is null)
            return false;

        foreach (var status in statuses)
        {
            if (status is not null && status.StatusId == JobTable.RaisePendingStatusId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds every player currently mid-cast on a raise spell and records who they are casting it
    /// on, so we do not hand you a corpse another healer has already committed to.
    /// </summary>
    private void CollectIncomingRaises(uint myEntityId)
    {
        incomingRaises.Clear();

        if (!config.RaiseSkipBeingRaisedByOthers)
            return;

        foreach (var caster in Svc.Objects.PlayerObjects)
        {
            if (caster.EntityId == myEntityId || !caster.IsCasting)
                continue;
            if (!JobTable.AllRaiseActionIds.Contains(caster.CastActionId))
                continue;

            incomingRaises.Add(caster.CastTargetObjectId);
        }
    }
}
