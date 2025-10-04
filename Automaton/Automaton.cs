using System.Collections.Generic;
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
using WorkManager = Materia.Game.WorkManager;

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

    private enum CactuarFarmMode
    {
        Disabled,
        All,
        Normal,
        Mythril
    }

    private static bool hasClosedStaminaModal = false;
    private static bool requeueInstead = false;
    private static CactuarFarmMode cactuarFarmMode = CactuarFarmMode.Disabled;
    private static bool exit = false;
    private static long prevSoloAreaBattleID = 0;
    private static bool prevBattleIDIsEvent = false;
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
            else if (GameInterop.IsGameObjectActive(matchingRoomScreen.NativePtr->view->readyButton) && matchingRoomScreen.NativePtr->view->readyButton->isEnable && matchingRoomScreen.NativePtr->view->readyButton->m_Interactable)
                GameInterop.TapButton(matchingRoomScreen.NativePtr->view->readyButton, true, 10_000);
            else if (GameInterop.IsGameObjectActive(matchingRoomScreen.NativePtr->view->playBattleButton) && matchingRoomScreen.NativePtr->view->playBattleButton->isEnable && matchingRoomScreen.NativePtr->view->playBattleButton->m_Interactable)
                GameInterop.TapButton(matchingRoomScreen.NativePtr->view->playBattleButton, true, 1_000);
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
        else if (cactuarFarmMode != CactuarFarmMode.Disabled && ScreenManager.Instance.GetCurrentScreen<SoloPartySelectScreenPresenter>() is { } soloPartySelect)
        {
            if (!soloPartySelect.NativePtr->canStaminaBoost || ModalManager.Instance?.CurrentModal != null) return;
            soloPartySelect.NativePtr->staminaBoostType = StaminaBoostType.None;
            soloPartySelect.NativePtr->soloPartyView->challengeButton->TapButton();
        }
        else if (prevSoloAreaBattleID > 0)
        {
            ScreenManager.TransitionAsync(prevBattleIDIsEvent ? TransitionType.EventSoloBattle : TransitionType.AreaSoloBattle, prevSoloAreaBattleID);
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
        if (cactuarFarmMode == CactuarFarmMode.Disabled
            || battleSystem.IsServerside
            || !battleSystem.NativePtr->isPlayingBattle->GetValue()
            || battleSystem.NativePtr->battleResultType->GetValue() != BattleResultType.None
            || SceneBehaviourManager.GetCurrentSceneBehaviour<BattleSceneBehaviour>() is not { } scene
            || !Il2CppType<BattleSceneBehaviour.SetupParameter>.Is(scene.NativePtr->battlePlayer->setupParameter, out var setup)
            || !setup->canRetire
            || setup->staminaBoostType == StaminaBoostType.None
            || setup->battleModeType != BattleModeType.Normal)
            return;

        long areaID;
        if (setup->areaBattleId != 0)
        {
            areaID = setup->areaBattleId;
            prevBattleIDIsEvent = false;
        }
        else if (setup->eventAreaBattleId != 0)
        {
            areaID = setup->eventAreaBattleId;
            prevBattleIDIsEvent = true;
        }
        else
        {
            return;
        }

        var resumeRareWaveInfo = battleSystem.NativePtr->resumeRareWaveInfo;
        if (resumeRareWaveInfo != null && resumeRareWaveInfo->rareWaveId != 0)
        {

            var rareType = resumeRareWaveInfo->version switch
            {
                BattleRareWaveVersion.Version1 => (BattleRareWaveType)(WorkManager.GetRareWaveStore(resumeRareWaveInfo->rareWaveId) is var rareWaveStore && rareWaveStore != null ? rareWaveStore->masterBattleRareWave->battleRareWaveType : 0),
                _ => BattleRareWaveType.Normal
            };

            switch (cactuarFarmMode)
            {
                case CactuarFarmMode.All:
                    return;
                case CactuarFarmMode.Normal when rareType == BattleRareWaveType.Normal:
                    return;
                case CactuarFarmMode.Mythril when rareType == BattleRareWaveType.GuildBonus:
                    return;
            }
        }

        prevSoloAreaBattleID = areaID;
        exit = true;
    }

    private static bool Retire()
    {
        var battleSystem = BattleSystem.Instance!;
        if (battleSystem.IsPlayingCutscene
            || !battleSystem.NativePtr->isPlayingBattle->GetValue()
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
                if (requeueInstead && GameInterop.TapKeyAction(KeyAction.Back, true, 50))
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
            else if (cactuarFarmMode != CactuarFarmMode.Disabled)
            {
                if (modalManager.GetCurrentModal<SoloAreaBattleResultModalPresenter>() is { } soloModal
                    && Il2CppType<AreaBattleWork.SoloAreaBattleStore>.Is(soloModal.NativePtr->soloAreaBattleInfo, out var soloStore)
                    && soloStore->masterSoloAreaBattle->staminaCost != 0
                    && GameInterop.IsGameObjectActive(soloModal.NativePtr->view->staminaBoostButton))
                {
                    soloModal.NativePtr->nextBattleStaminaBoostType = convertStaminaBoostTypeIfNeeded(StaminaBoostType.Normal3, 0);
                }
                else if (modalManager.GetCurrentModal<EventSoloAreaBattleResultModalPresenter>() is { } eventSoloModal
                    && Il2CppType<EventWork.EventSoloBattleStore>.Is(eventSoloModal.NativePtr->eventSoloBattleInfo, out var eventStore)
                    && eventStore->masterEventSoloBattle->staminaCost != 0
                    && GameInterop.IsGameObjectActive(eventSoloModal.NativePtr->view->staminaBoostButton))
                {
                    eventSoloModal.NativePtr->nextBattleStaminaBoostType = convertStaminaBoostTypeIfNeeded(StaminaBoostType.Normal3, 0);
                }
            }

            // Started after 3.0.0, not sure what causes this, seems to be trying to press something that doesn't exist for some reason
            // System.Runtime.InteropServices.SEHException (0x80004005): External component has thrown an exception.
            try
            {
                GameInterop.TapKeyAction(KeyAction.Confirm, true, 50);
            }
            catch { }
            return 1;
        }
        else if (modalManager.GetCurrentModal<StaminaRecoverModal>() is { } staminaRecoverModal)
        {
            hasClosedStaminaModal = true;
            return GameInterop.TapButton(staminaRecoverModal.NativePtr->modalCloseButton)
                ? BattleSystem.Instance is { IsMultiplayer: true } ? 10_000 : 2 * 60_000
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

        ImGui.SetNextWindowSize(new Vector2(190, 200) * ImGuiEx.Scale);
        ImGui.Begin("Automaton", ref draw, ImGuiWindowFlags.NoResize);

        if (ImGui.Checkbox("Repeat", ref repeating))
        {
            exit = false;
            prevSoloAreaBattleID = 0;
            repeatDelayMs = 0;
            repeatDelayStopwatch.Stop();
        }

        ImGui.TextUnformatted("Cactuar Farm Mode");
        ImGuiEx.Combo("##CactuarFarmMode", ref cactuarFarmMode);

        ImGui.Checkbox("Leave No Bonus Co-op", ref beToxic);
        ImGui.Checkbox("Exit After Co-op", ref requeueInstead);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("L. Ability Usage Mode");
        ImGuiEx.Combo("##SpecialSkillMode", ref specialSkillMode);

        ImGui.End();
    }

    [GameSymbol("Command.OutGame.StaminaBoost.StaminaBoostUtility$$ConvertStaminaBoostTypeIfNeeded")]
    private static delegate* unmanaged<StaminaBoostType, nint, StaminaBoostType> convertStaminaBoostTypeIfNeeded;

    //[GameSymbol("Command.Battle.BattleSystem$$SetPause")]
    //private static delegate* unmanaged<void*, CBool, nint, void> battleSystemSetPause;

    [GameSymbol("Command.Battle.BattleSystem$$OnBattleEnd")]
    private static delegate* unmanaged<void*, BattleResultType, nint, void> onBattleEnd;
}