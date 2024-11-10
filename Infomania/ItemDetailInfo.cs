using System;
using System.Linq;
using System.Numerics;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.ItemDetail;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

public unsafe class ItemDetailInfo : ModalInfo
{
    public override bool Enabled => Infomania.Config.EnableItemDetailInfo;
    public override Type[] ValidModals { get; } = [ typeof(ItemDetailModalPresenter) ];

    public override void Draw(Modal modal)
    {
        if (!Il2CppType<ItemDetailModalPresenter>.Is(modal.NativePtr, out var itemDetailModal)) return;

        WeaponWork.WeaponStore* weaponStore = null;
        foreach (var p in itemDetailModal->itemDetailPresenter->itemDetailList->models->PtrEnumerable)
        {
            if (!Il2CppType<ItemDetailListItemCellModel>.Is(p.ptr, out var itemCellModel)
                || !Il2CppType<ItemWork.ItemStore>.Is(itemCellModel->itemInfo, out var itemStore)
                || (ItemType)itemStore->masterItem->itemType != ItemType.WeaponMedal)
                continue;

            var id = itemStore->masterItem->id;
            weaponStore = WorkManager.NativePtr->weapon->masterWeaponStoreSet->weaponStoreCores->values->PtrEnumerable.FirstOrDefault(p2 => p2.ptr->masterWeaponMedalItem->id == id).ptr;
            break;
        }

        if (weaponStore == null) return;

        var weaponID = weaponStore->masterWeapon->id;
        var entry = DataStore.NativePtr->userData->dB->userWeaponTable->dictionary->Enumerable.FirstOrDefault(p => p.ptr->value->weaponId == weaponID).ptr;
        if (entry == null || entry->value == null) return;

        var userWeapon = entry->value;
        var upgradeCount = userWeapon->upgradeCount;
        Infomania.BeginInfoWindow("ItemDetailInfo");
        ImGui.TextUnformatted("Weapon OB:");
        ImGui.SameLine();
        if (userWeapon->weaponUpgradeType == WeaponUpgradeType.Limit)
            ImGui.TextColored(GetOverboostColor(10), $"10 +{upgradeCount}");
        else
            ImGui.TextColored(GetOverboostColor(upgradeCount), upgradeCount.ToString());
        ImGui.End();
    }

    private static Vector4 GetOverboostColor(long count) => count switch
    {
        >= 10 => Vector4.One,
        >= 6 => new Vector4(0.9f, 0.7f, 1, 1),
        > 0 => new Vector4(1, 0.35f, 0.35f, 1),
        _ => new Vector4(1, 0.75f, 0.15f, 1)
    };
}