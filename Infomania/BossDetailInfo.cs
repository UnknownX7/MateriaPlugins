using System;
using ImGuiNET;
using System.Numerics;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.Work;
using Materia.Game;

namespace Infomania;

public unsafe class BossDetailInfo : ModalInfo
{
    public override bool Enabled => Infomania.Config.EnableBossDetailInfo;
    public override Type[] ValidModals { get; } = [ typeof(BossDetailModalPresenter) ];

    public override void Draw(Modal modal)
    {
        if (!Il2CppType<BossDetailModalPresenter>.Is(modal.NativePtr, out var bossModal)) return;
        DrawStats(bossModal->contentParameterCaches != null ? bossModal->contentParameterCaches->GetPtr(0)->bossDataSelectModel : null);
    }

    private static int CalcEnemyBaseDamage(int attack, int defense) => attack * 2000 / (defense * 100 + 10000);
    private static int CalcEnemyDamageReduction(int defense) => 100 - (int)(32000 / (defense * 2.2f + 100));

    public static void DrawStats(BossDataSelectModel* bossModel)
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

        Infomania.BeginInfoWindow("BossDetailInfo");

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
        DrawElementResistText(ElementType.Fire, elementResistanceInfo->resistFireMagnificationPermil);
        DrawElementResistText(ElementType.Ice, elementResistanceInfo->resistIceMagnificationPermil);
        DrawElementResistText(ElementType.Thunder, elementResistanceInfo->resistThunderMagnificationPermil);
        DrawElementResistText(ElementType.Earth, elementResistanceInfo->resistEarthMagnificationPermil);
        DrawElementResistText(ElementType.Water, elementResistanceInfo->resistWaterMagnificationPermil);
        DrawElementResistText(ElementType.Wind, elementResistanceInfo->resistWindMagnificationPermil);
        DrawElementResistText(ElementType.Holy, elementResistanceInfo->resistHolyMagnificationPermil);
        DrawElementResistText(ElementType.Dark, elementResistanceInfo->resistDarkMagnificationPermil);
        ImGui.EndGroup();

        ImGui.End();
    }

    private static void DrawElementResistText(ElementType element, int permil)
    {
        if (permil != 0)
            ImGui.TextColored(PartyEditInfo.GetElementColor(element), $"{PartyEditInfo.GetElementName(element)}: {permil / 10}%%");
    }
}