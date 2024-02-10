using System;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame.Gift;
using ImGuiNET;

namespace Infomania;

public static unsafe class GiftInfo
{
    public static void Draw(GiftModalPresenter* giftModal)
    {
        var dictionary = giftModal->itemModel->giftListCache;
        var staminaTonicCount = 0L;
        var firstExpiry = 7258118400000;
        for (int i = 0; i < dictionary->count; i++)
        {
            var entry = dictionary->GetEntry(i);
            if (entry == null) continue;

            for (int j = 0; j < entry->value->size; j++)
            {
                var giftRewardInfo = entry->value->GetPtr(j);
                if (giftRewardInfo->rewardType != RewardType.Item || giftRewardInfo->targetId != 17002) continue;
                staminaTonicCount += giftRewardInfo->count;
                firstExpiry = Math.Min(giftRewardInfo->giftInfo->expireDatetime, firstExpiry);
            }
            break;
        }

        if (staminaTonicCount == 0) return;

        ImGui.Begin("GiftInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        ImGui.TextUnformatted($"Stamina Tonics: {staminaTonicCount}");
        ImGui.TextUnformatted($"First Expiry: {DateTimeOffset.FromUnixTimeMilliseconds(firstExpiry).ToLocalTime():g}");
        ImGui.End();
    }
}