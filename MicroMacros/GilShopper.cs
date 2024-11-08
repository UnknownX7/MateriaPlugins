using System.Linq;
using ECGen.Generated.Command.OutGame.ContentChanger;
using ECGen.Generated.Command.OutGame.Shop;
using ECGen.Generated.Command.Work;
using ECGen.Generated.System.Collections.Generic;
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
                    && Il2CppType<ShopSingleContent>.Is(contentHandle->content, out var shopContent) && shopContent->isOpen && shopContent->shopProductList->scroller->activeCells->size != 0)
                {
                    if (shopModal.NativePtr->view->bulkExchangeButton->buttonReference->isEnable)
                        GameInterop.TapButton(shopModal.NativePtr->view->bulkExchangeButton->buttonReference);
                    else if (shopContent->lineupResetButton->lineupResetButton->isEnable)
                        GameInterop.TapButton(shopContent->lineupResetButton->lineupResetButton);
                }
            }
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopCheckBulkExchangeModal>() is { } shopCheckBulkExchangeModal)
        {
            if (shopCheckBulkExchangeModal.NativePtr->consumptionType == ShopCheckBulkExchangeModal.ConsumptionType.Item && shopCheckBulkExchangeModal.NativePtr->consumptionItemField->consumptionItemId == 1)
            {
                var shopProductParameter = (Unmanaged_List<ShopProductParameter>*)shopCheckBulkExchangeModal.NativePtr->shopProductParameters;
                if (shopProductParameter->size > 0 && shopProductParameter->PtrEnumerable.First().ptr->ShopId == shopID)
                    GameInterop.TapButton(shopCheckBulkExchangeModal.NativePtr->consumptionItemField->okButton);
            }
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopResetLineupModal>() is { } shopResetLineupModal)
        {
            var shopStore = (ShopWork.ShopStore*)shopResetLineupModal.NativePtr->currentShopInfo;
            if (shopResetLineupModal.NativePtr->consumptionItemField->consumptionItemId == 1 && shopStore->masterShop->id == shopID)
                GameInterop.TapButton(shopResetLineupModal.NativePtr->consumptionItemField->okButton);
        }
        else if (ModalManager.Instance?.GetCurrentModal<ShopResetLineupSimpleModalPresenter>() is { } simpleResetLineupModal)
        {
            GameInterop.TapButton(simpleResetLineupModal.NativePtr->simpleModalView->positiveButton);
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