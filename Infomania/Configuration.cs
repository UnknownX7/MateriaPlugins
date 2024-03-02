using Materia.Plugin;

namespace Infomania;

public class Configuration : PluginConfiguration
{
    public bool EnableHomeInfo { get; set; }
    public bool EnablePartySelectInfo { get; set; }
    public bool EnablePartyEditInfo { get; set; }
    public bool EnableGiftInfo { get; set; }
    public bool EnableBossDetailInfo { get; set; }
}