using ECGen.Generated;
using ImGuiNET;
using Materia;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
using Materia.Utilities;
using System;
using System.Linq;
using System.Numerics;
using ECGen.Generated.Command;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Party;
using ECGen.Generated.Command.OutGame.Synthesis;
using ECGen.Generated.Command.Work;
using ECGen.Generated.System.Collections.Generic;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ECGen.Generated.Command.OutGame.Event;
using ECGen.Generated.Command.UI;
using BattleSystem = Materia.Game.BattleSystem;
using ModalManager = Materia.Game.ModalManager;
using SceneBehaviourManager = Materia.Game.SceneBehaviourManager;
using ScreenManager = Materia.Game.ScreenManager;
using WorkManager = Materia.Game.WorkManager;

namespace SettingsPlus;

[Injection]
public unsafe class SettingsPlus : IMateriaPlugin
{
    public string Name => "Settings+";
    public string Description => "Displays additional settings";
    public static PluginServiceManager PluginServiceManager { get; private set; } = null!;
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();

    private bool draw = false;
    private readonly int[] res = [ 960, 540 ];
    private (TransitionType TransitionType, long Id) battleTransitionInfo;

    public SettingsPlus(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
        pluginServiceManager.EventHandler.Dispose += Config.Save;

        if (Config.EnableStaticCamera)
            SetupStandardCameraHook?.Enable();
        if (Config.DisableActionCamera)
            IsValidActionCameraWaitingTimeHook?.Enable();
        if (Config.DisableCharacterParts)
            AdequatelyWeaponMedalItemHook?.Enable();
        if (Config.EnableBetterWeaponNotificationIcon)
        {
            GridWeaponSelectModelCtorHook?.Enable();
            CanEvolutionHighwindKeyItemHook?.Enable();
            HighwindKeyItemSelectUpdateAllModelHook?.Enable();
        }
        if (Config.DisableHiddenData)
        {
            AnotherDungeonBossCellModelCtorHook?.Enable();
            SetEnemyInfoAsyncHook?.Enable();
        }
        if (Config.EnableRememberLastSelectedMateriaRecipe)
            SynthesisSelectScreenSetupParameterCtorHook?.Enable();

        PluginServiceManager = pluginServiceManager;
    }

    public void Update()
    {
        UpdateUISettings();
        UpdateBattleTransitionInfo();
    }

