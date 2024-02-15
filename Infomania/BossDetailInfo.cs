using ImGuiNET;
using System.Numerics;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Boss;
using ECGen.Generated.Command.Work;
using Materia.Attributes;
using Materia.Game;
using Materia;

namespace Infomania;

[Injection]
public static unsafe class BossDetailInfo
{
    private static BossDataSelectModel* cachedBossModel;

    private static int CalcEnemyBaseDamage(int attack, int defense) => attack * 2000 / (defense * 100 + 10000);
    private static int CalcEnemyDamageReduction(int defense) => 100 - (int)(32000 / (defense * 2.2f + 100));
    public static void DrawBossDetailInfo(BossDetailModalPresenter* bossModal) => DrawStats(bossModal->contentParameterCaches != null ? bossModal->contentParameterCaches->GetPtr(0)->bossDataSelectModel : null);
    public static void DrawBossSelectDetailInfo() => DrawStats(cachedBossModel);

    private static void DrawStats(BossDataSelectModel* bossModel)
    {
        if (bossModel == null) return;

        StatusParamInfo* statusParamInfo = null;
        ElementResistanceInfo* elementResistanceInfo = null;
        if (Il2CppType<BattleWork.BattleEnemyStore>.Is(bossModel->battleEnemyInfo, out var battleEnemyStore)) // In menus
        {
            var enemyStore = (EnemyWork.EnemyStore*)bossModel->enemyInfo;
            elementResistanceInfo = (ElementResistanceInfo*)enemyStore->elementResistanceInfo;
            var vf = (delegate* unmanaged<BattleWork.BattleEnemyStore*, nint, StatusParamInfo*>)battleEnemyStore->@class->vtable.get_TotalStatusParamInfo.methodPtr;
            statusParamInfo = vf(battleEnemyStore, 0);
        }
        else if (Il2CppType<BattleEnemyInfo>.Is(bossModel->battleEnemyInfo, out var battleEnemyInfo)) // In battle
        {
            elementResistanceInfo = battleEnemyInfo->elementResistanceInfo;
            statusParamInfo = battleEnemyInfo->totalStatus;
        }

        if (statusParamInfo == null) return;

        ImGui.Begin("BossDetailInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);

        ImGui.BeginGroup();
        ImGui.TextUnformatted($"HP: {(statusParamInfo->dummyHp > 0 ? statusParamInfo->dummyHp : statusParamInfo->hp)}");
        ImGui.TextUnformatted($"PATK: {statusParamInfo->physicalAttack}");
        ImGui.TextUnformatted($"MATK: {statusParamInfo->magicalAttack}");
        ImGui.TextUnformatted($"PDEF: {statusParamInfo->physicalDefence} ({CalcEnemyDamageReduction(statusParamInfo->physicalDefence)}%)");
        ImGui.TextUnformatted($"MDEF: {statusParamInfo->magicalDefence} ({CalcEnemyDamageReduction(statusParamInfo->magicalDefence)}%)");
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

    private static void DrawElementResistText(string type, int permil)
    {
        if (permil != 0)
            ImGui.TextUnformatted($"{type}: {permil / 10}%");
    }

    private delegate void SetupContentDelegate(BossDetailDescriptionContent* bossDetailDescriptionContent, BossDataSelectModel* model, nint method);
    [GameSymbol("Command.OutGame.Boss.BossDetailDescriptionContent$$SetupContent")]
    private static IMateriaHook<SetupContentDelegate>? SetupContentHook;
    private static void SetupContentDetour(BossDetailDescriptionContent* bossDetailDescriptionContent, BossDataSelectModel* model, nint method)
    {
        cachedBossModel = model;
        SetupContentHook!.Original(bossDetailDescriptionContent, model, method);
    }
}