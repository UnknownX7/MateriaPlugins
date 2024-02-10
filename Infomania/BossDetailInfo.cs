using ImGuiNET;
using System.Numerics;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.Work;

namespace Infomania;

public static unsafe class BossDetailInfo
{
    public static void DrawBossDetailInfo(BossDetailModalPresenter* bossModal)
    {
        var bossModel = bossModal->contentParameterCaches != null ? bossModal->contentParameterCaches->GetPtr(0)->bossDataSelectModel : null;
        if (bossModel == null) return;

        var battleEnemyStore = (BattleWork.BattleEnemyStore*)bossModel->battleEnemyInfo;
        var enemyStore = (EnemyWork.EnemyStore*)bossModel->enemyInfo;
        var elementResistanceInfo = (ElementResistanceInfo*)enemyStore->elementResistanceInfo;
        var vf = (delegate* unmanaged<BattleWork.BattleEnemyStore*, nint, StatusParamInfo*>)battleEnemyStore->@class->vtable.get_TotalStatusParamInfo.methodPtr;
        var statusParamInfo = vf(battleEnemyStore, 0);
        if (statusParamInfo == null) return;

        ImGui.Begin("BossDetailInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);

        ImGui.BeginGroup();
        ImGui.TextUnformatted($"HP: {(statusParamInfo->dummyHp > 0 ? statusParamInfo->dummyHp : statusParamInfo->hp)}");
        ImGui.TextUnformatted($"PATK: {statusParamInfo->physicalAttack}");
        ImGui.TextUnformatted($"MATK: {statusParamInfo->magicalAttack}");
        ImGui.TextUnformatted($"PDEF: {statusParamInfo->physicalDefence}");
        ImGui.TextUnformatted($"MDEF: {statusParamInfo->magicalDefence}");
        if (statusParamInfo->healingPower != 0)
            ImGui.TextUnformatted($"HEAL: {statusParamInfo->healingPower}");
        ImGui.EndGroup();

        ImGui.SameLine();
        ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
        ImGui.SameLine();

        ImGui.BeginGroup();
        DrawElementResistText("Fire", elementResistanceInfo->resistFireMagnificationPermil);
        DrawElementResistText("Ice", elementResistanceInfo->resistIceMagnificationPermil);
        DrawElementResistText("Thunder", elementResistanceInfo->resistThunderMagnificationPermil);
        DrawElementResistText("Earth", elementResistanceInfo->resistEarthMagnificationPermil);
        DrawElementResistText("Water", elementResistanceInfo->resistWaterMagnificationPermil);
        DrawElementResistText("Wind", elementResistanceInfo->resistWindMagnificationPermil);
        DrawElementResistText("Holy", elementResistanceInfo->resistHolyMagnificationPermil);
        DrawElementResistText("Dark", elementResistanceInfo->resistDarkMagnificationPermil);
        ImGui.EndGroup();

        ImGui.End();
    }

    public static void DrawBossSelectDetailInfo(BossSelectDetailModalPresenter* bossModal)
    {
        // Info is unavailable from here
    }

    private static void DrawElementResistText(string type, int permil)
    {
        if (permil != 0)
            ImGui.TextUnformatted($"{type}: {permil / 10}%");
    }
}