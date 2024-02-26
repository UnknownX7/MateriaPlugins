using System;
using System.Collections.Generic;
using System.Numerics;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Party;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

public static unsafe class PartyInfo
{
    private static int CalcAllyBaseDamage(int attack, int defense) => (int)(attack * 50 / (defense * 2.2f + 100));
    private static int CalcAllyDamageReduction(int defense) => 100 - 2000000 / (defense * 100 + 10000);

    private static int CalcDamage(int baseDamage, int skillCoefficient, int potencyAdd, int potencyCoefficient, int stanceBonusCoefficient)
    {
        var skill = (skillCoefficient + potencyAdd) * (1000 + potencyCoefficient) / 1000;
        return baseDamage * skill / 1000 * (1000 + stanceBonusCoefficient) / 1000;
    }

    private static int CalcHP(int hp, int defense, int stanceReductionCoefficient) => (int)(hp * (1 + (defense - 100) * 0.005f) / ((1000 - stanceReductionCoefficient) / 1000f));

    public static void DrawPartySelectInfo(PartySelectScreenPresenterBase<PartySelectScreenSetupParameter>* partySelect)
    {
        var selectedParty = partySelect->partySelect->selectPartyInfo;
        if (selectedParty == null) return;

        switch (selectedParty->partyCharacterInfos->size)
        {
            case 1:
            {
                var character = selectedParty->partyCharacterInfos->GetPtr(0);
                if (character->characterId == 0) return;
                ImGui.Begin("PartySelectInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
                DrawStats(character);
                ImGui.End();
                break;
            }
            case 3:
            {
                var leftCharacter = selectedParty->partyCharacterInfos->GetPtr(1);
                var middleCharacter = selectedParty->partyCharacterInfos->GetPtr(0);
                var rightCharacter = selectedParty->partyCharacterInfos->GetPtr(2);
                if (leftCharacter->characterId == 0 && middleCharacter->characterId == 0 && rightCharacter->characterId == 0) return;

                ImGui.Begin("PartySelectInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);

                if (leftCharacter->characterId != 0)
                {
                    using (_ = ImGuiEx.GroupBlock.Begin())
                        DrawStats(leftCharacter);
                }

                if (middleCharacter->characterId != 0)
                {
                    if (leftCharacter->characterId != 0)
                    {
                        ImGui.SameLine();
                        ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
                        ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.5f, 0)));
                        ImGui.SameLine();
                    }

                    using (_ = ImGuiEx.GroupBlock.Begin())
                        DrawStats(middleCharacter);
                }

                if (rightCharacter->characterId != 0)
                {
                    if (leftCharacter->characterId != 0 || middleCharacter->characterId != 0)
                    {
                        ImGui.SameLine();
                        ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
                        ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.5f, 0)));
                        ImGui.SameLine();
                    }