    public void UpdateUISettings()
    {
        var currentModal = ModalManager.Instance?.CurrentModal;
        switch (currentModal?.Type.FullName)
        {
            case "Command.Battle.ContinueModalPresenter" when Config.DisableContinueModal:
                var continueModal = (ContinueModalPresenter*)currentModal.NativePtr;
                GameInterop.TapButton(continueModal->consumptionStoneField->cancelButton);
                return;
            case null:
                break;
            default:
                return;
        }

        var currentScreen = ScreenManager.Instance?.CurrentScreen;
        switch (currentScreen?.Type.FullName)
        {
            case "Command.OutGame.Synthesis.SynthesisTopScreenPresenter" when Config.EnableRememberLastSelectedMateriaRecipe && lastMateriaRecipeId == 0:
                var synthesisTop = (SynthesisTopScreenPresenter*)currentScreen.NativePtr;
                foreach (var p in synthesisTop->synthesisContentGroup->nowSynthesisContent->displayCellPresenterArray->PtrEnumerable.Where(p => p.ptr->cellModel->craftType->GetValue() == CraftType.Materia))
                {
                    switch (p.ptr->view->currentViewType)
                    {
                        case SynthesisViewType.Synthesis:
                        case SynthesisViewType.Acceptance:
                            var synthesisStore = (SynthesisWork.SynthesisStore*)p.ptr->cellModel->synthesisInfo->value;
                            var materiaRecipeInfo = (MateriaWork.MateriaRecipeStore*)synthesisStore->materiaRecipeInfo;
                            lastMateriaRecipeId = materiaRecipeInfo->masterMateriaRecipe->id;
                            break;
                    }

                    if (lastMateriaRecipeId != 0) break;
                }
                break;
            case "Command.OutGame.Synthesis.SynthesisSelectScreenPresenter" when Config.EnableRememberLastSelectedMateriaRecipe:
                var synthesisSelect = (SynthesisSelectScreenPresenter*)currentScreen.NativePtr;
                if (synthesisSelect->screenSetupParameter->synthesisRecipeViewType != SynthesisRecipeViewType.Materia) break;
                var materiaRecipeStore = (MateriaWork.MateriaRecipeStore*)synthesisSelect->selectRecipe;
                lastMateriaRecipeId = materiaRecipeStore->masterMateriaRecipe->id;
                break;
            case "Command.OutGame.Party.PartySelectScreenPresenter" when Config.DisableRenamedRecommendedParty:
            case "Command.OutGame.Party.SoloPartySelectScreenPresenter" when Config.DisableRenamedRecommendedParty:
            case "Command.OutGame.Party.StoryPartySelectScreenPresenter" when Config.DisableRenamedRecommendedParty:
            case "Command.OutGame.Party.MultiPartySelectScreenPresenter" when Config.DisableRenamedRecommendedParty:
            case "Command.OutGame.MultiBattle.MultiAreaBattlePartySelectPresenter" when Config.DisableRenamedRecommendedParty:
                var partySelect = (PartySelectScreenPresenterBase<PartySelectScreenSetupParameter>*)currentScreen.NativePtr;
                var partyInfo = partySelect->partySelect->selectPartyInfo;
                if (!partySelect->view->copyButton->IsActive())
                    partySelect->view->recommendFormationButton->ChangeActive(partyInfo == null || partyInfo->userPartyName->stringLength == 0 || partyInfo->userPartyName->Equals(partyInfo->defaultPartyName));
                break;
            case "Command.OutGame.Party.PartyEditTopScreenPresenter" when Config.DisableRenamedRecommendedParty:
            case "Command.OutGame.Party.PartyEditTopScreenMultiPresenter" when Config.DisableRenamedRecommendedParty:
            case "Command.OutGame.Party.MultiAreaBattlePartyEditPresenter" when Config.DisableRenamedRecommendedParty:
                if (!Il2CppType<PartyEditTopScreenPresenterBase>.Is(currentScreen.NativePtr, out var partyEdit)) break;
                var party = partyEdit->currentPartyInfo;
                partyEdit->statusThumbnailPresenter->recommendButton->ChangeActive(party == null || party->userPartyName->stringLength == 0 || party->userPartyName->Equals(party->defaultPartyName));
                break;
        }

        if (BattleSystem.Instance is { } battleSystem && BattleHUD.Instance is { } battleHUD)
        {
            if (Config.EnableSkipBattleCutscenes)
            {
                switch (battleHUD.CurrentStatus)
                {
                    case HUD.Status.BossEncounterCutScene:
                    case HUD.Status.BossDefeatCutScene:
                    case HUD.Status.SpecialSkill when battleSystem.IsLimitBreak && GameInterop.GetSharedMonoBehaviourInstance<MoviePlayer>() is var moviePlayer && moviePlayer != null && moviePlayer->movieStatus->GetValue() == MoviePlayer.MovieStatus.Play:
                        GameInterop.TapKeyAction(KeyAction.Skip);
                        break;
                }
            }
        }
    }

