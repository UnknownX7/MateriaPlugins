using Materia.Plugin;

namespace SettingsPlus;

public class Configuration : PluginConfiguration
{
    public bool EnableStaticCamera { get; set; }
    public bool DisableActionCamera { get; set; }
    public bool DisableCharacterParts { get; set; }
    public bool EnableBetterWeaponNotificationIcon { get; set; }
    public bool DisableHiddenData { get; set; }
    public bool EnableSkipBattleCutscenes { get; set; }
    public bool EnableRememberLastSelectedMateriaRecipe { get; set; }
    public bool DisableRenamedRecommendedParty { get; set; }
    public bool DisableContinueModal { get; set; }
}