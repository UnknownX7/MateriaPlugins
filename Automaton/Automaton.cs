﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ECGen.Generated;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ECGen.Generated.Command.OutGame.Option;
using ECGen.Generated.Command.OutGame.Party;
using ECGen.Generated.Command.OutGame.Stamina;
using ECGen.Generated.Command.UI;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
using BattleSystem = Materia.Game.BattleSystem;
using ModalManager = Materia.Game.ModalManager;
using SceneBehaviourManager = Materia.Game.SceneBehaviourManager;
using ScreenManager = Materia.Game.ScreenManager;

namespace Automaton;

[Injection]
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
    private static bool cactuarFarm = false;
    private static bool exit = false;
    private static long prevSoloAreaBattleID = 0;
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
        else if (cactuarFarm && ScreenManager.Instance.GetCurrentScreen<SoloPartySelectScreenPresenter>() is { } soloPartySelect)
        {
            if (!soloPartySelect.NativePtr->canStaminaBoost) return;
            soloPartySelect.NativePtr->staminaBoostType = StaminaBoostType.None;
            soloPartySelect.NativePtr->soloPartyView->challengeButton->TapButton();
        }
        else if (prevSoloAreaBattleID > 0)
        {
            ScreenManager.TransitionAsync(TransitionType.AreaSoloBattle, prevSoloAreaBattleID);
            prevSoloAreaBattleID = 0;
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

            if (exit)
            {
                exit = !Retire();
                if (exit)
                    repeatDelayMs = 250;
                return;
            }

            if (HandleDefeat())
            {
                repeating = false;
                return;
            }

            repeatDelayMs = HandleBattleModals();
            if (repeatDelayMs > 0) return;

            HandleBattle();
        }

        if (HandleSpecialSkills(specialSkillMode)) return;
    }

    private static void HandleBattle()
    {
        var battleSystem = BattleSystem.Instance!;
        if (!cactuarFarm
            || battleSystem.IsServerside
            || battleSystem.NativePtr->elapsedBattleTime->GetValue() < 0.1f
            || battleSystem.NativePtr->battleResultType->GetValue() != BattleResultType.None
            || SceneBehaviourManager.GetCurrentSceneBehaviour<BattleSceneBehaviour>() is not { } scene
            || !Il2CppType<BattleSceneBehaviour.SetupParameter>.Is(scene.NativePtr->battlePlayer->setupParameter, out var setup)
            || !setup->canRetire
            || setup->staminaBoostType == StaminaBoostType.None
            || setup->battleModeType != BattleModeType.Normal)
            return;

        var rareWaveID = battleSystem.NativePtr->resumeRareWaveInfo != null ? battleSystem.NativePtr->resumeRareWaveInfo->rareWaveId : 0;
        //var rareType = (BattleRareWaveType)(WorkManager.GetRareWaveStore(rareWaveID) is var rareWaveStore && rareWaveStore != null ? rareWaveStore->masterBattleRareWave->battleRareWaveType : 0);
        var areaID = setup->areaBattleId;
        if (rareWaveID != 0 || areaID == 0) return;

        prevSoloAreaBattleID = areaID;
        exit = true;
    }

    private static bool Retire()
    {
        var battleSystem = BattleSystem.Instance!;
        if (battleSystem.IsPlayingCutscene
            || !battleSystem.NativePtr->isPlayingBattle->GetValue()
            || battleSystem.NativePtr->elapsedBattleTime->GetValue() < 1
            || battleSystem.NativePtr->battleResultType->GetValue() != BattleResultType.None)
            return false;

        onBattleEnd(battleSystem.NativePtr, BattleResultType.Retire, 0);
        return true;
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
            else if (cactuarFarm
                && modalManager.GetCurrentModal<SoloAreaBattleResultModalPresenter>() is { } soloModal
                && Il2CppType<AreaBattleWork.SoloAreaBattleStore>.Is(soloModal.NativePtr->soloAreaBattleInfo, out var store)
                && store->masterSoloAreaBattle->staminaCost != 0
                && store->winResetInfo == null)
            {
                soloModal.NativePtr->nextBattleStaminaBoostType = convertStaminaBoostTypeIfNeeded(StaminaBoostType.Normal3, 0);
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

        ImGui.SetNextWindowSize(new Vector2(190, 180) * ImGuiEx.Scale);
        ImGui.Begin("Automaton", ref draw, ImGuiWindowFlags.NoResize);

        if (ImGui.Checkbox("Repeat", ref repeating))
        {
            exit = false;
            prevSoloAreaBattleID = 0;
            repeatDelayMs = 0;
            repeatDelayStopwatch.Stop();
        }

        ImGui.Checkbox("Cactuar Farm", ref cactuarFarm);
        ImGui.Checkbox("Leave No Bonus Co-op", ref beToxic);
        ImGui.Checkbox("Exit After Co-op", ref requeueInstead);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("L. Ability Usage Mode");
        ImGuiEx.Combo(string.Empty, ref specialSkillMode);

        ImGui.End();
    }

    [GameSymbol("Command.OutGame.StaminaBoost.StaminaBoostUtility$$ConvertStaminaBoostTypeIfNeeded")]
    private static delegate* unmanaged<StaminaBoostType, nint, StaminaBoostType> convertStaminaBoostTypeIfNeeded;

    //[GameSymbol("Command.Battle.BattleSystem$$SetPause")]
    //private static delegate* unmanaged<void*, CBool, nint, void> battleSystemSetPause;

    [GameSymbol("Command.Battle.BattleSystem$$OnBattleEnd")]
    private static delegate* unmanaged<void*, BattleResultType, nint, void> onBattleEnd;
}