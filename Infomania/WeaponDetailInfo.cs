using System;
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

    private int level = 90;
    private int upgradeCount = 0;

    public override void Draw(Modal modal)
    {
        if (!Il2CppType<WeaponDetailModalPresenter>.Is(modal.NativePtr, out var weaponDetailModal)) return;

        var refresh = false;
        Infomania.BeginInfoWindow("WeaponDetailInfo");

        if (ImGui.BeginTabBar("WeaponDetailInfoTabs"))
        {
            if (ImGui.BeginTabItem("Simple"))
            {
                if (ImGui.Button("Lv. 80"))
                {
                    level = 80;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Lv. 90"))
                {
                    level = 90;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Lv. 110"))
                {
                    level = 110;
                    refresh = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Lv. 120"))
                {
                    level = 120;
                    refresh = true;
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
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Advanced"))
            {
                refresh |= ImGui.SliderInt("Level", ref level, 1, 120, null, ImGuiSliderFlags.AlwaysClamp);
                refresh |= ImGui.SliderInt("OB", ref upgradeCount, 0, 30, null, ImGuiSliderFlags.AlwaysClamp);
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

    [GameSymbol("Command.Work.WeaponWork$$ForkMasterWeaponInfoByLevel")]
    private static delegate* unmanaged<WeaponWork*, long, long, RarityType, WeaponUpgradeType, long, int, nint, WeaponWork.WeaponStore*> getWeaponStore;

    private static void RefreshWeaponParameters(WeaponDetailModalPresenter* modal, int lv, int upgrade)
    {
        var id = ((WeaponWork.WeaponStore*)modal->currentWeaponInfo)->masterWeapon->id;
        var upgradeType = WeaponUpgradeType.Rank;
        if (upgrade > 10)
        {
            upgradeType = WeaponUpgradeType.Limit;
            upgrade -= 10;
        }
        GameInterop.RunOnUpdate(() => weaponDetailModalRefreshParameter(modal, getWeaponStore(WorkManager.NativePtr->weapon, id, lv, RarityType.Ssr, upgradeType, Math.Max((lv - 1) / 10 - 1, 0), upgrade, 0), 0));
    }
}