using ECGen.Generated;
using ImGuiNET;
using Materia;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
using System;
using System.Numerics;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Shop;
using ECGen.Generated.Command.OutGame.Synthesis;
using ECGen.Generated.Command.Work;
using ECGen.Generated.System.Collections.Generic;
using BattleSystem = Materia.Game.BattleSystem;

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
        if (Config.DisableHiddenData)
            AnotherDungeonBossCellModelCtorHook?.Enable();
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
            case "Command.OutGame.Shop.ShopCheckPurchaseItemsModal" when Config.EnableSkipGilShopConfirmation:
                var purchaseItemsModal = (ShopCheckPurchaseItemsModal*)currentModal.NativePtr;
                if (purchaseItemsModal->consumptionType == ShopCheckPurchaseModal.ConsumptionType.Item && purchaseItemsModal->consumptionItemField->consumptionItemId == 1 && purchaseItemsModal->currentShopProductParameter->ShopId == 101002)
                    GameInterop.TapButton(purchaseItemsModal->consumptionItemField->okButton);
                return;
            case "Command.OutGame.Shop.ShopResetLineupModal" when Config.EnableSkipGilResetShopConfirmation:
                var shopResetLineupModal = (ShopResetLineupModal*)currentModal.NativePtr;
                var shopStore = (ShopWork.ShopStore*)shopResetLineupModal->currentShopInfo;
                if (shopResetLineupModal->consumptionItemField->consumptionItemId == 1 && shopStore->masterShop->id is 101002 or 207001)
                    GameInterop.TapButton(shopResetLineupModal->consumptionItemField->okButton);
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
                var synthesisArray = synthesisTop->synthesisContentGroup->nowSynthesisContent->displayCellPresenterArray;
                for (int i = 0; i < synthesisArray->size; i++)
                {
                    var synth = synthesisArray->GetPtr(i);
                    if (synth->cellModel->craftType->GetValue() != CraftType.Materia) continue;

                    switch (synth->view->currentViewType)
                    {
                        case SynthesisViewType.Synthesis:
                        case SynthesisViewType.Acceptance:
                            var synthesisStore = (SynthesisWork.SynthesisStore*)synth->cellModel->synthesisInfo->value;
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
        }

        if (BattleSystem.Instance is { } battleSystem && BattleHUD.Instance is { } battleHUD)
        {
            if (Config.EnableSkipBattleCutscenes)
            {
                switch (battleHUD.CurrentStatus)
                {
                    case HUD.Status.BossEncounterCutScene: // TODO: Does not work for summon cutscene
                        GameInterop.TapButton(battleHUD.NativePtr->cutsceneSkipper->tapArea);
                        GameInterop.TapButton(battleHUD.NativePtr->cutsceneSkipper->skipButton);
                        break;
                    case HUD.Status.SpecialSkill when battleSystem.IsLimitBreak:
                        GameInterop.SendKey(VirtualKey.VK_CONTROL);
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

        b = Config.DisableHiddenData;
        if (ImGui.Checkbox("Reveal Hidden Dungeon Bosses", ref b))
        {
            AnotherDungeonBossCellModelCtorHook?.Toggle();
            Config.DisableHiddenData = b;
            Config.Save();
        }

        b = Config.EnableSkipBattleCutscenes;
        if (ImGui.Checkbox("Auto Skip Battle Cutscenes", ref b))
        {
            Config.EnableSkipBattleCutscenes = b;
            Config.Save();
        }

        b = Config.EnableSkipGilShopConfirmation;
        if (ImGui.Checkbox("Auto Skip Gil Shop Confirmation", ref b))
        {
            Config.EnableSkipGilShopConfirmation = b;
            Config.Save();
        }

        b = Config.EnableSkipGilResetShopConfirmation;
        if (ImGui.Checkbox("Auto Skip Shop Reset Confirmation", ref b))
        {
            Config.EnableSkipGilResetShopConfirmation = b;
            Config.Save();
        }
        ImGuiEx.SetItemTooltip("Works on the Gil and Chocobo Medals shops");

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

    // TODO: Reveal missions and bosses on the map too
    private delegate void AnotherDungeonBossCellModelCtorDelegate(nint anotherDungeonBossCellModel, nint anotherBattleInfo, nint anotherBossInfos, CBool isWin, CBool showBossLabel, CBool isDisplayInfo, nint method);
    [GameSymbol("Command.OutGame.AnotherDungeon.AnotherDungeonBossCellModel$$.ctor", EnableHook = false)]
    private static IMateriaHook<AnotherDungeonBossCellModelCtorDelegate>? AnotherDungeonBossCellModelCtorHook;
    private static void AnotherDungeonBossCellModelCtorDetour(nint anotherDungeonBossCellModel, nint anotherBattleInfo, nint anotherBossInfos, CBool isWin, CBool showBossLabel, CBool isDisplayInfo, nint method) =>
        AnotherDungeonBossCellModelCtorHook!.Original(anotherDungeonBossCellModel, anotherBattleInfo, anotherBossInfos, isWin, showBossLabel, true, method);

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
}