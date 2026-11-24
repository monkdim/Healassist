namespace Dalamud.Game.ClientState.Objects.Types { public interface IGameObject { } }
namespace Dalamud.Configuration { public interface IPluginConfiguration { int Version { get; set; } } }
namespace Dalamud.Game.ClientState.Keys { public enum VirtualKey { NO_KEY } }
namespace HealAssist
{
    public static class Svc { public static ConfigStore PluginInterface = new(); }
    public sealed class ConfigStore { public void SavePluginConfig(Configuration config) { } }
}
