using System;
using System.Numerics;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Home;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Attributes;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

[Injection]
public static unsafe class HomeInfo
{
    public static void Draw(HomeTopScreenPresenter* homeScreen)
    {
        if (homeScreen->currentContentState != HomeContentState.Top) return;

        var dailyQuests = DataStore.NativePtr->userData->dB->userDailyQuestTable->dictionary;
        var total = 0L;
        var remaining = 0L;
        for (int i = 0; i < dailyQuests->count; i++)
        {
            var quest = dailyQuests->GetEntry(i)->value;
            total += quest->totalRemainWinCount;
            remaining += quest->remainWinCount;
        }
        var premiumQuestGroupCategory = WorkManager.GetSoloAreaGroupCategoryStore(99995500001);

        ImGui.Begin("HomeInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        DrawResetTimer("Dailies", 4, 12); // Daily Mission Reset
        DrawResetTimer("Daily Shop", 3, 12); // 2 is the reset time for the refreshes for some reason (14 is also the daily shop reset)
        //DrawResetTimer("Guild Energy", 18, 12);
        DrawResetTimer("Weeklies", 5, 48); // Weekly Mission Reset
        DrawResetTimer("Weekly Shop", 11, 48);
        DrawResetTimer("Monthly Shop", 12, 72);
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted($"Daily Quests:    {total - remaining}/{total}");
        ImGui.TextUnformatted($"Premium Quests:  {premiumQuestGroupCategory->resetWinCount}/{premiumQuestGroupCategory->masterSoloAreaGroupCategory->resetMaxWinCount}");
        ImGui.End();
    }

    private static void DrawResetTimer(string name, long resetId, int hourColorThreshold)
    {
        const int padding = 19;
        var remainingTime = GetRemainingTime(resetId);
        var timeStr = remainingTime > TimeSpan.Zero ? $"{(long)remainingTime.TotalHours:D2}:{remainingTime:mm}" : "00:00";
        var colorThresholdTime = TimeSpan.FromHours(hourColorThreshold);
        var thresholdRatio = (float)(remainingTime / colorThresholdTime);
        var color = thresholdRatio < 1 ? new Vector4(1, thresholdRatio, thresholdRatio, 1) : Vector4.One;
        ImGui.TextColored(color, $"{name}:{new string(' ', padding - name.Length - timeStr.Length)}{timeStr}");
    }

    [GameSymbol("Command.Work.ResetWork$$GetOrCreateResetStore")]
    private static delegate* unmanaged<ResetWork*, long, nint, ResetWork.ResetStore*> getOrCreateResetStore;
    private static ResetWork.ResetStore* GetResetStore(long id) => getOrCreateResetStore(WorkManager.NativePtr->reset, id, 0);

    [GameSymbol("Command.Work.ResetWork$$GetRemainingMillSecond")]
    private static delegate* unmanaged<ResetWork*, long, nint, long> getRemainingMillSecond;
    private static TimeSpan GetRemainingTime(long resetId)
    {
        if (resetId != 12) // TODO: Fix
            return TimeSpan.FromMilliseconds(getRemainingMillSecond(WorkManager.NativePtr->reset, resetId, 0));

        const int offsetHours = 9;
        var utcNow = DateTimeOffset.UtcNow;
        var offsetUtcNow = utcNow.AddHours(offsetHours);
        var nextMonth = new DateTimeOffset(offsetUtcNow.Year, offsetUtcNow.Month + 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(-offsetHours);
        return nextMonth - utcNow;
    }
}