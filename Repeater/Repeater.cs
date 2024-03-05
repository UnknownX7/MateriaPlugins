using System.Diagnostics;
using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.OutGame.Stamina;
using BattleSystem = Materia.Game.BattleSystem;
using ModalManager = Materia.Game.ModalManager;

namespace Repeater;

public unsafe class Repeater : IMateriaPlugin
{
    public string Name => "Repeater";
    public string Description => "Repeats battles for you";

    private const int staminaRepeatDelayMs = 10 * 60_000;
    private bool draw = false;
    private bool repeating = false;
    private readonly Stopwatch staminaRepeatTimer = new();

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

        if (ModalManager.Instance is not { } modalManager) return;

        if (modalManager.CurrentModal is { } currentModal && Il2CppType<IBattleResultModalPresenter>.IsAssignableFrom(currentModal.NativePtr))
        {
            GameInterop.TapKeyAction(KeyAction.Confirm, false, 50);
            staminaRepeatTimer.Restart();
        }
        else if (modalManager.GetCurrentModal<StaminaRecoverModal>() is { } staminaRecoverModal && staminaRepeatTimer is { IsRunning: true, ElapsedMilliseconds: > staminaRepeatDelayMs })
        {
            GameInterop.TapButton(staminaRecoverModal.NativePtr->modalCloseButton);
        }
    }

    public void Draw()
    {
        if (!draw)
        {
            repeating = false;
            return;
        }

        ImGui.Begin("Repeater", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        if (ImGui.Checkbox("##Auto", ref repeating))
            staminaRepeatTimer.Restart();
        ImGui.End();
    }
}