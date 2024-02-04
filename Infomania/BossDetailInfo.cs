using ECGen.Generated;
using ImGuiNET;
using System.Numerics;

namespace Infomania;

public static unsafe class BossDetailInfo
{
    public static void DrawBossDetailInfo(Command_OutGame_BossDetailModalPresenter* bossModal)
    {
        var bossModel = bossModal->contentParameterCaches != null ? bossModal->contentParameterCaches->Get(0)->bossDataSelectModel : null;
        if (bossModel == null) return;

        var battleEnemyStore = (Command_Work_BattleWork_BattleEnemyStore*)bossModel->battleEnemyInfo;
        var enemyStore = (Command_Work_EnemyWork_EnemyStore*)bossModel->enemyInfo;
        var elementResistanceInfo = (Command_Work_ElementResistanceInfo*)enemyStore->elementResistanceInfo;
        var vf = (delegate* unmanaged<Command_Work_BattleWork_BattleEnemyStore*, nint, Command_Work_StatusParamInfo*>)battleEnemyStore->klass->vtable.get_TotalStatusParamInfo.methodPtr;
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

    public static void DrawBossSelectDetailInfo(Command_OutGame_BossSelectDetailModalPresenter* bossModal)
    {
        // Info is unavailable from here
    }

    private static void DrawElementResistText(string type, int permil)
    {
        if (permil != 0)
            ImGui.TextUnformatted($"{type}: {permil / 10}%");
    }
}