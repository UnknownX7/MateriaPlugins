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
using Materia.Attributes;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

[Injection]
public unsafe class HomeInfo : ScreenInfo
{
    private static readonly Vector4 green = new(0.4f, 1, 0.4f, 1);
    private static readonly Vector4 red = new(1, 0.4f, 0.4f, 1);

    public override bool Enabled => Infomania.Config.EnableHomeInfo;
    public override Type[] ValidScreens { get; } = [ typeof(HomeTopScreenPresenter) ];

    private long freeGachaAvailable;

    public override void Activate(Screen screen) => freeGachaAvailable = GetFreeGachaAvailable();

    public override void Draw(Screen screen)
    {
        if (!Il2CppType<HomeTopScreenPresenter>.Is(screen.NativePtr, out var homeScreen) || homeScreen->currentContentState != HomeContentState.Top) return;

        var gilShop = WorkManager.GetShopStore(101094);
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

        Infomania.BeginInfoWindow("HomeInfo");

        if (freeGachaAvailable != 0)
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "FREE DRAW AVAILABLE!!!");
            if (ImGuiEx.IsItemReleased())
                ScreenManager.TransitionAsync(TransitionType.Gacha, freeGachaAvailable);
        }

        var maintenanceTimer = GetTimeUntilMaintenance();
        if (maintenanceTimer >= TimeSpan.Zero)
        {
            DrawTimer("MAINTENANCE", maintenanceTimer, true, 21, 48);
            ImGui.Spacing();
            ImGui.Spacing();
        }

        var dailiesDone = IsMissionBonusObtained(200003);
        var guildDailiesDone = IsMissionGroupCleared(1300001);
        if (!dailiesDone || guildDailiesDone)
        {
            DrawResetTimer("Dailies", 4, 12, dailiesDone); // Daily Mission Reset
            if (ImGuiEx.IsItemReleased())
                ScreenManager.TransitionAsync(TransitionType.Mission, 200003);
        }
        else
        {
            DrawResetTimer("Guild Dailies", 4, 12, guildDailiesDone);
            if (ImGuiEx.IsItemReleased())
                ScreenManager.TransitionAsync(TransitionType.Mission, 1300001);
        }
        DrawResetTimer("Daily Shop", 3, 12, gilShop->userShop->lineupResetCount == gilShop->masterShop->maxLineupResetCount); // 2 is the reset time for the refreshes for some reason (14 is also the daily shop reset)
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.Shop, 101094);
        DrawResetTimer("Weeklies", 5, 48, IsMissionBonusObtained(300002)); // Weekly Mission Reset
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.Mission, 300002);
        DrawResetTimer("Weekly Shop", 11, 0);
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.Shop, 205002);
        DrawResetTimer("Monthly Shop", 12, 0);
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.Shop, 205001);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(remaining == 0 ? green : red, $"Daily Quests:      {total - remaining}/{total}");
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.Home, (int)HomeContentState.SoloBattleDailyQuest);
        ImGui.TextColored(remainingPremiumQuests == 0 ? green : red, $"Premium Quests:    {premiumQuestGroupCategory->masterSoloAreaGroupCategory->resetMaxWinCount - remainingPremiumQuests}/{premiumQuestGroupCategory->masterSoloAreaGroupCategory->resetMaxWinCount}");
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.AreaSoloBattleTop, (int)AreaGroupCategoryType.Extra);

        ImGui.Spacing();
        ImGui.Spacing();

        var dailyAdViews = getViewCount(WorkManager.NativePtr->advertisement, 0);
        if (dailyAdViews < 2)
            ImGui.TextUnformatted($"Daily Ads Remaining: {2 - dailyAdViews}");

        var expUntil = WorkManager.NextRequiredUserExp - WorkManager.NativePtr->user->userStatusStore->userStatus->exp;
        ImGui.TextUnformatted($"Stamina To Lv.: {(expUntil + 9) / 10}");
        var craftTimer = GetTimeUntilCraftFinished();
        if (craftTimer >= TimeSpan.Zero)
        {
            DrawTimer("Crafting", craftTimer);
            if (ImGuiEx.IsItemReleased())
                ScreenManager.TransitionAsync(TransitionType.CraftTop);
        }
        ImGui.TextUnformatted($"Chocobo: {GetHighestChocoboShopRank()}");
        if (ImGuiEx.IsItemReleased())
            ScreenManager.TransitionAsync(TransitionType.Shop, 207001);

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
        var remainingTime = TimeSpan.Zero;
        if (resetId != 12)
        {
            remainingTime = WorkManager.GetTimeUntilReset(resetId);
        }
        else
        {
            var resetStore = WorkManager.GetResetStore(resetId);
            var utcNow = DateTimeOffset.UtcNow;
            var offsetUtcNow = utcNow.AddMilliseconds(-resetStore->masterReset->resetOffsetDatetime);
            var nextMonth = offsetUtcNow.Month != 12
                ? new DateTimeOffset(offsetUtcNow.Year, offsetUtcNow.Month + 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(resetStore->masterReset->resetOffsetDatetime)
                : new DateTimeOffset(offsetUtcNow.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(resetStore->masterReset->resetOffsetDatetime);
            remainingTime = nextMonth - utcNow;
        }
        DrawTimer(name, remainingTime, false, padding, hourColorThreshold, finished);
    }

    private static bool IsMissionBonusObtained(long groupId)
    {
        var baseId = groupId * 100;
        while (WorkManager.GetMissionBonusStore(++baseId) is var missionBonusStore && missionBonusStore != null)
            if (!missionBonusStore->isReceived) return false;
        return true;
    }

    [GameSymbol("Command.Work.MissionWork$$GetMissionGroupInfo")]
    private static delegate* unmanaged<MissionWork*, long, nint, IMissionGroupInfo*> getMissionGroupInfo;

    [GameSymbol("Command.Work.MissionWorkExtensions$$IsGroupMissionClear")]
    private static delegate* unmanaged<IMissionGroupInfo*, nint, CBool> isGroupMissionClear;

    private static bool IsMissionGroupCleared(long id)
    {
        var missionGroup = getMissionGroupInfo(WorkManager.NativePtr->mission, id, 0);
        return missionGroup != null && isGroupMissionClear(missionGroup, 0);
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

    private static long GetFreeGachaAvailable()
    {
        var gachas = (Unmanaged_Array<GachaWork.GachaStore>*)WorkManager.NativePtr->gacha->gachaGroupStores->values->PtrEnumerable.First(p => p.ptr->masterGachaGroup->id == 3).ptr->gachaInfos;
        foreach (var p in gachas->PtrEnumerable)
        {
            var isDisplay = (delegate* unmanaged<GachaWork.GachaStore*, nint, CBool>)p.ptr->@class->vtable.get_IsDisplay.methodPtr;
            if (!isDisplay(p, 0)) continue;

            var stepGroups = (Unmanaged_Array<GachaWork.GachaStepGroupStore>*)p.ptr->gachaStepGroupInfos;
            foreach (var p2 in stepGroups->PtrEnumerable.Where(p2 => p2.ptr->masterGachaStepGroup->maxCount > 0))
            {
                var steps = (Unmanaged_Array<GachaWork.GachaStepStore>*)p2.ptr->gachaStepInfos;
                if (steps->size != 1) continue; // TODO: Detect first step free draws

                var step = steps->GetPtr(0);
                if (step->masterGachaStep->consumptionCount > 0) continue;

                var isDrewMaxCount = (delegate* unmanaged<GachaWork.GachaStepGroupStore*, nint, CBool>)p2.ptr->@class->vtable.get_IsDrewMaxCount.methodPtr;
                if (!isDrewMaxCount(p2, 0)) return p.ptr->masterGacha->id;
            }
        }
        return 0;
    }

    [GameSymbol("Command.Work.AdvertisementWork$$GetViewCount")]
    private static delegate* unmanaged<AdvertisementWork*, nint, long> getViewCount;
}