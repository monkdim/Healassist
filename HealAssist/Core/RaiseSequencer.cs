using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using HealAssist.Data;

namespace HealAssist.Core;

/// <summary>
/// Red Mage specific raise handling.
///
/// Verraise is a ten second cast, the slowest resurrection in the game, but Dualcast makes the
/// next spell instant. So the fast route is to spend one two second cast on anything, then let
/// Verraise ride the Dualcast that generates. Jolt is the better filler because it deals damage
/// and builds mana rather than wasting the global cooldown, and Vercure covers the case where
/// nothing is attackable.
///
/// Everything here runs on the framework thread. Actions are sent with an explicit target ID, so
/// your target cursor stays on the body the whole time and never bounces to the enemy.
/// </summary>
public sealed class RaiseSequencer(Configuration config)
{
    private enum Step
    {
        Idle,

        /// <summary>A filler cast is in flight. Fire Verraise as soon as it grants an instant cast.</summary>
        AwaitingInstantCast,
    }

    private Step step = Step.Idle;
    private uint corpseEntityId;
    private long deadlineMs;
    private string pendingName = string.Empty;

    public bool IsRunning => step != Step.Idle;

    /// <summary>
    /// Handles the raise if this is a Red Mage with the option enabled. Returns false to let the
    /// ordinary single action path deal with it.
    /// </summary>
    public bool TryBegin(PartyMemberInfo target)
    {
        var me = Svc.Me;
        if (me is null || !config.RdmSmartRaise || me.ClassJob.RowId != JobTable.RedMageJobId)
            return false;

        Reset();

        var verraise = Actions.Adjust(config.GetRaiseActionFor(JobTable.RedMageJobId));
        if (verraise == 0)
            return false;

        // Already instant. Nothing to set up.
        if (HasInstantCast(me))
        {
            Actions.Use(verraise, target.GameObject.EntityId);
            return true;
        }

        // Swiftcast is free in global cooldown terms, so it beats spending a cast on filler.
        if (config.AutoCastUseSwiftcast && Actions.Use(JobTable.SwiftcastActionId, me.EntityId))
        {
            Begin(target, "Swiftcast");
            return true;
        }

        if (config.RdmUseJolt && TryJoltNearestEnemy())
        {
            Begin(target, "Jolt");
            return true;
        }

        if (Actions.Use(Actions.Adjust(JobTable.VercureActionId), me.EntityId))
        {
            Begin(target, "Vercure");
            return true;
        }

        // No way to shortcut it. Take the ten second cast rather than doing nothing.
        Actions.Use(verraise, target.GameObject.EntityId);
        Report($"no filler available, hard casting on {target.Name}");
        return true;
    }

    public void Tick()
    {
        if (step != Step.AwaitingInstantCast)
            return;

        var me = Svc.Me;
        if (me is null)
        {
            Reset();
            return;
        }

        if (Environment.TickCount64 > deadlineMs)
        {
            Report("gave up waiting for an instant cast");
            Reset();
            return;
        }

        // The filler is still going out.
        if (me.IsCasting)
            return;

        if (!HasInstantCast(me))
            return;

        var corpse = Svc.Objects.SearchByEntityId(corpseEntityId);
        if (corpse is null)
        {
            Report("the body is no longer there");
            Reset();
            return;
        }

        var verraise = Actions.Adjust(config.GetRaiseActionFor(JobTable.RedMageJobId));

        // Verraise is instant now but still costs a global cooldown, so this can fail for a beat
        // after the filler lands. Keep trying until the deadline rather than dropping the raise.
        if (!Actions.Use(verraise, corpseEntityId))
            return;

        Report($"{pendingName} into instant Verraise on {corpse.Name.TextValue}");
        Reset();
    }

    public void Abort()
    {
        if (step != Step.Idle)
            Reset();
    }

    private void Begin(PartyMemberInfo target, string filler)
    {
        step = Step.AwaitingInstantCast;
        corpseEntityId = target.GameObject.EntityId;
        pendingName = filler;
        deadlineMs = Environment.TickCount64 + (long)(config.RdmSequenceTimeoutSeconds * 1000f);
    }

    private void Reset()
    {
        step = Step.Idle;
        corpseEntityId = 0;
        pendingName = string.Empty;
    }

    private void Report(string message)
    {
        if (config.ChatFeedbackOnSuccess)
            Svc.Chat.Print($"[HealAssist] {message}");
    }

    private static bool HasInstantCast(IBattleChara me)
    {
        foreach (var status in me.StatusList)
        {
            if (status is not null && JobTable.InstantCastStatusIds.Contains(status.StatusId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Jolt at the closest thing the game will actually let us hit. Asking the game whether the
    /// action is usable covers range, hostility and targetability in one go.
    /// </summary>
    private static bool TryJoltNearestEnemy()
    {
        var me = Svc.Me;
        if (me is null)
            return false;

        var jolt = Actions.Adjust(JobTable.JoltActionId);
        if (jolt == 0)
            return false;

        var candidates = new List<(float Distance, uint EntityId)>();

        foreach (var obj in Svc.Objects)
        {
            if (obj.ObjectKind != ObjectKind.BattleNpc || !obj.IsTargetable)
                continue;
            if (obj is not IBattleChara enemy || enemy.CurrentHp == 0)
                continue;

            candidates.Add((System.Numerics.Vector3.Distance(me.Position, obj.Position), obj.EntityId));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        foreach (var (_, entityId) in candidates)
        {
            if (Actions.Use(jolt, entityId))
                return true;
        }

        return false;
    }
}
