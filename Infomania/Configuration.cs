using System.Collections.Generic;
using Materia.Plugin;

namespace Infomania;

public class Configuration : PluginConfiguration
{
    public class InfoConfiguration
    {
        public bool Locked { get; set; }
        public float Scale { get; set; } = 1;
    }

    public bool EnableHomeInfo { get; set; }
    public bool EnablePartySelectInfo { get; set; }
    public bool EnablePartyEditInfo { get; set; }
    public bool EnableGiftInfo { get; set; }
    public bool EnableBossDetailInfo { get; set; }
    public bool EnableWeaponDetailInfo { get; set; }
    public bool EnableItemDetailInfo { get; set; }
    public bool EnableUserInfo { get; set; }
    public Dictionary<string, InfoConfiguration> InfoConfigs { get; set; } = [];
}