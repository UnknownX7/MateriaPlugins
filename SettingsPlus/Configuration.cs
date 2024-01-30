using Materia.Plugin;

namespace SettingsPlus;

public class Configuration : PluginConfiguration
{
    public bool EnableStaticCamera { get; set; }
    public bool DisableActionCamera { get; set; }
    public bool DisableCharacterParts { get; set; }
    public bool DisableHiddenData { get; set; }
    public bool EnableSkipBattleCutscenes { get; set; }
}