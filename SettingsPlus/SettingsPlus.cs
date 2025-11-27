using ECGen.Generated;
using ECGen.Generated.Command;
using ECGen.Generated.Command.ApiNetwork.Api;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.Entity;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Chocobo;
using ECGen.Generated.Command.OutGame.Event;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ECGen.Generated.Command.OutGame.Party;
using ECGen.Generated.Command.OutGame.Shop;
using ECGen.Generated.Command.OutGame.Synthesis;
using ECGen.Generated.Command.Title;
using ECGen.Generated.Command.UI;
using ECGen.Generated.Command.Work;
using ECGen.Generated.Google.Protobuf.Collections;
using ECGen.Generated.System.Collections.Generic;
using ECGen.Generated.TMPro;
using ImGuiNET;
using Materia;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
using Materia.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
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
        pluginServiceManager.EventHandler.Dispose += Dispose;

        if (Config.EnableStaticCamera)
            SetupStandardCameraHook?.Enable();
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
        if (Config.EnableSynthesisRarity)
            RefreshSynthesisViewHook?.Enable();
        if (Config.InterjectionDisplayLimit != 3)
            GetInterjectionDisplayLimitHook?.Enable();
        if (Config.EnableQuickChocoboosters)
            ChocoboExpeditionShortenItemModalPresenterSetupHook?.Enable();
        if (Config.EnableSkipLogo)
            PlayBeginAnimationAsyncHook?.Enable();

        PluginServiceManager = pluginServiceManager;
    }

    public void Update()
    {
        UpdateUISettings();
        UpdateBattleTransitionInfo();
        UpdateAudioFocus();
    }

    public void UpdateUISettings()
    {
        if (Config.EnableSkipTitleAnimations && SceneBehaviourManager.GetCurrentSceneBehaviour<TitleSceneBehaviour>() is { } title && Il2CppType<TitleContent>.Is(title.NativePtr->currentTitleContent, out var titleContent))
            titleContentSkipTimeline(titleContent, 0);

        var currentModal = ModalManager.Instance?.CurrentModal;
        switch (currentModal?.Type.FullName)
        {
            case "Command.Battle.ContinueModalPresenter" when Config.DisableContinueModal:
                var continueModal = (ContinueModalPresenter*)currentModal.NativePtr;
                GameInterop.TapButton(continueModal->consumptionStoneField->cancelButton);
                return;
            case "Command.OutGame.Shop.ShopCheckExchangeModalPresenter" when Config.EnableExchangeLimit:
                UpdateExchangeMax();
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
                foreach (var p in synthesisTop->synthesisContentGroup->nowSynthesisContentModel->synthesisCellModels->PtrEnumerable.Where(p => p.ptr->craftType->GetValue() == CraftType.Materia))
                {
                    switch (p.ptr->synthesisViewType->GetValue())
                    {
                        case SynthesisViewType.Synthesis:
                        case SynthesisViewType.Acceptance:
                            var synthesisStore = (SynthesisWork.SynthesisStore*)p.ptr->synthesisInfo->value;
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
            var battleServerProxy = ((BattleServerProxy.StaticFields*)Il2CppType<BattleServerProxy>.NativePtr->staticFields)->instance;
            if (battleServerProxy == null
                || SceneBehaviourManager.GetCurrentSceneBehaviour<OutGameSceneBehaviour>() == null
                || ScreenManager.Instance is not { InTransition: false } screenManager
                || battleServerProxy->roomStatus->GetValue() != MultiRoomStatus.None
                || screenManager.IsCurrentScreen<MultiAreaBattleMatchingRoomScreenPresenter>())
                return;

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

            if (isValid && screenManager.CurrentScreen is { } screen)
            {
                PluginServiceManager.Log.Info($"Transitioning: {battleTransitionInfo}");

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

    private static void UpdateAudioFocus()
    {
        if (!Config.EnableAudioFocus) return;

        var device = GameInterop.GetSingletonMonoBehaviourInstance<Device>();
        if (device != null)
            SetMute(device->focusMode->GetValue() == Device.FocusMode.Off);
    }

    private static void UpdateExchangeMax()
    {
        if (ModalManager.Instance?.GetCurrentModal<ShopCheckExchangeModalPresenter>() is not { } exchangeModal) return;

        var stepper = exchangeModal.NativePtr->view->numericStepper;
        if (!GameInterop.IsGameObjectActive(stepper) || stepper->internalMaxCount != stepper->internalSelectableMaxCount) return;

        foreach (var p in exchangeModal.NativePtr->view->shopExchangeItemDataScroller->models->PtrEnumerable)
        {
            if (!Il2CppType<ShopExchangeItemDataCellModel>.Is(p.ptr, out var model)
                || !Il2CppType<RewardWork.ConsumptionSetConsumptionRelStore>.Is(model->ConsumptionSetConsumptionRelInfo, out var consumptionStore)
                || !Il2CppType<RewardWork.RewardStore>.Is(consumptionStore->rewardInfo, out var rewardStore)
                || (RewardType)rewardStore->masterReward->rewardType != RewardType.Item)
                continue;

            var possessionCount = WorkManager.GetItemStore(rewardStore->masterReward->targetId)->count;
            var maxPurchasable = possessionCount / consumptionStore->masterConsumptionSetConsumptionRel->consumptionCount;
            if (maxPurchasable >= stepper->selectableMaxCount) break;

            stepper->selectableMaxCount = maxPurchasable;
            stepper->internalSelectableMaxCount = maxPurchasable;
            break;
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

        var i = Config.InterjectionDisplayLimit;
        ImGui.SetNextItemWidth(ImGui.GetFrameHeight());
        if (ImGui.DragInt("Login Popup Limit", ref i, 0.1f, 0, 30))
        {
            if (i != 3)
                GetInterjectionDisplayLimitHook?.Enable();
            else
                GetInterjectionDisplayLimitHook?.Disable();
            Config.InterjectionDisplayLimit = i;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Changes the max number of daily popups (E.g. \"New Draw Available!\") (Default: 3)");

        var b = Config.EnableAudioFocus;
        if (ImGui.Checkbox("Enable Audio Focus", ref b))
        {
            Config.EnableAudioFocus = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Toggles the audio depending on game window focus");

        b = Config.EnableStaticCamera;
        if (ImGui.Checkbox("Enable Static Camera", ref b))
        {
            SetupStandardCameraHook?.Toggle();
            Config.EnableStaticCamera = b;
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
        ImGuiEx.SetItemTooltip("Does not consider character specific parts when displaying if a weapon\ncan be overboosted and only displays the notification icon if it can.\nAlso disables the overboost notification on Highwind collection items.");

        b = Config.DisableHiddenData;
        if (ImGui.Checkbox("Reveal Hidden Dungeon Bosses", ref b))
        {
            AnotherDungeonBossCellModelCtorHook?.Toggle();
            SetEnemyInfoAsyncHook?.Toggle();
            Config.DisableHiddenData = b;
            Config.Save();
        }

        b = Config.EnableSkipLogo;
        if (ImGui.Checkbox("Skip Startup Logo", ref b))
        {
            PlayBeginAnimationAsyncHook?.Toggle();
            Config.EnableSkipLogo = b;
            Config.Save();
        }

        b = Config.EnableSkipTitleAnimations;
        if (ImGui.Checkbox("Skip Title Animations", ref b))
        {
            Config.EnableSkipTitleAnimations = b;
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

        b = Config.EnableSynthesisRarity;
        if (ImGui.Checkbox("Reveal Synthesis Rarity", ref b))
        {
            RefreshSynthesisViewHook?.Toggle();
            Config.EnableSynthesisRarity = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Adds the rarity of a synthesis to its name");

        b = Config.EnableBattleReselection;
        if (ImGui.Checkbox("Auto Select Previous Battle", ref b))
        {
            Config.EnableBattleReselection = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Transitions back to the party selection screen after most battles");

        b = Config.EnableQuickChocoboosters;
        if (ImGui.Checkbox("Enable Quick Chocoboosters", ref b))
        {
            ChocoboExpeditionShortenItemModalPresenterSetupHook?.Toggle();
            ChocoboExpeditionShortenItemModalPresenterRefreshHook?.Disable();
            Config.EnableQuickChocoboosters = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Sets the default number of selected chocoboosters to the max without overcapping");

        b = Config.EnableExchangeLimit;
        if (ImGui.Checkbox("Cap Exchange to Max Purchasable", ref b))
        {
            Config.EnableExchangeLimit = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Limits the max selection for exchangeable items to the max purchasable");

        b = ApiRequestAsync9Hook?.IsEnabled ?? false;
        if (ImGui.Checkbox("Save Account Data On Next Login", ref b))
            ApiRequestAsync9Hook?.Toggle();
        ImGuiEx.SetItemTooltip("Saves account data to Materia's folder in \"accountData.json\"\nupon next login from the title screen. This setting is not saved.");

        ImGui.End();
    }

    public void Dispose()
    {
        Config?.Save();
    }

    [GameSymbol("Command.SteamWindowUtility$$SetResolution")]
    private static delegate* unmanaged<int, int, int, nint, void> setResolution;

    private delegate void PlayBeginAnimationAsyncDelegate(void* a1, nint method);
    [GameSymbol("Command.EntryPointSceneBehaviour.<PlayBeginAnimationAsync>d__24$$MoveNext", EnableHook = false)]
    private static IMateriaHook<PlayBeginAnimationAsyncDelegate>? PlayBeginAnimationAsyncHook;
    private static void PlayBeginAnimationAsyncDetour(void* a1, nint method) { }

    [GameSymbol("Command.Title.TitleContent$$SkipTimeline")]
    private static delegate* unmanaged<TitleContent*, nint, void> titleContentSkipTimeline;

    private delegate void SetupStandardCameraDelegate(nint battleSystem, int cameraGroup, nint cameraName, CBool isDynamicCamera, nint method);
    [GameSymbol("Command.Battle.BattleSystem$$SetupStandardCamera_1", EnableHook = false)]
    private static IMateriaHook<SetupStandardCameraDelegate>? SetupStandardCameraHook;
    private static void SetupStandardCameraDetour(nint battleSystem, int cameraGroup, nint cameraName, CBool isDynamicCamera, nint method) =>
        SetupStandardCameraHook!.Original(battleSystem, cameraGroup, cameraName, false, method);

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

    // TODO: Inlining sometimes breaks this
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

    private delegate void GridWeaponSelectModelCtorDelegate(GridWeaponSelectModel* gridWeaponSelectModel, int index, WeaponWork.WeaponStore* info, CBool selectEnable, ICharacterInfo* equipCharacter, CBool isSelected, CBool isSelectAndEquipment, CBool isEventBonusActive, CBool isDisplayEquipBadge, CBool isShowEnhanceButton, CBool isNew, CBool showEnhanceNotice, CBool isDisplayWeapon, long filterBonusEventBaseId, Unmanaged_Array<long>* eventBonusTargetEventBaseIds, CBool isDisplayArmouryBadge, CBool isDisplayPickupBadge, int armouryPoint, nint method);
    [GameSymbol("Command.OutGame.GridWeaponSelectModel$$.ctor", EnableHook = false)]
    private static IMateriaHook<GridWeaponSelectModelCtorDelegate>? GridWeaponSelectModelCtorHook;
    private static void GridWeaponSelectModelCtorDetour(GridWeaponSelectModel* gridWeaponSelectModel, int index, WeaponWork.WeaponStore* info, CBool selectEnable, ICharacterInfo* equipCharacter, CBool isSelected, CBool isSelectAndEquipment, CBool isEventBonusActive, CBool isDisplayEquipBadge, CBool isShowEnhanceButton, CBool isNew, CBool showEnhanceNotice, CBool isDisplayWeapon, long filterBonusEventBaseId, Unmanaged_Array<long>* eventBonusTargetEventBaseIds, CBool isDisplayArmouryBadge, CBool isDisplayPickupBadge, int armouryPoint, nint method)
    {
        if (showEnhanceNotice)
        {
            var medalItem = WorkManager.GetItemStore(info->masterWeapon->weaponMedalItemId);
            if (medalItem != null)
            {
                if (info->weaponUpgradeLimit < 20)
                {
                    var neededMedals = info->rarityUpMedalCount > 0 ? info->rarityUpMedalCount : 200;
                    if (neededMedals > 0)
                        showEnhanceNotice = medalItem->count >= neededMedals;
                }
                else
                {
                    showEnhanceNotice = false;
                }
            }
        }

        GridWeaponSelectModelCtorHook!.Original(gridWeaponSelectModel, index, info, selectEnable, equipCharacter, isSelected, isSelectAndEquipment, isEventBonusActive, isDisplayEquipBadge, isShowEnhanceButton, isNew, showEnhanceNotice, isDisplayWeapon, filterBonusEventBaseId, eventBonusTargetEventBaseIds, isDisplayArmouryBadge, isDisplayPickupBadge, armouryPoint, method);
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

    [GameSymbol("Command.AudioVolumeController$$SetMute")]
    private static delegate* unmanaged<void*, CBool, nint, void> setMute;

    private static void SetMute(bool b)
    {
        var audioManager = GameInterop.GetSingletonMonoBehaviourInstance<AudioManager>();
        if (audioManager == null) return;

        if (audioManager->bGMVolumeController != null)
            setMute(audioManager->bGMVolumeController, b, 0);
        if (audioManager->sEVolumeController != null)
            setMute(audioManager->sEVolumeController, b, 0);
        if (audioManager->voiceVolumeController != null)
            setMute(audioManager->voiceVolumeController, b, 0);
    }

    [GameSymbol("TMPro.TMP_Text$$SetText")]
    private static delegate* unmanaged<TMP_Text*, Unmanaged_String*, CBool, nint, void> tmpSetText;

    private delegate void RefreshSynthesisViewDelegate(SynthesisCellView* synthesisCellView, nint method);
    [GameSymbol("Command.OutGame.Synthesis.SynthesisCellView$$RefreshSynthesisView", EnableHook = false)]
    private static IMateriaHook<RefreshSynthesisViewDelegate>? RefreshSynthesisViewHook;
    private static void RefreshSynthesisViewDetour(SynthesisCellView* synthesisCellView, nint method)
    {
        RefreshSynthesisViewHook!.Original(synthesisCellView, method);

        try
        {
            if (!Il2CppType<SynthesisWork.SynthesisStore>.Is(synthesisCellView->currentSynthesisCellModel->synthesisInfo->value, out var synthesisStore)) return;
            tmpSetText((TMP_Text*)synthesisCellView->nameText, GameInterop.CreateString($"{(int)synthesisStore->qualityType}* {synthesisCellView->nameText->m_text->ToString()}"), true, 0);
        }
        catch (Exception e)
        {
            PluginServiceManager.Log.Error(e);
        }
    }

    private delegate int GetInterjectionDisplayLimitDelegate(ConfigWork.ConfigStore* configStore, nint method);
    [GameSymbol("Command.Work.ConfigWork.ConfigStore$$get_InterjectionDisplayLimit", EnableHook = false)]
    private static IMateriaHook<GetInterjectionDisplayLimitDelegate>? GetInterjectionDisplayLimitHook;
    private static int GetInterjectionDisplayLimitDetour(ConfigWork.ConfigStore* configStore, nint method) => Config.InterjectionDisplayLimit;

    private delegate void ChocoboExpeditionShortenItemModalPresenterSetupDelegate(ChocoboExpeditionShortenItemModalPresenter* chocoboExpeditionShortenItemModalPresenter, ItemCountSelectModel* itemModel, IChocoboExpeditionDeckInfo* chocoboExpeditionDeckInfo, nint method);
    [GameSymbol("Command.OutGame.Chocobo.ChocoboExpeditionShortenItemModalPresenter$$Setup", EnableHook = false)]
    private static IMateriaHook<ChocoboExpeditionShortenItemModalPresenterSetupDelegate>? ChocoboExpeditionShortenItemModalPresenterSetupHook;
    private static void ChocoboExpeditionShortenItemModalPresenterSetupDetour(ChocoboExpeditionShortenItemModalPresenter* chocoboExpeditionShortenItemModalPresenter, ItemCountSelectModel* itemModel, IChocoboExpeditionDeckInfo* chocoboExpeditionDeckInfo, nint method)
    {
        ChocoboExpeditionShortenItemModalPresenterRefreshHook?.Enable();
        ChocoboExpeditionShortenItemModalPresenterSetupHook!.Original(chocoboExpeditionShortenItemModalPresenter, itemModel, chocoboExpeditionDeckInfo, method);
    }

    private delegate void ChocoboExpeditionShortenItemModalPresenterRefreshDelegate(ChocoboExpeditionShortenItemModalPresenter* chocoboExpeditionShortenItemModalPresenter, nint method);
    [GameSymbol("Command.OutGame.Chocobo.ChocoboExpeditionShortenItemModalPresenter$$Refresh", EnableHook = false)]
    private static IMateriaHook<ChocoboExpeditionShortenItemModalPresenterRefreshDelegate>? ChocoboExpeditionShortenItemModalPresenterRefreshHook;
    private static void ChocoboExpeditionShortenItemModalPresenterRefreshDetour(ChocoboExpeditionShortenItemModalPresenter* chocoboExpeditionShortenItemModalPresenter, nint method)
    {
        chocoboExpeditionShortenItemModalPresenter->itemModel->selectedCount = Math.Min(chocoboExpeditionShortenItemModalPresenter->userRemainUsableItemCount, chocoboExpeditionShortenItemModalPresenter->itemModel->displayMaxCount - 1);
        ChocoboExpeditionShortenItemModalPresenterRefreshHook!.Original(chocoboExpeditionShortenItemModalPresenter, method);
        ChocoboExpeditionShortenItemModalPresenterRefreshHook.Disable();
    }

    private delegate void ApiRequestAsync9Delegate(void* a1, BaseResponse* baseResponse, nint method);
    [GameSymbol("Command.ApiNetwork.Api.ApiCommon.<>c__DisplayClass9_0$$<RequestAsync>b__1", EnableHook = false)]
    private static IMateriaHook<ApiRequestAsync9Delegate>? ApiRequestAsync9Hook;
    private static void ApiRequestAsync9Detour(void* a1, BaseResponse* baseResponse, nint method)
    {
        ApiRequestAsync9Hook!.Original(a1, baseResponse, method);
        if (!Il2CppType<ApiResponse>.Is(baseResponse, out var apiResponse) || apiResponse->common == null || apiResponse->common->user->update == null || apiResponse->postPvtUserTitle == null) return;
        Util.SaveJsonToFile(Path.Combine(Util.MateriaDirectory.FullName, "accountData.json"), ConvertCommonResponseToJObject(apiResponse->common));
        ApiRequestAsync9Hook.Disable();
    }

    private static JObject ConvertCommonResponseToJObject(CommonResponse* commonResponse)
    {
        var json = new JObject();

        try
        {
            var tables = *commonResponse->user->update;
            foreach (var f in tables.GetType().GetFields().Where(f => f.FieldType.HasElementType))
            {
                var t = f.FieldType.GetElementType()!;
                if (!t.IsGenericType || t.GenericTypeArguments.Length != 1) continue;

                var v = f.GetValue(tables);
                if (v is not Pointer p) continue;

                var ptr = (RepeatedField<nint>*)Pointer.Unbox(p);
                if (ptr == null || ptr->array->size == 0) continue;

                var subJson = new JArray();
                var subType = t.GenericTypeArguments.First();
                var subFields = subType.GetFields().Where(sF => sF.Name != "userId").ToArray();
                foreach (var subPtr in ptr->array->Enumerable)
                {
                    if (Marshal.PtrToStructure(subPtr, subType) is not { } o) continue;

                    var item = new JObject();
                    foreach (var sF in subFields)
                    {
                        var sFType = sF.FieldType;
                        if (sFType == typeof(nint)) continue;

                        if (!sFType.HasElementType)
                        {
                            var sV = sF.GetValue(o);
                            item[sF.Name] = sV != null ? JToken.FromObject(sV) : null;
                        }
                        else
                        {
                            var elementType = sFType.GetElementType();
                            if (elementType != typeof(Unmanaged_String) || sF.GetValue(o) is not { } sV) continue;

                            var strPtr = (Unmanaged_String*)Pointer.Unbox(sV);
                            if (strPtr == null) continue;

                            item[sF.Name] = strPtr->ToString();
                        }
                    }
                    subJson.Add(item);
                }
                json[f.Name] = subJson;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return json;
    }
}