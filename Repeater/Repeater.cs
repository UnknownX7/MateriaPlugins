using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.KeyInput;
using BattleSystem = Materia.Game.BattleSystem;
using ModalManager = Materia.Game.ModalManager;

namespace Repeater;

public unsafe class Repeater : IMateriaPlugin
{
    public string Name => "Repeater";
    public string Description => "Repeats battles for you";

    private bool draw = false;
    private bool repeating = false;

    public Repeater(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
    }

    public void Update()
    {
        if (!repeating || BattleSystem.Instance is not { } battleSystem) return;

        if (battleSystem.IsDefeated)
        {
            repeating = false;
            return;
        }

        if (ModalManager.Instance?.CurrentModal is { } currentModal && Il2CppType<IBattleResultModalPresenter>.IsAssignableFrom(currentModal.NativePtr))
            GameInterop.TapKeyAction(KeyAction.Confirm, 50);
    }

    public void Draw()
    {
        if (!draw)
        {
            repeating = false;
            return;
        }

        ImGui.Begin("Repeater", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        ImGui.Checkbox("##Auto", ref repeating);
        ImGui.End();
    }
}