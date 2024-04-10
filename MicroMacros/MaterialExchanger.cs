using System.Collections.Generic;
using System.Linq;
using ECGen.Generated.Command.OutGame.ContentChanger;
using ECGen.Generated.Command.OutGame.Shop;
using ECGen.Generated.Command.UI;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Game;
using ModalManager = Materia.Game.ModalManager;

namespace MicroMacros;

public unsafe class MaterialExchanger : IMicroMacro
{
    private bool enabled = false;
    public ref bool Enabled => ref enabled;

    private static readonly Dictionary<long, long[]> shopItems = new()
    {
        [206001] =
            [
                551000000010011,
                551000000010012,
                551000000010021,
                551000000010022,
                551000000010041,
                551000000010042
            ],
        [206002] =
            [
                551000000005011,
                551000000005021,
                551000000005031,
                551000000005041,
                551000000005051,
                551000000005061,
                551000000005091,
                551000000005111,
                551000000005121
            ]
    };

    private long selectedShopItem = 0;
    private int count = 0;

    public void Update()
    {
        if (count <= 0)
        {
            enabled = false;
            return;
        }

        if (ModalManager.Instance?.GetCurrentModal<ShopModalPresenter>() is { } shopModal)
        {
            foreach (var t in shopModal.NativePtr->contentHandles->Enumerable)
            {
                if (Il2CppType<ShopWork.ShopStore>.Is(t.Item2, out var shopStore) && shopItems.ContainsKey(shopStore->masterShop->id)
                    && Il2CppType<ContentChanger.ContentHandle>.Is(t.Item1, out var contentHandle)
                    && Il2CppType<ShopMultipleContent>.Is(contentHandle->content, out var shopContent) && shopContent->isOpen && shopContent->shopProductList->scroller->activeCells->size != 0)
                {
                    foreach (var p in shopContent->shopProductList->scroller->activeCells->PtrEnumerable
                        .Where(p => p == Il2CppType<ShopMultipleProductCell>.Instance)
                        .SelectMany(p => Il2CppType<ShopMultipleProductCell>.As(p.ptr)->shopMultipleProductCellColumns->PtrEnumerable))
                    {
                        if (!Il2CppType<ShopWork.ShopItemStore>.Is(p.ptr->model->shopProductParameter->ShopItemInfo, out var shopItemStore) || shopItemStore->masterShopItem->nameLanguageId != selectedShopItem) continue;
                        GameInterop.TapButton(p.ptr->button);
                        break;
                    }
                }
            }
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopCheckExchangeModalPresenter>() is { } shopCheckExchangeModal)
        {
            if (shopItems.ContainsKey(shopCheckExchangeModal.NativePtr->currentShopProductParameter->ShopId)
                && Il2CppType<ShopWork.ShopItemStore>.Is(shopCheckExchangeModal.NativePtr->currentShopProductParameter->ShopItemInfo, out var shopItemStore)
                && shopItemStore->masterShopItem->nameLanguageId == selectedShopItem
                && GameInterop.TapButton(shopCheckExchangeModal.NativePtr->view->confirmButton, false))
                count--;
        }
        else
        {
            enabled = false;
        }
    }

    public void Draw()
    {
        if (ModalManager.Instance == null) return;

        var modal = ModalManager.Instance.GetModal<ShopModalPresenter>();
        if (modal == null) return;

        var found = 0L;
        foreach (var t in modal.NativePtr->contentHandles->Enumerable)
        {
            if (!Il2CppType<ShopWork.ShopStore>.Is(t.Item2, out var shopStore) || !shopItems.ContainsKey(shopStore->masterShop->id)) continue;
            if (!Il2CppType<ContentChanger.ContentHandle>.Is(t.Item1, out var contentHandle)
                || !Il2CppType<ShopMultipleContent>.Is(contentHandle->content, out var shopContent)
                || !shopContent->isOpen)
                continue;

            found = shopStore->masterShop->id;
            break;
        }

        if (found == 0) return;

        ImGui.Begin("MaterialExchanger", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        if (ImGui.BeginCombo("##Item", GameInterop.GetLocalizedText(LocalizeTextCategory.ShopItem, selectedShopItem)))
        {
            foreach (var shopItem in shopItems[found])
            {
                if (!ImGui.Selectable(GameInterop.GetLocalizedText(LocalizeTextCategory.ShopItem, shopItem), shopItem == selectedShopItem)) continue;
                selectedShopItem = shopItem;
                enabled = false;
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button("Buy"))
            enabled ^= true;

        ImGui.DragInt("Count", ref count, 0.25f, 0, 1000);

        ImGui.End();
    }
}