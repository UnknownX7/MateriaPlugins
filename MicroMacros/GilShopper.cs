﻿using ECGen.Generated.Command.OutGame.ContentChanger;
using ECGen.Generated.Command.OutGame.Shop;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Game;

namespace MicroMacros;

public unsafe class GilShopper : IMicroMacro
{
    private const long shopID = 101067;
    private bool enabled = false;
    public ref bool Enabled => ref enabled;

    public void Update()
    {
        if (ModalManager.Instance?.GetCurrentModal<ShopModalPresenter>() is { } shopModal)
        {
            foreach (var t in shopModal.NativePtr->contentHandles->Enumerable)
            {
                if (Il2CppType<ShopWork.ShopStore>.Is(t.Item2, out var shopStore) && shopStore->masterShop->id == shopID
                    && Il2CppType<ContentChanger.ContentHandle>.Is(t.Item1, out var contentHandle)
                    && Il2CppType<ShopSingleContent>.Is(contentHandle->content, out var shopContent) && shopContent->isOpen && shopContent->shopProductList->scroller->activeCells->size != 0
                    && Il2CppType<ShopSingleProductCell>.Is(shopContent->shopProductList->scroller->activeCells->GetPtr(0), out var productCell) && productCell->shopSingleProductCellColumns->size != 0)
                {
                    var column = productCell->shopSingleProductCellColumns->GetPtr(0);
                    if (column->shopProductParameter != null && (column->button->lockedObject == null || !column->button->lockedObject->isLocked))
                        GameInterop.TapButton(column->button, false);
                    else
                        GameInterop.TapButton(shopContent->lineupResetButton->lineupResetButton, false);
                }
            }
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopCheckPurchaseItemsModal>() is { } shopCheckPurchaseItemsModal)
        {
            if (shopCheckPurchaseItemsModal.NativePtr->consumptionType == ShopCheckPurchaseModal.ConsumptionType.Item && shopCheckPurchaseItemsModal.NativePtr->consumptionItemField->consumptionItemId == 1 && shopCheckPurchaseItemsModal.NativePtr->currentShopProductParameter->ShopId == shopID)
                GameInterop.TapButton(shopCheckPurchaseItemsModal.NativePtr->consumptionItemField->okButton, false);
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopResetLineupModal>() is { } shopResetLineupModal)
        {
            var shopStore = (ShopWork.ShopStore*)shopResetLineupModal.NativePtr->currentShopInfo;
            if (shopResetLineupModal.NativePtr->consumptionItemField->consumptionItemId == 1 && shopStore->masterShop->id == shopID)
                GameInterop.TapButton(shopResetLineupModal.NativePtr->consumptionItemField->okButton, false);
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopResetLineupSimpleModalPresenter>() is { } simpleResetLineupModal)
        {
            GameInterop.TapButton(simpleResetLineupModal.NativePtr->simpleModalView->positiveButton, false);
        }
        else
        {
            enabled = false;
        }
    }

    public void Draw()
    {
        if (ModalManager.Instance == null) return;

        var found = false;
        foreach (var modal in ModalManager.Instance.CurrentModals)
        {
            if (!Il2CppType<ShopModalPresenter>.Is(modal.NativePtr, out var shopModal)) continue;
            for (int i = 0; i < shopModal->contentHandles->size; i++)
            {
                var t = shopModal->contentHandles->Get(i);
                if (!Il2CppType<ShopWork.ShopStore>.Is(t.Item2, out var shopStore) || shopStore->masterShop->id != shopID) continue;
                if (!Il2CppType<ContentChanger.ContentHandle>.Is(t.Item1, out var contentHandle)
                    || !Il2CppType<ShopSingleContent>.Is(contentHandle->content, out var shopContent)
                    || !shopContent->isOpen)
                    return;
                found = true;
                break;
            }
        }

        if (!found) return;

        ImGui.Begin("GilShopper", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        if (ImGui.Button("Quick Buy Gil Shop"))
            enabled ^= true;
        ImGui.End();
    }
}