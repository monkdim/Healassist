using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HealAssist;

/// <summary>
/// Dalamud services, injected once at load by <see cref="IDalamudPluginInterface.Create{T}"/>.
/// </summary>
internal sealed class Svc
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IPartyList Party { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    /// <summary>
    /// The local player. Isolated here because it moved from IClientState to IObjectTable in
    /// Dalamud API 14, and IClientState.LocalPlayer was removed outright in API 15 — if you ever
    /// build against an older level, this is the only line that needs to change back.
    /// </summary>
    internal static IPlayerCharacter? Me => Objects.LocalPlayer;
}
