using System;
using System.Linq;
using System.Numerics;
using ECGen.Generated;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Home;
using ECGen.Generated.Command.Work;
using ECGen.Generated.System.Collections.Generic;
using ImGuiNET;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

public static unsafe class HomeInfo
{
    private static readonly Vector4 green = new(0.4f, 1, 0.4f, 1);
    private static readonly Vector4 red = new(1, 0.4f, 0.4f, 1);

    public static void Draw(HomeTopScreenPresenter* homeScreen)
    {
        if (homeScreen->currentContentState != HomeContentState.Top) return;

        var gilShop = WorkManager.GetShopStore(101002);
        var total = 0L;
        var remaining = 0L;
        foreach (var p in DataStore.NativePtr->userData->dB->userDailyQuestTable->dictionary->Enumerable)
        {
            total += p.ptr->value->totalRemainWinCount;
            remaining += p.ptr->value->remainWinCount;
        }
        var premiumQuestGroupCategory = WorkManager.GetSoloAreaGroupCategoryStore(99995500001);
        var f = (delegate* unmanaged<AreaBattleWork.SoloAreaGroupCategoryStore*, nint, long>)premiumQuestGroupCategory->@class->vtable.get_RemainingChallengeCount.methodPtr;
        var remainingPremiumQuests = f(premiumQuestGroupCategory, 0);

        ImGui.Begin("HomeInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);

        var maintenanceTimer = GetTimeUntilMaintenance();
        if (maintenanceTimer >= TimeSpan.Zero)
        {
            DrawTimer("MAINTENANCE", maintenanceTimer, true, 0, 24);
            ImGui.Spacing();
            ImGui.Spacing();
        }

        DrawResetTimer("Dailies", 4, 12, IsMissionBonusObtained(200001)); // Daily Mission Reset
        DrawResetTimer("Daily Shop", 3, 12, gilShop->userShop->lineupResetCount == gilShop->masterShop->maxLineupResetCount); // 2 is the reset time for the refreshes for some reason (14 is also the daily shop reset)
        //DrawResetTimer("Guild Energy", 18, 0);
        DrawResetTimer("Weeklies", 5, 48, IsMissionBonusObtained(300001)); // Weekly Mission Reset
        DrawResetTimer("Weekly Shop", 11, 0);
        DrawResetTimer("Monthly Shop", 12, 0);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(remaining == 0 ? green : red, $"Daily Quests:      {total - remaining}/{total}");
        ImGui.TextColored(remainingPremiumQuests == 0 ? green : red, $"Premium Quests:    {premiumQuestGroupCategory->masterSoloAreaGroupCategory->resetMaxWinCount - remainingPremiumQuests}/{premiumQuestGroupCategory->masterSoloAreaGroupCategory->resetMaxWinCount}");

        ImGui.Spacing();
        ImGui.Spacing();

        var craftTimer = GetTimeUntilCraftFinished();
        if (craftTimer >= TimeSpan.Zero)
            DrawTimer("Crafting", craftTimer);
        ImGui.TextUnformatted($"Chocobo: {GetHighestChocoboShopRank()}");

        ImGui.End();
    }

    private static void DrawTimer(string name, TimeSpan remainingTime, bool drawSeconds = true, int padding = 0, int hourColorThreshold = 0, bool finished = false)
    {
        var timeStr = remainingTime > TimeSpan.Zero ? drawSeconds ? $"{(long)remainingTime.TotalHours:D2}:{remainingTime:mm\\:ss}" : $"{(long)remainingTime.TotalHours:D2}:{remainingTime:mm}" : drawSeconds ? "00:00:00" : "00:00";
        var color = green;
        if (!finished)
        {
            var colorThresholdTime = TimeSpan.FromHours(hourColorThreshold);
            if (colorThresholdTime > TimeSpan.Zero)
            {
                var thresholdRatio = (float)(remainingTime / colorThresholdTime);
                color = thresholdRatio < 1 ? new Vector4(1, thresholdRatio, thresholdRatio, 1) : Vector4.One;
            }
            else
            {
                color = Vector4.One;
            }
        }
        ImGui.TextColored(color, $"{name}:{new string(' ', Math.Max(padding - name.Length - timeStr.Length, 1))}{timeStr}");
    }

    private static void DrawResetTimer(string name, long resetId, int hourColorThreshold, bool finished = false)
    {
        const int padding = 21;
        var remainingTime = WorkManager.GetTimeUntilReset(resetId);
        DrawTimer(name, remainingTime, false, padding, hourColorThreshold, finished);
    }

    private static bool IsMissionBonusObtained(long groupId)
    {
        var baseId = groupId * 100;
        while (WorkManager.GetMissionBonusStore(++baseId) is var missionBonusStore && missionBonusStore != null)
            if (!missionBonusStore->isReceived) return false;
        return true;
    }

    private static TimeSpan GetTimeUntilCraftFinished()
    {
        var currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var crafts = DataStore.NativePtr->userData->dB->userCraftTable->dictionary->Enumerable.Where(p => !p.ptr->value->isEmpty && p.ptr->value->calcCompleteDatetime > currentMs).ToArray();
        var firstFinished = crafts.Length > 0 ? crafts.Min(p => p.ptr->value->calcCompleteDatetime) : long.MaxValue;
        return firstFinished < long.MaxValue ? TimeSpan.FromMilliseconds(firstFinished - currentMs) : TimeSpan.MinValue;
    }

    private static string GetHighestChocoboShopRank()
    {
        var chocoboShop = WorkManager.GetShopStore(207001);
        var chocobos = (Unmanaged_Array<ShopItemLineupInfo>*)chocoboShop->shopItemLineupInfos;
        var highestRank = ChocoboRankType.None;
        var areaType = ChocoboAreaType.None;
        foreach (var ptr in chocobos->PtrEnumerable)
        {
            var rewardArray = (Unmanaged_List<RewardWork.RewardSetRewardRelStore>*)WorkManager.GetShopItemStore(ptr.ptr->shopItemId)->rewardSetRewardRelInfos;
            if (rewardArray->size == 0) continue;
            var reward = WorkManager.GetRewardStore(rewardArray->GetPtr(0)->masterRewardSetRewardRel->rewardId);
            if (reward == null || (RewardType)reward->masterReward->rewardType != RewardType.Chocobo) continue;
            var chocobo = WorkManager.GetChocoboStore(reward->masterReward->targetId);
            if (chocobo == null || chocobo->currentChocoboRankType < highestRank) continue;
            highestRank = chocobo->currentChocoboRankType;
            areaType = chocobo->chocoboAreaType;
        }
        return $"{highestRank.ToString().Replace("Plus", "+")} ({areaType})";
    }

    private static TimeSpan GetTimeUntilMaintenance() => TimeSpan.FromMilliseconds(DataStore.NativePtr->master->dB->maintenanceTable->data->PtrEnumerable
        .Where(p => (PlatformType)p.ptr->targetPlatformType is PlatformType.Any or PlatformType.Steam)
        .Max(p => p.ptr->startDatetime) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}