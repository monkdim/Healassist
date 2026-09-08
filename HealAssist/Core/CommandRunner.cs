using FFXIVClientStructs.FFXIV.Client.Game;
using HealAssist.Data;

namespace HealAssist.Core;

/// <summary>
/// Executes the two user-facing actions. Must be called on the framework thread.
/// </summary>
public sealed class CommandRunner(Configuration config, PartyScanner scanner, TargetPicker picker, RaiseSequencer sequencer)
{
    private const uint SwiftcastStatusId = 167;

    /// <summary>Return value of <c>GetActionStatus</c> meaning "you may use this right now".</summary>
    private const uint ActionUsable = 0;

    public PickResult TargetRaise()
    {
        if (Svc.Me is null)
            return Report(PickResult.Fail(PickFailure.NotLoggedIn), "Raise");

        var members = scanner.Scan(config.RaiseIncludeAlliance, config.RaiseIncludeNearby);
        var result = picker.PickRaiseTarget(members);

        if (result.Target is null)
            return Report(result, "Raise");

        Svc.Targets.Target = result.Target.GameObject;

        if (config.AutoCastRaise
            && !TryPhantomRevive(result.Target)
            && !sequencer.TryBegin(result.Target))
        {
            TryAutoCastRaise(result.Target);
        }

        return Report(result, "Raise");
    }

    public PickResult TargetLowestHp()
    {
        if (Svc.Me is null)
            return Report(PickResult.Fail(PickFailure.NotLoggedIn), "Lowest HP");

        var members = scanner.Scan(config.LowestIncludeAlliance, config.LowestIncludeNearby);
        var result = picker.PickLowestHpTarget(members);

        if (result.Target is null)
        {
            if (!config.LowestKeepTargetIfNoneFound)
                Svc.Targets.Target = null;
            return Report(result, "Lowest HP");
        }

        Svc.Targets.Target = result.Target.GameObject;
        return Report(result, "Lowest HP");
    }

    /// <summary>
    /// Live preview for the settings window. Never changes your target. The local job travels with
    /// the snapshot because the settings window draws on the render thread, where reading the
    /// object table throws.
    /// </summary>
    public (PickResult Raise, PickResult Lowest, IReadOnlyList<PartyMemberInfo> Members, uint LocalJobId) Preview()
    {
        var me = Svc.Me;
        if (me is null)
        {
            var none = PickResult.Fail(PickFailure.NotLoggedIn);
            return (none, none, [], 0);
        }

        // The preview scans the union of both commands' sources; the picker filters per command.
        var members = scanner.Scan(
            config.RaiseIncludeAlliance || config.LowestIncludeAlliance,
            config.RaiseIncludeNearby || config.LowestIncludeNearby);
        return (picker.PickRaiseTarget(members), picker.PickLowestHpTarget(members), members, me.ClassJob.RowId);
    }

    private PickResult Report(PickResult result, string label)
    {
        if (result.Target is not null)
        {
            if (config.ChatFeedbackOnSuccess)
                Svc.Chat.Print($"[HealAssist] {label}: {result.Target.Name} ({result.Target.JobAbbreviation})");
        }
        else if (config.ChatFeedbackOnFailure)
        {
            Svc.Chat.Print($"[HealAssist] {label}: {TargetPicker.DescribeFailure(result.Failure)}");
        }

        return result;
    }

    // -- Optional auto-cast ------------------------------------------------
    // Off by default. Everything above only moves your target cursor; this is the one place
    // HealAssist presses a button on your behalf.

    /// <summary>
    /// Phantom Revive is instant and works on any job, so wherever the game accepts it there is no
    /// reason to cast anything else. Asking the game whether it is usable is also how we detect
    /// being in the Occult Crescent with it slotted, without tracking zones or phantom jobs.
    /// </summary>
    private bool TryPhantomRevive(PartyMemberInfo target)
    {
        if (!config.PreferPhantomRevive)
            return false;

        var revive = Actions.Adjust(JobTable.PhantomReviveActionId);
        if (!Actions.Use(revive, target.GameObject.EntityId))
            return false;

        if (config.ChatFeedbackOnSuccess)
            Svc.Chat.Print($"[HealAssist] phantom Revive on {target.Name}");

        return true;
    }

    private void TryAutoCastRaise(PartyMemberInfo target)
    {
        var me = Svc.Me;
        if (me is null)
            return;

        var raiseAction = config.GetRaiseActionFor(me.ClassJob.RowId);
        if (raiseAction == 0)
        {
            if (config.ChatFeedbackOnFailure)
                Svc.Chat.Print($"[HealAssist] {JobTable.GetAbbreviation(me.ClassJob.RowId)} has no raise spell to cast.");
            return;
        }

        if (config.AutoCastUseSwiftcast && !HasSwiftcast(me) && TryUseAction(JobTable.SwiftcastActionId, me.EntityId))
        {
            // Swiftcast landed; the raise below goes out instantly. If the raise is rejected this
            // frame the game keeps Swiftcast up for ten seconds, so nothing is wasted.
            Svc.Log.Debug("Swiftcast used before raise");
        }

        TryUseAction(raiseAction, target.GameObject.EntityId);
    }

    private static bool HasSwiftcast(Dalamud.Game.ClientState.Objects.Types.IBattleChara me)
    {
        foreach (var status in me.StatusList)
        {
            if (status is not null && status.StatusId == SwiftcastStatusId)
                return true;
        }

        return false;
    }

    private static unsafe bool TryUseAction(uint actionId, uint targetEntityId)
    {
        var manager = ActionManager.Instance();
        if (manager == null)
            return false;

        if (manager->GetActionStatus(ActionType.Action, actionId, targetEntityId) != ActionUsable)
            return false;

        return manager->UseAction(ActionType.Action, actionId, targetEntityId);
    }
}
