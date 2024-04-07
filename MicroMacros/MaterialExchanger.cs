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

    private const long exchangeShopId = 206001;
    private static readonly long[] shopItems =
    [
        551000000010011,
        551000000010012,
        551000000010021,
        551000000010022,
        551000000010041,
        551000000010042
    ];

    private long selectedShopItem = shopItems.First();
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
                if (Il2CppType<ShopWork.ShopStore>.Is(t.Item2, out var shopStore) && shopStore->masterShop->id == exchangeShopId
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
            if (shopCheckExchangeModal.NativePtr->currentShopProductParameter->ShopId == exchangeShopId
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

        var found = false;
        foreach (var t in modal.NativePtr->contentHandles->Enumerable)
        {
            if (!Il2CppType<ShopWork.ShopStore>.Is(t.Item2, out var shopStore) || shopStore->masterShop->id != exchangeShopId) continue;
            if (!Il2CppType<ContentChanger.ContentHandle>.Is(t.Item1, out var contentHandle)
                || !Il2CppType<ShopMultipleContent>.Is(contentHandle->content, out var shopContent)
                || !shopContent->isOpen)
                return;

            found = true;
            break;
        }

        if (!found) return;

        ImGui.Begin("MaterialExchanger", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        if (ImGui.BeginCombo("##Item", GameInterop.GetLocalizedText(LocalizeTextCategory.ShopItem, selectedShopItem)))
        {
            foreach (var shopItem in shopItems)
            {
                if (ImGui.Selectable(GameInterop.GetLocalizedText(LocalizeTextCategory.ShopItem, shopItem), shopItem == selectedShopItem))
                    selectedShopItem = shopItem;
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