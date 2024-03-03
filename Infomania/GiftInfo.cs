using System;
using System.Linq;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame.Gift;
using ImGuiNET;

namespace Infomania;

public static unsafe class GiftInfo
{
    public static void Draw(GiftModalPresenter* giftModal)
    {
        var entry = giftModal->itemModel->giftListCache->Enumerable.FirstOrDefault().ptr;
        if (entry == null) return;

        var staminaGifts = entry->value->PtrEnumerable.Where(p => p.ptr->rewardType == RewardType.Item && p.ptr->targetId == 17002).ToArray();
        var staminaTonicCount = staminaGifts.Sum(p => p.ptr->count);
        var firstExpiry = staminaTonicCount > 0 ? staminaGifts.Min(p => p.ptr->giftInfo->expireDatetime) : 0;

        if (staminaTonicCount == 0) return;

        ImGui.Begin("GiftInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        ImGui.TextUnformatted($"Stamina Tonics: {staminaTonicCount}");
        ImGui.TextUnformatted($"First Expiry: {DateTimeOffset.FromUnixTimeMilliseconds(firstExpiry).ToLocalTime():g}");
        ImGui.End();
    }
}