                    using (_ = ImGuiEx.GroupBlock.Begin())
                        DrawStats(rightCharacter);
                }

                ImGui.End();
                break;
            }
        }
    }

    private static PartyCharacterInfo* cachedMemberInfo = null;
    public static void DrawPartyEditInfo(PartyEditTopScreenPresenterBase* partyEdit)
    {
        var characterInfo = partyEdit->currentPartyInfo->partyCharacterInfos->GetPtr(partyEdit->selectIndex);
        if (characterInfo == null || characterInfo->characterId == 0) return;

        ImGui.Begin("PartyEditInfo", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        using (_ = ImGuiEx.GroupBlock.Begin())
            DrawStats(characterInfo);
        if (partyEdit->afterSelectPartyCharacterInfo != null && partyEdit->partyEditSelectType is not (PartyEditSelectType.None or PartyEditSelectType.Character or PartyEditSelectType.Costume or PartyEditSelectType.DisplayWeapon or PartyEditSelectType.SpecialSkill))
        {
            GameInterop.RunOnUpdate(() => cachedMemberInfo = WorkManager.GetStatusParamInfo(partyEdit->afterSelectPartyCharacterInfo));
            if (cachedMemberInfo != null)
            {
                ImGui.SameLine();
                ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
                ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.5f, 0)));
                ImGui.SameLine();
                using (_ = ImGuiEx.GroupBlock.Begin())
                    DrawStats(cachedMemberInfo);
            }
        }
        else
        {
            cachedMemberInfo = null;
        }
        ImGui.End();
    }

    private static void DrawStats(PartyCharacterInfo* characterInfo)
    {
        var physAdd = 0;
        var physCoefficient = 0;
        var magAdd = 0;
        var magCoefficient = 0;
        var elementalPotencies = stackalloc (int, int)[8];
        var skillCoefficients = new int[10];
        skillCoefficients[1] = 1000; // Basic Attack

        for (int i = 0; i < characterInfo->passiveSkillEffectInfos->size; i++)
        {
            var skillEffectInfo = characterInfo->passiveSkillEffectInfos->GetPtr(i);
            switch (skillEffectInfo->passiveSkillType)
            {
                case PassiveSkillType.ElementDamage:
                    var element = skillEffectInfo->passiveDetailType - 2;
                    elementalPotencies[element].Item1 += skillEffectInfo->effectValue;
                    elementalPotencies[element].Item2 += skillEffectInfo->effectCoefficient;
                    break;
                case PassiveSkillType.PhysicalDamage:
                    physAdd += skillEffectInfo->effectValue;
                    physCoefficient += skillEffectInfo->effectCoefficient;
                    break;
                case PassiveSkillType.MagicalDamage:
                    magAdd += skillEffectInfo->effectValue;
                    magCoefficient += skillEffectInfo->effectCoefficient;
                    break;
                case PassiveSkillType.Parameter:
                case PassiveSkillType.LimitBreakDamage:
                case PassiveSkillType.SummonDamage:
                    break;
                default:
                    //ImGui.TextUnformatted($"??? {skillEffectInfo->passiveDetailType} +{skillEffectInfo->effectValue}/{skillEffectInfo->effectCoefficient / 10f}%");
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
            ImGui.TextUnformatted($"Heal: {characterInfo->totalStatus->healingPower * (int)(skillCoefficients[0] * 1.5f * 0.45f) / 1000}");
        ImGui.TextUnformatted($"Phys. Res.: {CalcAllyDamageReduction(characterInfo->totalStatus->physicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->physicalDefence, 0)} HP)");
        ImGui.TextUnformatted($"Mag. Res.: {CalcAllyDamageReduction(characterInfo->totalStatus->magicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->magicalDefence, 0)} HP)");
    }

    private static void GetMaxDamageCoefficients(BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot, SkillAttackType attackType, IList<int> coefficients)
    {
        var materiaDamageBonus = 0;
        var materiaHealingBonus = 0;
        var materiaAoE = false;
        if (materiaSupportSlot != null)
        {
            for (int i = 0; i < materiaSupportSlot->materiaSupportEfefcts->size; i++)
            {
                var supportEfefct = materiaSupportSlot->materiaSupportEfefcts->GetPtr(i);
                if (supportEfefct->materiaSupportEffectTriggerType != SupportEffectTriggerType.Always) continue;
                switch (supportEfefct->supportEffectType)
                {
                    case SupportEffectType.Damage when supportEfefct->isPercent:
                        materiaDamageBonus = supportEfefct->value;
                        break;
                    case SupportEffectType.Healing when supportEfefct->isPercent:
                        materiaHealingBonus = supportEfefct->value;
                        break;
                    case SupportEffectType.AllTarget:
                        materiaAoE = true;
                        break;
                }
            }
        }

        for (int i = 0; i < baseSkillInfo->skillEffectDetailInfos->size; i++)
        {
            var skillEffect = baseSkillInfo->skillEffectDetailInfos->GetPtr(i);
            if (!Il2CppType<SkillDamageInfo>.Is(skillEffect->skillEffectDetail, out var skillDamageInfo) || skillDamageInfo->skillDamageType != SkillDamageType.Normal) continue;
            if (skillDamageInfo->skillAttackType == attackType)
                coefficients[(int)skillDamageInfo->damageElementType] = Math.Max(coefficients[(int)skillDamageInfo->damageElementType], skillDamageInfo->damageMagnificationPermil.permilValue * (materiaDamageBonus + 1000) / 1000);
            else if (skillDamageInfo->skillAttackType == SkillAttackType.Heal && (skillEffect->skillEffectInfo->targetType == SkillTargetType.OwnAll || materiaAoE))
                coefficients[0] = Math.Max(coefficients[0], skillDamageInfo->damageMagnificationPermil.permilValue * (materiaHealingBonus + 1000) / 1000);
        }
    }

    private static bool IsMateriaSupportActive(long materiaId, WeaponMateriaSupportSlotInfo* materiaSupportSlot)
    {
        for (int i = 0; i < materiaSupportSlot->targetMateriaIds->size; i++)
            if (materiaId == materiaSupportSlot->targetMateriaIds->Get(i)) return true;
        return false;
    }

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