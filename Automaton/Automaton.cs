using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ECGen.Generated;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ECGen.Generated.Command.OutGame.Option;
using ECGen.Generated.Command.OutGame.Stamina;
using ECGen.Generated.Command.UI;
using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using BattleSystem = Materia.Game.BattleSystem;
using ModalManager = Materia.Game.ModalManager;
using ScreenManager = Materia.Game.ScreenManager;

namespace Automaton;

public unsafe class Automaton : IMateriaPlugin
{
    public string Name => "Automaton";
    public string Description => "Automates battles for you";

    private enum SpecialSkillMode
    {
        Disabled,
        Instant,
        Chain,
        Follow
    }

    private static bool hasClosedStaminaModal = false;
    private static bool requeueInstead = false;
    private bool draw = false;
    private bool repeating = false;
    private int repeatDelayMs;
    private readonly Stopwatch repeatDelayStopwatch = new();
    private SpecialSkillMode specialSkillMode;
    private bool beToxic = false;

    public Automaton(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
    }

    public void Update()
    {
        if (!draw) return;

        if (BattleSystem.Instance != null)
            UpdateBattle();
        else if (ScreenManager.Instance != null)
            UpdateOutGame();
    }

    private void UpdateOutGame()
    {
        if (!repeating || ScreenManager.Instance!.InTransition) return;

        if (ScreenManager.Instance.GetCurrentScreen<MultiAreaBattleMatchingRoomScreenPresenter>() is { } matchingRoomScreen)
        {
            if (ModalManager.Instance?.GetCurrentModal<SimpleModalPresenter>() is { } simpleModal && simpleModal.NativePtr->header->titleText->m_text->Equals(GameInterop.GetLocalizedText(LocalizeTextCategory.OutGameBossBattle, 212110))) // Exit Lobby
            {
                GameInterop.TapButton(simpleModal.NativePtr->simpleModalView->positiveButton);
                return;
            }

            if (matchingRoomScreen.NativePtr->isReadyBattle || matchingRoomScreen.NativePtr->loadingUserInfos) return;

            // TODO: Add % bonus checking
            if (beToxic
                && !matchingRoomScreen.NativePtr->param->joinedPrivateRoom
                && GameInterop.IsGameObjectActive(matchingRoomScreen.NativePtr->view->eventBonusInfoButton)
                && matchingRoomScreen.NativePtr->userInfos->PtrEnumerable
                    .Where(p => p.ptr->isActive)
                    .Any(p => !GameInterop.IsGameObjectActive(p.ptr->partyMemberInfoPlate->eventBonusObject)))
                GameInterop.TapKeyAction(KeyAction.Back);
            else if (matchingRoomScreen.NativePtr->view->readyButton->isEnable && matchingRoomScreen.NativePtr->view->readyButton->m_Interactable)
                GameInterop.TapButton(matchingRoomScreen.NativePtr->view->readyButton, true, 10_000);
        }
        else if (ScreenManager.Instance.GetCurrentScreen<MultiAreaBattlePartySelectPresenter>() is { } multiPartySelect)
        {
            if (ModalManager.Instance?.GetCurrentModal<SimpleModalPresenter>() is { } simpleModal && simpleModal.NativePtr->header->titleText->m_text->Equals(GameInterop.GetLocalizedText(LocalizeTextCategory.OutGameBossBattle, 212121))) // Out of Time
            {
                GameInterop.TapButton(simpleModal.NativePtr->simpleModalView->negativeButton); // Let the screen handler requeue instead
                return;
            }

            if (ModalManager.Instance?.CurrentModal != null || GameInterop.IsGameObjectActive(multiPartySelect.NativePtr->multiAreaView->multiAreaBattleWaiting)) return;

            GameInterop.TapButton(multiPartySelect.NativePtr->multiAreaView->randomMatchingButton, true, 10_000);
        }
    }

    private void UpdateBattle()
    {
        if (ModalManager.Instance?.GetCurrentModal<OptionSteamModalPresenter>() != null) return;

        if (repeating)
        {
            if (repeatDelayMs > 1)
            {
                if (!repeatDelayStopwatch.IsRunning)
                {
                    repeatDelayStopwatch.Restart();
                    return;
                }

                if (repeatDelayStopwatch.ElapsedMilliseconds < repeatDelayMs) return;

                repeatDelayMs = 0;
                repeatDelayStopwatch.Stop();
            }

            if (HandleDefeat())
            {
                repeating = false;
                return;
            }

            repeatDelayMs = HandleBattleModals();
            if (repeatDelayMs > 0) return;
        }

        if (HandleSpecialSkills(specialSkillMode)) return;
    }

    private static bool HandleDefeat() => BattleSystem.Instance is { IsDefeated: true };

