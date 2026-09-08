using FFXIVClientStructs.FFXIV.Client.Game;

namespace HealAssist.Core;

/// <summary>
/// Thin wrapper over the game's action manager. Every call here must run on the framework thread.
/// </summary>
public static unsafe class Actions
{
    /// <summary>Return value of GetActionStatus meaning "you may use this right now".</summary>
    private const uint Usable = 0;

    /// <summary>
    /// Resolves an action to the upgrade your current level grants, so passing base Jolt returns
    /// Jolt II or Jolt III as appropriate and we do not have to track level thresholds.
    /// </summary>
    public static uint Adjust(uint actionId)
    {
        var manager = ActionManager.Instance();
        return manager == null ? actionId : manager->GetAdjustedActionId(actionId);
    }

    /// <summary>
    /// Whether the action can be used on that target right now. This defers to the game for range,
    /// cooldown, target validity and hostility, which is far more reliable than reimplementing them.
    /// </summary>
    public static bool CanUse(uint actionId, uint targetEntityId)
    {
        var manager = ActionManager.Instance();
        return manager != null
            && manager->GetActionStatus(ActionType.Action, actionId, targetEntityId) == Usable;
    }

    public static bool Use(uint actionId, uint targetEntityId)
    {
        var manager = ActionManager.Instance();
        if (manager == null || !CanUse(actionId, targetEntityId))
            return false;

        return manager->UseAction(ActionType.Action, actionId, targetEntityId);
    }
}
