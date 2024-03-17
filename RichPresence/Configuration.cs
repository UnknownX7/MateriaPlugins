using Materia.Plugin;

namespace RichPresence;

public class Configuration : PluginConfiguration
{
    public bool EnableRichPresence { get; set; }
    public bool EnableDetailedInfo { get; set; }
    public bool EnableMultiplayerInvites { get; set; }
    public bool EnableOnlyInLobby { get; set; }
}