    private void UpdateBattleTransitionInfo()
    {
        if (!Config.EnableBattleReselection) return;

        if (battleTransitionInfo.Id == 0)
        {
            if (SceneBehaviourManager.GetCurrentSceneBehaviour<BattleSceneBehaviour>() is not { } battleSceneBehaviour) return;

            var setupParameter = battleSceneBehaviour.NativePtr->@class->staticFields->NextSetupParameter;
            if (setupParameter == null) return;

            if (setupParameter->areaBattleId != 0)
                battleTransitionInfo = (!setupParameter->isMulti ? TransitionType.AreaSoloBattle : TransitionType.AreaMultiBattle, setupParameter->areaBattleId);
            else if (setupParameter->eventAreaBattleId != 0)
                battleTransitionInfo = (!setupParameter->isMulti ? TransitionType.EventSoloBattle : TransitionType.Event, setupParameter->eventAreaBattleId);
            else
                battleTransitionInfo.Id = 1;
        }
        else
        {
            if (SceneBehaviourManager.GetCurrentSceneBehaviour<OutGameSceneBehaviour>() == null || ScreenManager.Instance is not { InTransition: false } screenManager) return;

            var isValid = false;
            switch (battleTransitionInfo.TransitionType)
            {
                case TransitionType.AreaSoloBattle:
                {
                    var store = WorkManager.GetSoloAreaBattleStore(battleTransitionInfo.Id);
                    if (store == null) break;

                    if (store->masterSoloAreaBattle->resetMaxWinCount > 0)
                    {
                        var f = (delegate* unmanaged<void*, Il2CppMethodInfo*, long>)store->@class->vtable.get_RemainingChallengeCount.methodPtr;
                        isValid = f(store, store->@class->vtable.get_RemainingChallengeCount.method) > 0;
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;
                }
                case TransitionType.AreaMultiBattle:
                {
                    var store = WorkManager.GetMultiAreaBattleStore(battleTransitionInfo.Id);
                    if (store == null) break;

                    if (store->masterMultiAreaBattle->resetMaxWinCount > 0)
                    {
                        var f = (delegate* unmanaged<void*, Il2CppMethodInfo*, long>)store->@class->vtable.get_RemainingChallengeCount.methodPtr;
                        isValid = f(store, store->@class->vtable.get_RemainingChallengeCount.method) > 0;
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;
                }
                case TransitionType.EventSoloBattle:
                {
                    var store = WorkManager.GetEventSoloBattleStore(battleTransitionInfo.Id);
                    if (store == null) break;

                    if (store->masterEventSoloBattle->challengeCountMax > 0)
                    {
                        var f = (delegate* unmanaged<void*, Il2CppMethodInfo*, long>)store->@class->vtable.get_RemainingChallengeCount.methodPtr;
                        isValid = f(store, store->@class->vtable.get_RemainingChallengeCount.method) > 0;
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;
                }
                case TransitionType.Event:
                {
                    var store = WorkManager.GetEventMultiBattleStore(battleTransitionInfo.Id);
                    if (store == null) break;

                    if (store->masterEventMultiBattle->challengeCountMax > 0)
                    {
                        var f = (delegate* unmanaged<void*, Il2CppMethodInfo*, long>)store->@class->vtable.get_RemainingChallengeCount.methodPtr;
                        isValid = f(store, store->@class->vtable.get_RemainingChallengeCount.method) > 0;
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;
                }
                case TransitionType.EventMultiBattle:
                    isValid = true;
                    break;
            }

            if (isValid && screenManager.CurrentScreen is { } screen && !Il2CppType<MultiAreaBattleMatchingRoomScreenPresenter>.IsAssignableFrom(screen.NativePtr))
            {
                switch (battleTransitionInfo.TransitionType)
                {
                    case TransitionType.EventMultiBattle:
                        TransitionToEventMultiBattle(screen.NativePtr, battleTransitionInfo.Id);
                        break;
                    case TransitionType.Event:
                        if (screen.NativePtr->@class->parent != null && screen.NativePtr->@class->parent->GetName() == "EventAreaScreenPresenterBase`1")
                            goto case TransitionType.EventMultiBattle;

                        var eventMultiBattleStore = WorkManager.GetEventMultiBattleStore(battleTransitionInfo.Id);
                        ScreenManager.TransitionAsync(TransitionType.Event, eventMultiBattleStore->masterEventMultiBattle->eventBaseId);
                        battleTransitionInfo.TransitionType = TransitionType.EventMultiBattle;
                        return;
                    default:
                        ScreenManager.TransitionAsync(battleTransitionInfo.TransitionType, battleTransitionInfo.Id);
                        break;
                }
            }

            battleTransitionInfo = default;
        }
    }

    private static void TransitionToEventMultiBattle(ScreenBase<ScreenSetupParameter>* screen, long id)
    {
        var parent = screen->@class->parent;
        if (parent == null || parent->GetName() != "EventAreaScreenPresenterBase`1") return;

        for (int i = 0; i < parent->methodCount; i++)
        {
            var method = parent->methods[i];
            if (Util.ReadCString(method->name) != "GoToMultiPartyScreen") continue;

            var eventMultiBattleStore = WorkManager.GetEventMultiBattleStore(id);
            if (eventMultiBattleStore == null) return;

            var model = new EventAreaListModel { eventMultiBattleInfo = (IEventMultiBattleInfo*)eventMultiBattleStore };
            var f = (delegate* unmanaged<void*, EventAreaListModel*, Il2CppMethodInfo*, void>)method->methodPtr;
            f(screen, &model, method);
        }
    }

    public void Draw()
    {
        if (!draw) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(300, 200) * ImGuiEx.Scale, new Vector2(10000));
        ImGui.Begin("Settings+", ref draw);

        if (ImGui.Button("Set") && setResolution != null)
            setResolution(1, res[0], res[1], 0);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
        if (ImGui.InputInt2("Resolution", ref res[0]))
        {
            res[0] = Math.Min(Math.Max(res[0], 960), 10000);
            res[1] = Math.Min(Math.Max(res[1], 540), 10000);
        }

        var b = Config.EnableStaticCamera;
        if (ImGui.Checkbox("Enable Static Camera", ref b))
        {
            SetupStandardCameraHook?.Toggle();
            Config.EnableStaticCamera = b;
            Config.Save();
        }

        b = Config.DisableActionCamera;
        if (ImGui.Checkbox("Disable Action Camera", ref b))
        {
            IsValidActionCameraWaitingTimeHook?.Toggle();
            Config.DisableActionCamera = b;
            Config.Save();
        }

        b = Config.DisableCharacterParts;
        if (ImGui.Checkbox("Disable Character Parts", ref b))
        {
            AdequatelyWeaponMedalItemHook?.Toggle();
            Config.DisableCharacterParts = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Prevents the optimize button from using\ncharacter specific parts when overboosting");

        b = Config.DisableRenamedRecommendedParty;
        if (ImGui.Checkbox("Disable Rcmd. on Renamed Parties", ref b))
        {
            Config.DisableRenamedRecommendedParty = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("You can delete a party to reset its name");

        b = Config.DisableContinueModal;
        if (ImGui.Checkbox("Disable Continuing Failed Battles", ref b))
        {
            Config.DisableContinueModal = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Automatically retires when failing a battle");

        b = Config.EnableBetterWeaponNotificationIcon;
        if (ImGui.Checkbox("Enable Better Enhance Notif. Icon", ref b))
        {
            GridWeaponSelectModelCtorHook?.Toggle();
            CanEvolutionHighwindKeyItemHook?.Toggle();
            HighwindKeyItemSelectUpdateAllModelHook?.Toggle();
            Config.EnableBetterWeaponNotificationIcon = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Does not consider character specific parts when displaying if a weapon\ncan be overboosted and only displays the notification icon if it can.\nAlso disables the overboost notification on Highwind collection items");

        b = Config.DisableHiddenData;
        if (ImGui.Checkbox("Reveal Hidden Dungeon Bosses", ref b))
        {
            AnotherDungeonBossCellModelCtorHook?.Toggle();
            SetEnemyInfoAsyncHook?.Toggle();
            Config.DisableHiddenData = b;
            Config.Save();
        }

        b = Config.EnableSkipBattleCutscenes;
        if (ImGui.Checkbox("Auto Skip Battle Cutscenes", ref b))
        {
            Config.EnableSkipBattleCutscenes = b;
            Config.Save();
        }

        b = Config.EnableRememberLastSelectedMateriaRecipe;
        if (ImGui.Checkbox("Auto Select Last Materia Recipe", ref b))
        {
            SynthesisSelectScreenSetupParameterCtorHook?.Toggle();
            Config.EnableRememberLastSelectedMateriaRecipe = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Does not currently work with certain recipes!");

        b = Config.EnableBattleReselection;
        if (ImGui.Checkbox("Auto Select Previous Battle", ref b))
        {
            Config.EnableBattleReselection = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Transitions back to the party selection screen after most battles");

        ImGui.End();
    }

    [GameSymbol("Command.SteamWindowUtility$$SetResolution")]
    private static delegate* unmanaged<int, int, int, nint, void> setResolution;

    private delegate void SetupStandardCameraDelegate(nint battleSystem, int cameraGroup, nint cameraName, CBool isDynamicCamera, nint method);
    [GameSymbol("Command.Battle.BattleSystem$$SetupStandardCamera_1", EnableHook = false)]
    private static IMateriaHook<SetupStandardCameraDelegate>? SetupStandardCameraHook;
    private static void SetupStandardCameraDetour(nint battleSystem, int cameraGroup, nint cameraName, CBool isDynamicCamera, nint method) =>
        SetupStandardCameraHook!.Original(battleSystem, cameraGroup, cameraName, false, method);

    private delegate CBool IsValidActionCameraWaitingTimeDelegate(nint cameraManager, nint method);
    [GameSymbol("Command.Battle.CameraManager$$IsValidActionCameraWaitingTime", EnableHook = false)]
    private static IMateriaHook<IsValidActionCameraWaitingTimeDelegate>? IsValidActionCameraWaitingTimeHook;
    private static CBool IsValidActionCameraWaitingTimeDetour(nint cameraManager, nint method) => true;

    private delegate void AdequatelyWeaponMedalItemDelegate(nint weaponEnhancePanel, Unmanaged_List<ItemCountSelectModel>* weaponMedalModels, long gil, nint method);
    [GameSymbol("Command.OutGame.Weapon.WeaponEnhancePanel$$AdequatelyWeaponMedalItem", EnableHook = false)]
    private static IMateriaHook<AdequatelyWeaponMedalItemDelegate>? AdequatelyWeaponMedalItemHook;
    private static void AdequatelyWeaponMedalItemDetour(nint weaponEnhancePanel, Unmanaged_List<ItemCountSelectModel>* weaponMedalModels, long gil, nint method)
    {
        // TODO: Extremely hacky but it works
        var prevSize = weaponMedalModels->size;
        weaponMedalModels->size = 1;
        AdequatelyWeaponMedalItemHook!.Original(weaponEnhancePanel, weaponMedalModels, gil, method);
        weaponMedalModels->size = prevSize;
    }

    private delegate void AnotherDungeonBossCellModelCtorDelegate(nint anotherDungeonBossCellModel, nint anotherBattleInfo, nint anotherBossInfos, CBool isWin, CBool showBossLabel, CBool isDisplayInfo, nint method);
    [GameSymbol("Command.OutGame.AnotherDungeon.AnotherDungeonBossCellModel$$.ctor", EnableHook = false)]
    private static IMateriaHook<AnotherDungeonBossCellModelCtorDelegate>? AnotherDungeonBossCellModelCtorHook;
    private static void AnotherDungeonBossCellModelCtorDetour(nint anotherDungeonBossCellModel, nint anotherBattleInfo, nint anotherBossInfos, CBool isWin, CBool showBossLabel, CBool isDisplayInfo, nint method) =>
        AnotherDungeonBossCellModelCtorHook!.Original(anotherDungeonBossCellModel, anotherBattleInfo, anotherBossInfos, isWin, showBossLabel, true, method);

    private delegate nint SetEnemyInfoAsyncDelegate(nint retstr, nint enemyThumbnail, nint token, nint enemyInfo, CBool isUnknown, CBool isBoss, int thumbnailTapType, nint method);
    [GameSymbol("Command.UI.EnemyThumbnail$$SetEnemyInfoAsync", EnableHook = false, ReturnPointer = true)]
    private static IMateriaHook<SetEnemyInfoAsyncDelegate>? SetEnemyInfoAsyncHook;
    private static nint SetEnemyInfoAsyncDetour(nint retstr, nint enemyThumbnail, nint token, nint enemyInfo, CBool isUnknown, CBool isBoss, int thumbnailTapType, nint method) =>
        SetEnemyInfoAsyncHook!.Original(retstr, enemyThumbnail, token, enemyInfo, false, isBoss, thumbnailTapType, method);

    // TODO: Inlining broke this
    private static long lastMateriaRecipeId;
    private delegate void SynthesisSelectScreenSetupParameterCtorDelegate(SynthesisSelectScreenSetupParameter* param, int selectDataIndex, int synthesisRecipeViewType, nint method);
    [GameSymbol("Command.OutGame.Synthesis.SynthesisSelectScreenSetupParameter$$.ctor", EnableHook = false)]
    private static IMateriaHook<SynthesisSelectScreenSetupParameterCtorDelegate>? SynthesisSelectScreenSetupParameterCtorHook;
    private static void SynthesisSelectScreenSetupParameterCtorDetour(SynthesisSelectScreenSetupParameter* param, int selectDataIndex, int synthesisRecipeViewType, nint method)
    {
        SynthesisSelectScreenSetupParameterCtorHook!.Original(param, selectDataIndex, synthesisRecipeViewType, method);
        if (synthesisRecipeViewType == 1)
            param->materiaRecipeId = lastMateriaRecipeId;
    }

    private delegate void GridWeaponSelectModelCtorDelegate(GridWeaponSelectModel* gridWeaponSelectModel, int index, WeaponWork.WeaponStore* info, CBool selectEnable, ICharacterInfo* equipCharacter, CBool isSelected, CBool isSelectAndEquipment, CBool isEventBonusActive, CBool isDisplayEquipBadge, CBool isShowEnhanceButton, CBool isNew, CBool showEnhanceNotice, CBool isDisplayWeapon, long filterBonusEventBaseId, nint method);
    [GameSymbol("Command.OutGame.GridWeaponSelectModel$$.ctor", EnableHook = false)]
    private static IMateriaHook<GridWeaponSelectModelCtorDelegate>? GridWeaponSelectModelCtorHook;
    private static void GridWeaponSelectModelCtorDetour(GridWeaponSelectModel* gridWeaponSelectModel, int index, WeaponWork.WeaponStore* info, CBool selectEnable, ICharacterInfo* equipCharacter, CBool isSelected, CBool isSelectAndEquipment, CBool isEventBonusActive, CBool isDisplayEquipBadge, CBool isShowEnhanceButton, CBool isNew, CBool showEnhanceNotice, CBool isDisplayWeapon, long filterBonusEventBaseId, nint method)
    {
        if (isShowEnhanceButton)
        {
            var medalItem = WorkManager.GetItemStore(info->masterWeapon->weaponMedalItemId);
            if (medalItem != null)
            {
                if (info->canUpgradeLimit)
                    info->canUpgradeLimit = medalItem->count >= 200;
                else if (info->canUpgradeRank)
                    info->canUpgradeRank = medalItem->count >= 200;
                isShowEnhanceButton = info->canUpgradeLimit || info->canUpgradeRank;
            }
        }

        GridWeaponSelectModelCtorHook!.Original(gridWeaponSelectModel, index, info, selectEnable, equipCharacter, isSelected, isSelectAndEquipment, isEventBonusActive, isDisplayEquipBadge, isShowEnhanceButton, isNew, showEnhanceNotice, isDisplayWeapon, filterBonusEventBaseId, method);
    }

    [GameSymbol("Command.OutGame.Highwind.HighwindTopScreenModel$$CanEvolutionHighwindKeyItem", EnableHook = false)]
    private static IMateriaHook<HasKeyItemEvolutionItemDelegate>? CanEvolutionHighwindKeyItemHook;
    private static CBool CanEvolutionHighwindKeyItemDetour(nint o, nint method) => false;

    private delegate CBool HasKeyItemEvolutionItemDelegate(nint o, nint method);
    [GameSymbol("Command.OutGame.Highwind.HighwindUtility$$HasKeyItemEvolutionItem_1", EnableHook = false)]
    private static IMateriaHook<HasKeyItemEvolutionItemDelegate>? HasKeyItemEvolutionItemHook;
    private static CBool HasKeyItemEvolutionItemDetour(nint o, nint method) => false;

    private delegate void HighwindKeyItemSelectUpdateAllModelDelegate(nint o, nint method);
    [GameSymbol("Command.OutGame.GridHighwindKeyItemSelect$$UpdateAllModel", EnableHook = false)]
    private static IMateriaHook<HighwindKeyItemSelectUpdateAllModelDelegate>? HighwindKeyItemSelectUpdateAllModelHook;
    private static void HighwindKeyItemSelectUpdateAllModelDetour(nint o, nint method)
    {
        HasKeyItemEvolutionItemHook?.Enable();
        HighwindKeyItemSelectUpdateAllModelHook!.Original(o, method);
        HasKeyItemEvolutionItemHook?.Disable();
    }
}