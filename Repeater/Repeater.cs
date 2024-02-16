using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using ECGen.Generated.Command;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.UI;
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
            TapKeyAction(KeyAction.Confirm, 50);
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

    private static bool TapKeyAction(KeyAction keyAction, uint lockoutMs = 2000)
    {
        var ret = false;
        if (GameInterop.GetSingletonInstance<KeyMapManager>() is var keyMapManager && (keyMapManager == null || keyMapManager->keyMaps->size == 0)) return ret;

        var keyMap = keyMapManager->keyMaps->GetPtr(keyMapManager->keyMaps->size - 1);
        for (int i = 0; i < keyMap->keyHandlers->size; i++)
        {
            if (!Il2CppType<SingleTapButton>.Is(keyMap->keyHandlers->GetPtr(i), out var singleTapButton)) continue;
            var buttonKeyAction = singleTapButton->steamKeyAction != KeyAction.None ? singleTapButton->steamKeyAction : singleTapButton->steamKeyActionDefault;
            if (buttonKeyAction == keyAction || buttonKeyAction == KeyAction.Any)
                ret |= GameInterop.TapButton(singleTapButton, lockoutMs);
        }
        return ret;
    }
}