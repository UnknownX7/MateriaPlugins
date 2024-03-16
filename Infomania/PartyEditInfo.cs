using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Party;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

public unsafe class PartyEditInfo : ScreenInfo
{
    public override bool Enabled => Infomania.Config.EnablePartyEditInfo;

    public override Type[] ValidScreens { get; } =
    [
        typeof(PartyEditTopScreenPresenter),
        typeof(PartyEditTopScreenMultiPresenter),
        typeof(MultiAreaBattlePartyEditPresenter)
    ];

    private PartyCharacterInfo* cachedAfterSelectPartyCharacterInfo;
    private Il2CppObject<PartyCharacterInfo>? cachedMemberInfo;
    public override void Draw(Screen screen)
    {
        if (!Il2CppType<PartyEditTopScreenPresenterBase>.Is(screen.NativePtr, out var partyEdit)) return;

        var characterInfo = partyEdit->currentPartyInfo->partyCharacterInfos->GetPtr(partyEdit->selectIndex);
        if (characterInfo == null || characterInfo->characterId == 0) return;

        Infomania.BeginInfoWindow("PartyEditInfo");
        using (_ = ImGuiEx.GroupBlock.Begin())
            DrawStats(characterInfo);
        if (partyEdit->afterSelectPartyCharacterInfo != null && partyEdit->partyEditSelectType is not (PartyEditSelectType.None or PartyEditSelectType.Character or PartyEditSelectType.Costume or PartyEditSelectType.DisplayWeapon or PartyEditSelectType.SpecialSkill))
        {
            if (cachedAfterSelectPartyCharacterInfo != partyEdit->afterSelectPartyCharacterInfo)
            {
                GameInterop.RunOnUpdate(() =>
                {
                    if (cachedMemberInfo != null)
                    {
                        lock (cachedMemberInfo)
                        {
                            cachedMemberInfo.Dispose();
                            cachedMemberInfo = new Il2CppObject<PartyCharacterInfo>(WorkManager.GetStatusParamInfo(partyEdit->afterSelectPartyCharacterInfo));
                        }
                    }
                    else
                    {
                        cachedMemberInfo = new Il2CppObject<PartyCharacterInfo>(WorkManager.GetStatusParamInfo(partyEdit->afterSelectPartyCharacterInfo));
                    }
                });
                cachedAfterSelectPartyCharacterInfo = partyEdit->afterSelectPartyCharacterInfo;
            }

            if (cachedMemberInfo != null)
            {
                lock (cachedMemberInfo)
                {
                    ImGui.SameLine();
                    ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
                    ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.5f, 0)));
                    ImGui.SameLine();
                    using (_ = ImGuiEx.GroupBlock.Begin())
                        DrawStats(cachedMemberInfo);
                }
            }
        }
        ImGui.End();
    }

    public override void Dispose() => cachedMemberInfo?.Dispose();

    private static int CalcAllyBaseDamage(int attack, int defense) => (int)(attack * 50 / (defense * 2.2f + 100));
    private static int CalcAllyDamageReduction(int defense) => 100 - 2000000 / (defense * 100 + 10000);

    private static int CalcDamage(int baseDamage, int skillCoefficient, int potencyAdd, int potencyCoefficient, int stanceBonusCoefficient)
    {
        var skill = (skillCoefficient + potencyAdd) * (1000 + potencyCoefficient) / 1000;
        return baseDamage * skill / 1000 * (1000 + stanceBonusCoefficient) / 1000;
    }

    private static int CalcHP(int hp, int defense, int stanceReductionCoefficient) => (int)(hp * (1 + (defense - 100) * 0.005f) / ((1000 - stanceReductionCoefficient) / 1000f));

    public static void DrawStats(PartyCharacterInfo* characterInfo)
    {
        var physAdd = 0;
        var physCoefficient = 0;
        var magAdd = 0;
        var magCoefficient = 0;
        var elementalPotencies = stackalloc (int, int)[8];
        var skillCoefficients = new int[10];
        skillCoefficients[1] = 1000; // Basic Attack

        foreach (var p in characterInfo->passiveSkillEffectInfos->PtrEnumerable)
        {
            switch (p.ptr->passiveSkillType)
            {
                case PassiveSkillType.ElementDamage:
                    var element = p.ptr->passiveDetailType - 2;
                    elementalPotencies[element].Item1 += p.ptr->effectValue;
                    elementalPotencies[element].Item2 += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.PhysicalDamage:
                    physAdd += p.ptr->effectValue;
                    physCoefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.MagicalDamage:
                    magAdd += p.ptr->effectValue;
                    magCoefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.Parameter:
                case PassiveSkillType.LimitBreakDamage:
                case PassiveSkillType.SummonDamage:
                    break;
            }
        }

        var physicalBaseDamage = CalcAllyBaseDamage(characterInfo->totalStatus->physicalAttack, 100);
        var magicalBaseDamage = CalcAllyBaseDamage(characterInfo->totalStatus->magicalAttack, 100);
        //var hybridBaseDamage = CalcAllyBaseDamage((characterInfo->totalStatus->physicalAttack + characterInfo->totalStatus->magicalAttack) / 2, 100);

        var pDmg = CalcDamage(physicalBaseDamage, 1000, physAdd, physCoefficient, 0);
        var mDmg = CalcDamage(magicalBaseDamage, 1000, magAdd, magCoefficient, 0);
        //var hDmg = CalcDamage(hybridBaseDamage, 1000, (physAdd + magAdd) / 2, (physCoefficient + magCoefficient) / 2, 0);

        SkillAttackType attackType;
        int baseDamage, add, coefficient;
        if (pDmg >= mDmg)
        {
            baseDamage = physicalBaseDamage;
            add = physAdd;
            coefficient = physCoefficient;
            attackType = SkillAttackType.Physical;
        }
        else
        {
            baseDamage = magicalBaseDamage;
            add = magAdd;
            coefficient = magCoefficient;
            attackType = SkillAttackType.Magical;
        }

        if (characterInfo->mainWeaponInfo0 != null)
            GetMaxDamageCoefficients(characterInfo->mainWeaponInfo0->weaponSkill->activeSkillInfo->baseSkillInfo, null, attackType, skillCoefficients);
        if (characterInfo->mainWeaponInfo1 != null)
            GetMaxDamageCoefficients(characterInfo->mainWeaponInfo1->weaponSkill->activeSkillInfo->baseSkillInfo, null, attackType, skillCoefficients);
        if (characterInfo->materiaInfo0 != null)
            GetMaxDamageCoefficients(characterInfo->materiaInfo0->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo0->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot0) ? characterInfo->mainWeaponInfo0->materiaSupportSlot0 : null, attackType, skillCoefficients);
        if (characterInfo->materiaInfo1 != null)
            GetMaxDamageCoefficients(characterInfo->materiaInfo1->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo1->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot1) ? characterInfo->mainWeaponInfo0->materiaSupportSlot1 : null, attackType, skillCoefficients);
        if (characterInfo->materiaInfo2 != null)
            GetMaxDamageCoefficients(characterInfo->materiaInfo2->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo2->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot2) ? characterInfo->mainWeaponInfo0->materiaSupportSlot2 : null, attackType, skillCoefficients);

        ImGui.TextUnformatted($"{(attackType == SkillAttackType.Physical ? "Phys. Dmg" : "Mag. Dmg")}.: {CalcDamage(baseDamage, skillCoefficients[(int)ElementType.No], add, coefficient, 500)}");
        ImGui.Spacing();

        for (int i = 0; i < 8; i++)
        {
            var skillCoefficient = skillCoefficients[i + 2];
            if (skillCoefficient == 0) continue;
            var (a, c) = elementalPotencies[i];
            var element = (ElementType)(i + 2);
            ImGui.TextColored(GetElementColor(element), $"{GetElementName(element)}: {CalcDamage(baseDamage, skillCoefficient, a + add, c + coefficient, 500)}");
        }

        ImGui.Spacing();
        if (skillCoefficients[0] != 0)
            ImGui.TextUnformatted($"Heal: {characterInfo->totalStatus->healingPower * (int)(skillCoefficients[0] * 1.5f * 0.45f) / 1000 + 10}");
        ImGui.TextUnformatted($"Phys. Res.: {CalcAllyDamageReduction(characterInfo->totalStatus->physicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->physicalDefence, 0)} HP)");
        ImGui.TextUnformatted($"Mag. Res.: {CalcAllyDamageReduction(characterInfo->totalStatus->magicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->magicalDefence, 0)} HP)");
    }

    public static void DrawStats(PartyCharacterInfo*[] characterInfos)
    {
        var first = true;
        foreach (var characterInfo in characterInfos)
        {
            if (characterInfo->characterId == 0) continue;

            if (!first)
            {
                ImGui.SameLine();
                ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
                ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.5f, 0)));
                ImGui.SameLine();
            }

            using (_ = ImGuiEx.GroupBlock.Begin())
                DrawStats(characterInfo);
            first = false;
        }
    }

    public static void DrawStats(IReadOnlyList<nint> characterInfos)
    {
        if (characterInfos.Count == 0) return;
        var array = new PartyCharacterInfo*[characterInfos.Count];
        for (int i = 0; i < characterInfos.Count; i++)
            array[i] = (PartyCharacterInfo*)characterInfos[i];
        DrawStats(array);
    }

    private static void GetMaxDamageCoefficients(BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot, SkillAttackType attackType, IList<int> coefficients)
    {
        var materiaDamageBonus = 0;
        var materiaHealingBonus = 0;
        var materiaAoE = false;
        if (materiaSupportSlot != null)
        {
            foreach (var p in materiaSupportSlot->materiaSupportEfefcts->PtrEnumerable.Where(p => p.ptr->materiaSupportEffectTriggerType == SupportEffectTriggerType.Always))
            {
                switch (p.ptr->supportEffectType)
                {
                    case SupportEffectType.Damage when p.ptr->isPercent:
                        materiaDamageBonus = p.ptr->value;
                        break;
                    case SupportEffectType.Healing when p.ptr->isPercent:
                        materiaHealingBonus = p.ptr->value;
                        break;
                    case SupportEffectType.AllTarget:
                        materiaAoE = true;
                        break;
                }
            }
        }

        foreach (var p in baseSkillInfo->skillEffectDetailInfos->PtrEnumerable)
        {
            if (!Il2CppType<SkillDamageInfo>.Is(p.ptr->skillEffectDetail, out var skillDamageInfo) || skillDamageInfo->skillDamageType != SkillDamageType.Normal) continue;
            if (skillDamageInfo->skillAttackType == attackType)
                coefficients[(int)skillDamageInfo->damageElementType] = Math.Max(coefficients[(int)skillDamageInfo->damageElementType], skillDamageInfo->damageMagnificationPermil.permilValue * (materiaDamageBonus + 1000) / 1000);
            else if (skillDamageInfo->skillAttackType == SkillAttackType.Heal && (p.ptr->skillEffectInfo->targetType == SkillTargetType.OwnAll || materiaAoE))
                coefficients[0] = Math.Max(coefficients[0], skillDamageInfo->damageMagnificationPermil.permilValue * (materiaHealingBonus + 1000) / 1000);
        }
    }

    private static bool IsMateriaSupportActive(long materiaId, WeaponMateriaSupportSlotInfo* materiaSupportSlot) => materiaSupportSlot->targetMateriaIds->Enumerable.Any(id => id == materiaId);

    public static string GetElementName(ElementType element) => element switch
    {
        ElementType.No => "Non. Elem.",
        ElementType.Fire => "Fire",
        ElementType.Ice => "Ice",
        ElementType.Thunder => "Lightning",
        ElementType.Earth => "Earth",
        ElementType.Water => "Water",
        ElementType.Wind => "Wind",
        ElementType.Holy => "Holy",
        ElementType.Dark => "Dark",
        _ => "?"
    };

    public static Vector4 GetElementColor(ElementType element) => element switch
    {
        ElementType.Fire => new Vector4(1, 0.4f, 0.4f, 1),
        ElementType.Ice => new Vector4(0.4f, 0.55f, 1, 1),
        ElementType.Thunder => new Vector4(0.9f, 1, 0.15f, 1),
        ElementType.Earth => new Vector4(1, 0.6f, 0.25f, 1),
        ElementType.Water => new Vector4(0.4f, 1, 1, 1),
        ElementType.Wind => new Vector4(0.4f, 0.9f, 0.45f, 1),
        ElementType.Holy => new Vector4(1, 1, 0.67f, 1),
        ElementType.Dark => new Vector4(0.9f, 0.4f, 0.9f, 1),
        _ => Vector4.One
    };
}