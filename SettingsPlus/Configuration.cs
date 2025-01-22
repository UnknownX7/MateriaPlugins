using Materia.Plugin;

namespace SettingsPlus;

public class Configuration : PluginConfiguration
{
    public bool EnableStaticCamera { get; set; }
    public bool DisableCharacterParts { get; set; }
    public bool EnableBetterWeaponNotificationIcon { get; set; }
    public bool DisableHiddenData { get; set; }
    public bool EnableSkipBattleCutscenes { get; set; }
    public bool EnableRememberLastSelectedMateriaRecipe { get; set; }
    public bool DisableRenamedRecommendedParty { get; set; }
    public bool DisableContinueModal { get; set; }
    public bool EnableBattleReselection { get; set; }
    public bool EnableAudioFocus { get; set; }
    public bool EnableSynthesisRarity { get; set; }
    public int InterjectionDisplayLimit { get; set; } = 3;
    public bool EnableQuickChocoboosters { get; set; }
}