using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using HealAssist.Data;

namespace HealAssist.Core;

/// <summary>
/// Builds the list of party members HealAssist can act on. Everything in here must run on the
/// framework thread — <see cref="Svc.Objects"/> throws if touched from anywhere else.
/// </summary>
public sealed class PartyScanner(Configuration config)
{
    private const int MaxAllianceSlots = 16;

    /// <summary>
    /// Raise casts currently in flight, keyed by the cast target's object ID. Cast targets are
    /// reported as ulong GameObjectIds, which match a plain entity ID for ordinary players.
    /// </summary>
    private readonly HashSet<ulong> incomingRaises = [];

    public IReadOnlyList<PartyMemberInfo> Scan(bool includeAlliance)
    {
        var result = new List<PartyMemberInfo>(8);

        var me = Svc.Me;
        if (me is null)
            return result;

        CollectIncomingRaises(me.EntityId);

        if (Svc.Party.Length == 0)
        {
            // Solo, or in a duty that has not populated the party list yet. You are still a
            // legitimate target for the lowest-HP command.
            var solo = Build(me, me.CurrentHp, me.MaxHp, me.ClassJob.RowId, me.StatusList, me.Position,
                             me.Position, 0, true, false);
            if (solo is not null)
                result.Add(solo);
            return result;
        }

        for (var i = 0; i < Svc.Party.Length; i++)
        {
            var member = Svc.Party[i];
            if (member is null)
                continue;

            var info = BuildFromMember(member, me.Position, i, isAlliance: false, me.EntityId);
            if (info is not null)
                result.Add(info);
        }

        if (includeAlliance)
            CollectAlliance(result, me.Position, me.EntityId);

        return result;
    }

    private void CollectAlliance(List<PartyMemberInfo> result, Vector3 origin, uint myEntityId)
    {
        // The alliance list is only populated in 24-player content. Reading an empty slot yields
        // a null address, so the loop simply finds nothing outside of alliance raids.
        for (var i = 0; i < MaxAllianceSlots; i++)
        {
            nint address;
            IPartyMember? member;
            try
            {
                address = Svc.Party.GetAllianceMemberAddress(i);
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

            var info = BuildFromMember(member, origin, Svc.Party.Length + i, isAlliance: true, myEntityId);

            // Alliance and party lists can overlap in some transitional states.
            if (info is not null && result.All(existing => existing.GameObject.EntityId != info.GameObject.EntityId))
                result.Add(info);
        }
    }

    private PartyMemberInfo? BuildFromMember(IPartyMember member, Vector3 origin, int index, bool isAlliance, uint myEntityId)
    {
        // Out of object-table range. There is nothing to target, so there is nothing to do.
        if (member.GameObject is null)
            return null;

        return Build(member.GameObject, member.CurrentHP, member.MaxHP, member.ClassJob.RowId,
                     member.Statuses, member.Position, origin, index,
                     member.GameObject.EntityId == myEntityId, isAlliance);
    }

    private PartyMemberInfo? Build(
        IGameObject gameObject,
        uint currentHp,
        uint maxHp,
        uint jobId,
        Dalamud.Game.ClientState.Statuses.StatusList? statuses,
        Vector3 position,
        Vector3 origin,
        int index,
        bool isSelf,
        bool isAlliance)
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
            IsAllianceMember = isAlliance,
            HasRaisePending = HasRaisePending(statuses),
            IsBeingRaisedByOther = incomingRaises.Contains(gameObject.EntityId),
        };
    }

    private static bool HasRaisePending(Dalamud.Game.ClientState.Statuses.StatusList? statuses)
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
