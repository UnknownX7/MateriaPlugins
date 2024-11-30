using System;
using System.Numerics;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame.Weapon;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Attributes;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

[Injection]
public unsafe class WeaponDetailInfo : ModalInfo
{
    public override bool Enabled => Infomania.Config.EnableWeaponDetailInfo;
    public override Type[] ValidModals { get; } = [ typeof(WeaponDetailModalPresenter) ];

    private const int maxLevel = 120;
    private const int maxUpgrade = 30;
    private int level = maxLevel;
    private int upgradeCount = 10;

    public override void Activate(Modal modal)
    {
        if (!Il2CppType<WeaponDetailModalPresenter>.Is(modal.NativePtr, out var weaponDetailModal)
            || !Il2CppType<WeaponWork.WeaponStore>.Is(weaponDetailModal->currentWeaponInfo, out var weaponStore))
            return;

        level = (int)weaponStore->level;

        if (weaponStore->userWeapon != null)
        {
            upgradeCount = weaponStore->userWeapon->rarityType switch
            {
                RarityType.R => -2,
                RarityType.Sr => -1,
                RarityType.Legendary => 0,
                _ => weaponStore->weaponUpgradeRank + weaponStore->weaponUpgradeLimit
            };
        }
        else
        {
            upgradeCount = 0;
        }
    }

    public override void Draw(Modal modal)
    {
        if (!Il2CppType<WeaponDetailModalPresenter>.Is(modal.NativePtr, out var weaponDetailModal)) return;

        var refresh = false;
        using var __ = ImGuiEx.StyleVarBlock.Begin(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
        using var ___ = ImGuiEx.ButtonRepeatBlock.Begin();

        Infomania.BeginInfoWindow("WeaponDetailInfo");

        if (ImGui.BeginTabBar("WeaponDetailInfoTabs"))
        {
            if (ImGui.BeginTabItem("Simple"))
            {
                if (ImGui.Button("Lv.80"))
                {
                    level = 80;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Lv.90"))
                {
                    level = 90;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Lv.110"))
                {
                    level = 110;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Lv.120"))
                {
                    level = 120;
                    refresh = true;
                }
                ImGui.SameLine();
                var squareVector = new Vector2(ImGui.GetItemRectSize().Y);
                using (_ = ImGuiEx.DisabledBlock.Begin(level <= 1))
                {
                    if (ImGui.Button("-##level", squareVector))
                    {
                        level = Math.Max((level - 10) / 10 * 10, 1);
                        refresh = true;
                    }
                }
                ImGui.SameLine();
                using (_ = ImGuiEx.DisabledBlock.Begin(level >= maxLevel))
                {
                    if (ImGui.Button("+##level", squareVector))
                    {
                        level = Math.Min((level + 10) / 10 * 10, maxLevel);
                        refresh = true;
                    }
                }

                if (ImGui.Button("OB0"))
                {
                    upgradeCount = 0;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("OB1"))
                {
                    upgradeCount = 1;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("OB6"))
                {
                    upgradeCount = 6;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("OB10"))
                {
                    upgradeCount = 10;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("OB+20"))
                {
                    upgradeCount = 30;
                    refresh = true;
                }
                ImGui.SameLine();
                using (_ = ImGuiEx.DisabledBlock.Begin(upgradeCount <= -2))
                {
                    if (ImGui.Button("-##upgradeCount", squareVector))
                    {
                        upgradeCount -= 1;
                        refresh = true;
                    }
                }
                ImGui.SameLine();
                using (_ = ImGuiEx.DisabledBlock.Begin(upgradeCount >= maxUpgrade))
                {
                    if (ImGui.Button("+##upgradeCount", squareVector))
                    {
                        upgradeCount += 1;
                        refresh = true;
                    }
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Advanced"))
            {
                refresh |= ImGui.SliderInt("Level", ref level, 1, 120, null, ImGuiSliderFlags.AlwaysClamp);
                refresh |= ImGui.SliderInt("OB", ref upgradeCount, -2, 30, null, ImGuiSliderFlags.AlwaysClamp);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (refresh)
            RefreshWeaponParameters(weaponDetailModal, level, upgradeCount);

        ImGui.End();
    }

    [GameSymbol("Command.OutGame.Weapon.WeaponDetailModalPresenter$$RefreshParameter")]
    private static delegate* unmanaged<WeaponDetailModalPresenter*, WeaponWork.WeaponStore*, nint, void> weaponDetailModalRefreshParameter;

    private static void RefreshWeaponParameters(WeaponDetailModalPresenter* modal, int lv, int upgrade)
    {
        var weaponStore = (WeaponWork.WeaponStore*)modal->currentWeaponInfo;
        var id = weaponStore->masterWeapon->id;
        var rarityType = RarityType.None;
        var upgradeType = WeaponUpgradeType.Rank;

        lv = Math.Clamp(lv, 1, maxLevel);
        upgrade = Math.Clamp(upgrade, -2, maxUpgrade);

        switch ((WeaponEquipmentType)weaponStore->masterWeapon->weaponEquipmentType)
        {
            case WeaponEquipmentType.Normal:
                switch (upgrade)
                {
                    case -2:
                        rarityType = RarityType.R;
                        upgrade = 0;
                        break;
                    case -1:
                        rarityType = RarityType.Sr;
                        upgrade = 0;
                        break;
                    case > 10:
                        rarityType = RarityType.Ssr;
                        upgradeType = WeaponUpgradeType.Limit;
                        upgrade -= 10;
                        break;
                    case >= 0:
                        rarityType = RarityType.Ssr;
                        break;
                }
                break;
            case WeaponEquipmentType.Legendary:
                rarityType = RarityType.Legendary;
                upgrade = 0;
                break;
        }

        GameInterop.RunOnUpdate(() => weaponDetailModalRefreshParameter(modal, WorkManager.GetWeaponStore(id, lv, rarityType, upgradeType, Math.Max((lv - 1) / 10 - 1, 0), upgrade, 0, 0, 0), 0));
    }
}