    private static int HandleBattleModals()
    {
        if (ModalManager.Instance is not { } modalManager) return 0;

        if (modalManager.CurrentModal is { } currentModal && Il2CppType<IBattleResultModalPresenter>.IsAssignableFrom(currentModal.NativePtr))
        {
            if ((modalManager.GetCurrentModal<MultiAreaBattleResultModalPresenter>() is { } multiModal && GameInterop.IsGameObjectActive(multiModal.NativePtr->waitingVotePresenter))
                || (modalManager.GetCurrentModal<EventMultiAreaBattleResultModalPresenter>() is { } eventMultiModal && GameInterop.IsGameObjectActive(eventMultiModal.NativePtr->waitingVotePresenter)))
                return 1;

            if (BattleSystem.Instance is { IsMultiplayer: true })
            {
                if (requeueInstead && GameInterop.TapKeyAction(KeyAction.Back, false, 50))
                {
                    return 1;
                }
                else if (hasClosedStaminaModal)
                {
                    hasClosedStaminaModal = false;
                    GameInterop.TapKeyAction(KeyAction.Back);
                    return 1;
                }
            }

            GameInterop.TapKeyAction(KeyAction.Confirm, false, 50);
            return 1;
        }
        else if (modalManager.GetCurrentModal<StaminaRecoverModal>() is { } staminaRecoverModal)
        {
            hasClosedStaminaModal = true;
            return GameInterop.TapButton(staminaRecoverModal.NativePtr->modalCloseButton)
                ? BattleSystem.Instance is { IsMultiplayer: true } ? 10_000 : 10 * 60_000
                : 1;
        }
        else if (modalManager.GetCurrentModal<MultiAreaBattleFirstMeetingModalPresenter>() is { } firstMeetingModal)
        {
            GameInterop.TapButton(firstMeetingModal.NativePtr->okButton);
            return 1;
        }
        else if (modalManager.GetCurrentModal<BattleResultFriendRequestModalPresenter>() is { } friendRequestModal)
        {
            GameInterop.TapButton(friendRequestModal.NativePtr->closeButton);
            return 1;
        }

        return 0;
    }

    private static bool HandleSpecialSkills(SpecialSkillMode specialSkillMode)
    {
        if (specialSkillMode == SpecialSkillMode.Disabled || BattleHUD.Instance is not { } battleHUD) return false;

        var players = battleHUD.NativePtr->characterStatusManagers->PtrEnumerable
            .Where(p => p.ptr->hudKind is CharacterStatusManager.HudKind.Player or CharacterStatusManager.HudKind.Friend)
            .SelectMany(p => p.ptr->statusGaugePool->PtrEnumerable)
            .ToArray();

        var readySpecialSkillButtons = new List<Ptr<SpecialSkillButtonPresenter>>(3);
        var readyCount = 0;
        foreach (var p in players)
        {
            if (!Il2CppType<PlayerStatusPresenter>.Is(p.ptr, out var playerStatusPresenter)) continue;

            var isLb = GameInterop.IsGameObjectActive(playerStatusPresenter->limitBreakGaugePresenter);
            var model = isLb ? playerStatusPresenter->limitBreakGaugePresenter->specialSkillModel : playerStatusPresenter->summonSkillGaugePresenter->specialSkillModel;
            if (model == null) continue;

            var presenter = isLb ? playerStatusPresenter->limitBreakButtonPresenter : playerStatusPresenter->summonSkillButtonPresenter;
            switch (presenter->stateMachine->currentKey->GetValue())
            {
                case SpecialSkillButtonPresenter.Status.Active:
                    readySpecialSkillButtons.Add(new Ptr<SpecialSkillButtonPresenter>(presenter));
                    goto case SpecialSkillButtonPresenter.Status.Reserved;
                case SpecialSkillButtonPresenter.Status.Reserved:
                case SpecialSkillButtonPresenter.Status.Invalid when model->chargeRate->GetValue() >= 1: // Multiplayer TODO: Check for disables
                    readyCount++;
                    break;
            }
        }

        var activate = specialSkillMode switch
        {
            SpecialSkillMode.Instant => true,
            SpecialSkillMode.Chain => players.Length == readyCount,
            SpecialSkillMode.Follow => battleHUD.CurrentStatus == HUD.Status.LimitCombo,
            _ => false
        };

        return activate && readySpecialSkillButtons.Any(p => GameInterop.TapButton(p.ptr->button));
    }

    public void Draw()
    {
        if (!draw)
        {
            repeating = false;
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(180, 160) * ImGuiEx.Scale);
        ImGui.Begin("Automaton", ref draw, ImGuiWindowFlags.NoResize);

        if (ImGui.Checkbox("Repeat", ref repeating))
        {
            repeatDelayMs = 0;
            repeatDelayStopwatch.Stop();
        }

        ImGui.Checkbox("Leave No Bonus Co-op", ref beToxic);
        ImGui.Checkbox("Exit After Co-op", ref requeueInstead);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("L. Ability Usage Mode");
        ImGuiEx.Combo(string.Empty, ref specialSkillMode);

        ImGui.End();
    }
}