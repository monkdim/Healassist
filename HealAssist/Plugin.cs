using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HealAssist.Core;
using HealAssist.Windows;

namespace HealAssist;

/// <summary>Cached party state so the settings window can show a live preview without touching game memory.</summary>
public sealed record PreviewSnapshot(
    PickResult Raise,
    PickResult Lowest,
    IReadOnlyList<PartyMemberInfo> Members,
    uint LocalJobId)
{
    public static readonly PreviewSnapshot Empty = new(
        PickResult.Fail(PickFailure.NotLoggedIn),
        PickResult.Fail(PickFailure.NotLoggedIn),
        [],
        0);
}

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandMain = "/healassist";
    private const string CommandShort = "/ha";
    private const string CommandRaise = "/harez";
    private const string CommandLowest = "/halow";

    private const long PreviewIntervalMs = 150;

    private readonly List<string> registeredCommands = [];
    private readonly WindowSystem windowSystem = new("HealAssist");
    private readonly ConfigWindow configWindow;
    private readonly HotkeyManager hotkeys;
    private readonly CommandRunner runner;
    private readonly RaiseSequencer sequencer;

    private long nextPreviewTick;

    public Configuration Config { get; }

    /// <summary>Refreshed on the framework thread while the settings window is open.</summary>
    public PreviewSnapshot Preview { get; private set; } = PreviewSnapshot.Empty;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Svc>();

        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.RaisePriority ??= Configuration.DefaultPriority();
        Config.RaiseActionOverrides ??= new Dictionary<uint, uint>();

        var scanner = new PartyScanner(Config);
        var picker = new TargetPicker(Config);
        sequencer = new RaiseSequencer(Config);
        runner = new CommandRunner(Config, scanner, picker, sequencer);
        hotkeys = new HotkeyManager(Config, runner);

        configWindow = new ConfigWindow(this, runner, picker, hotkeys);
        windowSystem.AddWindow(configWindow);

        AddCommand(CommandMain, "Open HealAssist settings. Also accepts: rez, low.");
        AddCommand(CommandShort, "Short form of /healassist.");
        AddCommand(CommandRaise, "Target the highest-priority raisable dead party member.");
        AddCommand(CommandLowest, "Target the party member with the lowest HP percentage.");

        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;

        foreach (var command in registeredCommands)
            Svc.Commands.RemoveHandler(command);

        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
    }

    private void AddCommand(string command, string help)
    {
        var added = Svc.Commands.AddHandler(command, new CommandInfo(OnCommand)
        {
            HelpMessage = help,
            ShowInHelp = command != CommandShort,
        });

        if (added)
            registeredCommands.Add(command);
        else
            Svc.Log.Warning("Command {Command} is already taken by another plugin; skipping it.", command);
    }

    private void OnCommand(string command, string args)
    {
        switch (command)
        {
            case CommandRaise:
                runner.TargetRaise();
                return;
            case CommandLowest:
                runner.TargetLowestHp();
                return;
        }

        switch (args.Trim().ToLowerInvariant())
        {
            case "":
            case "config":
            case "settings":
                OpenConfig();
                return;
            case "rez":
            case "raise":
            case "res":
                runner.TargetRaise();
                return;
            case "low":
            case "lowest":
            case "lowhp":
                runner.TargetLowestHp();
                return;
            default:
                Svc.Chat.Print($"[HealAssist] Unknown option. Try: {CommandMain}, {CommandRaise}, {CommandLowest}");
                return;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            hotkeys.Update(framework);
            sequencer.Tick();

            if (!configWindow.IsOpen)
                return;

            var now = Environment.TickCount64;
            if (now < nextPreviewTick)
                return;

            nextPreviewTick = now + PreviewIntervalMs;

            var (raise, lowest, members, localJobId) = runner.Preview();
            Preview = new PreviewSnapshot(raise, lowest, members, localJobId);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Framework update failed");
        }
    }

    private void OpenConfig() => configWindow.IsOpen = true;
}
