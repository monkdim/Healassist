using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace HealAssist.Core;

/// <summary>
/// Optional in-game hotkeys, polled on the framework thread. These read the game's own key state,
/// so they only fire while the game window has focus.
/// </summary>
public sealed class HotkeyManager(Configuration config, CommandRunner runner)
{
    private bool raiseWasDown;
    private bool lowestWasDown;

    /// <summary>Set while the settings window is capturing a new binding, to avoid firing the action.</summary>
    public bool SuppressForRebind { get; set; }

    public void Update(IFramework framework)
    {
        if (!config.RaiseHotkey.IsBound && !config.LowestHotkey.IsBound)
            return;

        if (Svc.Me is null || SuppressForRebind || !AllowedRightNow())
        {
            raiseWasDown = false;
            lowestWasDown = false;
            return;
        }

        raiseWasDown = Edge(config.RaiseHotkey, raiseWasDown, runner.TargetRaise);
        lowestWasDown = Edge(config.LowestHotkey, lowestWasDown, runner.TargetLowestHp);
    }

    private static bool Edge(Hotkey hotkey, bool wasDown, Func<PickResult> action)
    {
        var isDown = hotkey.IsBound && IsPressed(hotkey);

        // Fire once on the press, not every frame the key is held.
        if (isDown && !wasDown)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "Hotkey action failed");
            }
        }

        return isDown;
    }

    private static bool IsPressed(Hotkey hotkey)
    {
        if (!IsDown(hotkey.Key))
            return false;

        return IsDown(VirtualKey.CONTROL) == hotkey.Ctrl
            && IsDown(VirtualKey.MENU) == hotkey.Alt
            && IsDown(VirtualKey.SHIFT) == hotkey.Shift;
    }

    /// <summary>
    /// Reading a key the game does not track throws, so unknown keys are treated as "not pressed".
    /// </summary>
    public static bool IsDown(VirtualKey key)
    {
        if (key == VirtualKey.NO_KEY || !Svc.KeyState.IsVirtualKeyValid(key))
            return false;

        return Svc.KeyState[key];
    }

    private bool AllowedRightNow()
    {
        if (Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Svc.Condition[ConditionFlag.OccupiedInQuestEvent])
            return false;

        return !config.HotkeysOnlyInCombat || Svc.Condition[ConditionFlag.InCombat];
    }
}
