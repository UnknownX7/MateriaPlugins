using ECGen.Generated;
using ImGuiNET;
using Materia;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SettingsPlus;

[Injection]
public unsafe class SettingsPlus : IMateriaPlugin
{
    public string Name => "Settings+";
    public string Description => "Displays additional settings";
    public static PluginServiceManager PluginServiceManager { get; private set; } = null!;
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();

    private static readonly List<Action> onUpdate = new();
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

        foreach (var action in onUpdate)
            action.Invoke();
        onUpdate.Clear();

        if (!Config.EnableSkipBattleCutscenes || BattleSystem.Instance is not { } battleSystem || BattleHUD.Instance is not { } battleHUD) return;
        if (battleHUD.CurrentStatus == 4 || (battleHUD.CurrentStatus == 9 && battleSystem.IsLimitBreak))
            GameInterop.SendKey(VirtualKey.VK_CONTROL);
    }

    public void UpdateUISettings()
    {
        var currentModal = ModalManager.Instance?.CurrentModal;
        switch (currentModal?.TypeName)
        {
            case "Command.OutGame.Shop.ShopCheckPurchaseItemsModal" when Config.EnableSkipGilShopConfirmation:
                var purchaseItemsModal = (Command_OutGame_Shop_ShopCheckPurchaseItemsModal*)currentModal.NativePtr;
                if (purchaseItemsModal->consumptionType == 2 && purchaseItemsModal->consumptionItemField->consumptionItemId == 1 && purchaseItemsModal->currentShopProductParameter->ShopId == 101002)
                    PressButton(purchaseItemsModal->consumptionItemField->okButton);
                return;
            case "Command.OutGame.Shop.ShopResetLineupModal" when Config.EnableSkipGilResetShopConfirmation:
                var shopResetLineupModal = (Command_OutGame_Shop_ShopResetLineupModal*)currentModal.NativePtr;
                var shopStore = (Command_Work_ShopWork_ShopStore*)shopResetLineupModal->currentShopInfo;
                if (shopResetLineupModal->consumptionItemField->consumptionItemId == 1 && shopStore->masterShop->id is 101002 or 207001)
                    PressButton(shopResetLineupModal->consumptionItemField->okButton);
                return;
            case null:
                break;
            default:
                return;
        }

        var currentScreen = ScreenManager.Instance?.CurrentScreen;
        switch (currentScreen?.TypeName)
        {
            case "Command.OutGame.Synthesis.SynthesisSelectScreenPresenter" when Config.EnableRememberLastSelectedMateriaRecipe:
                var synthesisSelect = (Command_OutGame_Synthesis_SynthesisSelectScreenPresenter*)currentScreen.NativePtr;
                var materiaRecipeStore = (Command_Work_MateriaWork_MateriaRecipeStore*)synthesisSelect->selectRecipe;
                lastMateriaRecipeId = materiaRecipeStore->masterMateriaRecipe->id;
                break;
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

    private delegate void AdequatelyWeaponMedalItemDelegate(nint weaponEnhancePanel, System_Collections_Generic_List<Command_OutGame_ItemCountSelectModel>* weaponMedalModels, long gil, nint method);
    [GameSymbol("Command.OutGame.Weapon.WeaponEnhancePanel$$AdequatelyWeaponMedalItem", EnableHook = false)]
    private static IMateriaHook<AdequatelyWeaponMedalItemDelegate>? AdequatelyWeaponMedalItemHook;
    private static void AdequatelyWeaponMedalItemDetour(nint weaponEnhancePanel, System_Collections_Generic_List<Command_OutGame_ItemCountSelectModel>* weaponMedalModels, long gil, nint method)
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

    [GameSymbol("Command.UI.SingleTapButton$$ForceTapSteamUICursor")]
    private static delegate* unmanaged<void*, nint, void> forceTapSteamUICursor;

    private static readonly Queue<(nint, nint)> lastPressedButtons = new(11);
    public static void PressButton(Command_UI_SingleTapButton* singleTapButton)
    {
        if (lastPressedButtons.Contains(((nint)singleTapButton, (nint)singleTapButton->steamUICursorTapSubject))) return;

        var isSteamKeyAvailable = (delegate* unmanaged<Command_UI_SingleTapButton*, nint, CBool>)singleTapButton->klass->vtable.IsSteamKeyAvailable.methodPtr;
        if (!isSteamKeyAvailable(singleTapButton, 0)) return;

        lastPressedButtons.Enqueue(((nint)singleTapButton, (nint)singleTapButton->steamUICursorTapSubject));
        if (lastPressedButtons.Count > 10)
            lastPressedButtons.Dequeue();
        onUpdate.Add(() => forceTapSteamUICursor(singleTapButton, 0));
    }

    public static void PressButton(Command_UI_TintButton* button) => PressButton((Command_UI_SingleTapButton*)button);

    private static long lastMateriaRecipeId;
    private delegate void SynthesisSelectScreenSetupParameterCtorDelegate(Command_OutGame_Synthesis_SynthesisSelectScreenSetupParameter* param, int selectDataIndex, int synthesisRecipeViewType, nint method);
    [GameSymbol("Command.OutGame.Synthesis.SynthesisSelectScreenSetupParameter$$.ctor", EnableHook = false)]
    private static IMateriaHook<SynthesisSelectScreenSetupParameterCtorDelegate>? SynthesisSelectScreenSetupParameterCtorHook;
    private static void SynthesisSelectScreenSetupParameterCtorDetour(Command_OutGame_Synthesis_SynthesisSelectScreenSetupParameter* param, int selectDataIndex, int synthesisRecipeViewType, nint method)
    {
        SynthesisSelectScreenSetupParameterCtorHook!.Original(param, selectDataIndex, synthesisRecipeViewType, method);
        if (synthesisRecipeViewType == 1)
            param->materiaRecipeId = lastMateriaRecipeId;
    }
}