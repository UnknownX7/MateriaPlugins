using ECGen.Generated;
using ImGuiNET;
using Materia;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
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
using BattleSystem = Materia.Game.BattleSystem;
using ModalManager = Materia.Game.ModalManager;
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
    private readonly int[] res = { 960, 540 };

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
            GridWeaponSelectModelCtorHook?.Enable();
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
    }

    public void UpdateUISettings()
    {
        var currentModal = ModalManager.Instance?.CurrentModal;
        switch (currentModal?.Type.FullName)
        {
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
                if (partyInfo != null && partyInfo->userPartyName->stringLength > 0 && !partyInfo->userPartyName->Equals(partyInfo->defaultPartyName))
                {
                    if (partySelect->view->recommendFormationButton->IsActive())
                        partySelect->view->recommendFormationButton->SetActive(false);
                }
                else if (!partySelect->view->recommendFormationButton->IsActive())
                {
                    partySelect->view->recommendFormationButton->SetActive(true);
                }
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

        b = Config.EnableBetterWeaponNotificationIcon;
        if (ImGui.Checkbox("Enable Better Weapon Notif. Icon", ref b))
        {
            GridWeaponSelectModelCtorHook?.Toggle();
            Config.EnableBetterWeaponNotificationIcon = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Does not consider character specific parts when displaying if a weapon\ncan be overboosted and only displays the notification icon if it can");

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
}