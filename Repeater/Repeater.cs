using System.Diagnostics;
using System.Linq;
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
    private bool useSpecialSkills = false;
    private readonly Stopwatch staminaRepeatTimer = new();

    public Repeater(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
    }

    public void Update()
    {
        if (!repeating || BattleSystem.Instance is not { } battleSystem || BattleHUD.Instance is not { } battleHUD) return;

        if (battleSystem.IsDefeated)
        {
            repeating = false;
            return;
        }

        if (ModalManager.Instance is { } modalManager)
        {
            if (modalManager.CurrentModal is { } currentModal && Il2CppType<IBattleResultModalPresenter>.IsAssignableFrom(currentModal.NativePtr))
            {
                GameInterop.TapKeyAction(KeyAction.Confirm, false, 50);
                staminaRepeatTimer.Restart();
                return;
            }
            else if (modalManager.GetCurrentModal<StaminaRecoverModal>() is { } staminaRecoverModal && staminaRepeatTimer is { IsRunning: true, ElapsedMilliseconds: > staminaRepeatDelayMs })
            {
                GameInterop.TapButton(staminaRecoverModal.NativePtr->modalCloseButton);
                return;
            }
        }

        if (!useSpecialSkills) return;

        foreach (var p in battleHUD.NativePtr->characterStatusManagers->PtrEnumerable
            .Where(p => p.ptr->hudKind is CharacterStatusManager.HudKind.Player or CharacterStatusManager.HudKind.Friend)
            .SelectMany(p => p.ptr->statusGaugePool->PtrEnumerable))
        {
            if (!Il2CppType<PlayerStatusPresenter>.Is(p.ptr, out var playerStatusPresenter)) continue;
            if (GameInterop.IsGameObjectActive(playerStatusPresenter->limitBreakButtonPresenter) && playerStatusPresenter->limitBreakButtonPresenter->stateMachine->currentKey->GetValue() == SpecialSkillButtonPresenter.Status.Active)
            {
                GameInterop.TapButton(playerStatusPresenter->limitBreakButtonPresenter->button);
                break;
            }
            else if (GameInterop.IsGameObjectActive(playerStatusPresenter->summonSkillButtonPresenter) && playerStatusPresenter->summonSkillButtonPresenter->stateMachine->currentKey->GetValue() == SpecialSkillButtonPresenter.Status.Active)
            {
                GameInterop.TapButton(playerStatusPresenter->summonSkillButtonPresenter->button);
                break;
            }
        }
    }

    public void Draw()
    {
        if (!draw)
        {
            repeating = false;
            return;
        }

        ImGui.Begin("Repeater", ref draw, ImGuiWindowFlags.AlwaysAutoResize);
        if (ImGui.Checkbox("Repeat", ref repeating))
            staminaRepeatTimer.Restart();
        ImGui.Checkbox("Use Special Skills", ref useSpecialSkills);
        ImGui.End();
    }
}