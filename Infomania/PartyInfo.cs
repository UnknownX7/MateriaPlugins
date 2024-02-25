﻿using System.Numerics;
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
                    ImGui.BeginGroup();
                    DrawStats(leftCharacter);
                    ImGui.EndGroup();
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

                    ImGui.BeginGroup();
                    DrawStats(middleCharacter);
                    ImGui.EndGroup();
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

                    ImGui.BeginGroup();
                    DrawStats(rightCharacter);
                    ImGui.EndGroup();
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
        var displayHeal = false;

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
                    if ((ParameterType)skillEffectInfo->passiveDetailType == ParameterType.HealingPower && skillEffectInfo->effectCoefficient >= 150)
                        displayHeal = true;
                    break;
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


        int baseDamage, add, coefficient;
        if (pDmg >= mDmg)
        {
            baseDamage = physicalBaseDamage;
            add = physAdd;
            coefficient = physCoefficient;
            ImGui.TextUnformatted($"Phys. Dmg.: {pDmg}");
        }
        else
        {
            baseDamage = magicalBaseDamage;
            add = magAdd;
            coefficient = magCoefficient;
            ImGui.TextUnformatted($"Mag. Dmg.: {mDmg}");
        }

        ImGui.Spacing();

        for (int i = 0; i < 8; i++)
        {
            var (a, c) = elementalPotencies[i];
            if (a == 0 && c == 0) continue;
            ImGui.TextColored(GetElementColor((ElementType)(i + 2)), $"{GetElementName((ElementType)(i + 2))}: {CalcDamage(baseDamage, 1000, a + add, c + coefficient, 0)}");
        }

        ImGui.Spacing();
        if (displayHeal)
            ImGui.TextUnformatted($"Heal: {characterInfo->totalStatus->healingPower}");
        ImGui.TextUnformatted($"Phys. Res.: {CalcAllyDamageReduction(characterInfo->totalStatus->physicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->physicalDefence, 0)} HP)");
        ImGui.TextUnformatted($"Mag. Res.: {CalcAllyDamageReduction(characterInfo->totalStatus->magicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->magicalDefence, 0)} HP)");
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