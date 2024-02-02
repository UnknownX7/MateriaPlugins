using ECGen.Generated;
using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using System.Numerics;

namespace Infomania;

// TODO: Use enums
public unsafe class Infomania : IMateriaPlugin
{
    public string Name => "Infomania";
    public string Description => "Displays extra information on certain menus";
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();

    private bool draw = false;

    public Infomania(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
    }

    public void Draw()
    {
        if (draw)
            DrawSettings();

        var currentModal = ModalManager.Instance?.CurrentModal;
        switch (currentModal?.TypeName)
        {
            case "Command.OutGame.Gift.GiftModalPresenter" when Config.EnableGiftInfo:
                GiftInfo.Draw((Command_OutGame_Gift_GiftModalPresenter*)currentModal.NativePtr);
                break;
            case "Command.OutGame.BossDetailModalPresenter" when Config.EnableBossDetailInfo:
                BossDetailInfo.DrawBossDetailInfo((Command_OutGame_BossDetailModalPresenter*)currentModal.NativePtr);
                break;
            case "Command.OutGame.BossSelectDetailModalPresenter" when Config.EnableBossDetailInfo:
                BossDetailInfo.DrawBossSelectDetailInfo((Command_OutGame_BossSelectDetailModalPresenter*)currentModal.NativePtr);
                break;
            case null:
                break;
            default:
                return;
        }

        var currentScreen = ScreenManager.Instance?.CurrentScreen;
        switch (currentScreen?.TypeName)
        {
            case "Command.OutGame.Party.PartyEditTopScreenPresenter" when Config.EnablePartyEditInfo:
            case "Command.OutGame.Party.MultiAreaBattlePartyEditPresenter" when Config.EnablePartyEditInfo:
                PartyInfo.DrawPartyEditInfo((Command_OutGame_Party_PartyEditTopScreenPresenter*)currentScreen.NativePtr);
                break;
        }
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowSizeConstraints(new Vector2(250, 100) * ImGuiEx.Scale, new Vector2(10000));
        ImGui.Begin("Infomania", ref draw);

        var b = Config.EnablePartyEditInfo;
        if (ImGui.Checkbox("Enable Party Edit Stats", ref b))
        {
            Config.EnablePartyEditInfo = b;
            Config.Save();
        }

        b = Config.EnableGiftInfo;
        if (ImGui.Checkbox("Enable Gift Count", ref b))
        {
            Config.EnableGiftInfo = b;
            Config.Save();
        }

        b = Config.EnableBossDetailInfo;
        if (ImGui.Checkbox("Enable Enemy Detail Stats", ref b))
        {
            Config.EnableBossDetailInfo = b;
            Config.Save();
        }

        ImGui.End();
    }
}