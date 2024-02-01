using System;
using ECGen.Generated;
using ImGuiNET;

namespace Infomania;

public static unsafe class GiftInfo
{
    public static void Draw(Command_OutGame_Gift_GiftModalPresenter* giftModal)
    {
        var dictionary = (System_Collections_Generic_Dictionary<int, System_Collections_Generic_List<Command_Api_GiftRewardInfo>>*)giftModal->itemModel->giftListCache;
        var staminaTonicCount = 0L;
        var firstExpiry = 7258118400000;
        for (int i = 0; i < dictionary->count; i++)
        {
            var entry = dictionary->GetEntry(i);
            if (entry == null) continue;

            for (int j = 0; j < entry->value->size; j++)
            {
                var giftRewardInfo = entry->value->Get(j);
                if (giftRewardInfo->rewardType != 1 || giftRewardInfo->targetId != 17002) continue;
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