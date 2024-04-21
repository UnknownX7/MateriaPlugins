using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Party;
using ECGen.Generated.Command.Work;
using ECGen.Generated.System.Collections.Generic;
using ImGuiNET;
using Materia.Attributes;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

public enum SkillSlotType
{
    MainWeapon,
    AbilityWeapon,
    Materia1,
    Materia2,
    Materia3,
    LimitBreak,
    Summon
}

public class CalculationInfo
{
    public struct Multiplier
    {
        public int add;
        public int coefficient;
        public static Multiplier operator +(Multiplier left, Multiplier right) => new() { add = left.add + right.add, coefficient = left.coefficient + right.coefficient };
        public static Multiplier operator /(Multiplier left, int right) => new() { add = left.add / right, coefficient = left.coefficient / right };
    }

    public int physicalBaseDamage;
    public int magicalBaseDamage;
    public int hybridBaseDamage;
    public int heal;
    public Multiplier physicalMultiplier;
    public Multiplier magicalMultiplier;
    public Multiplier limitBreakMultiplier;
    public Multiplier summonMultiplier;
    public Multiplier healMultiplier;
    public Multiplier highwindSkillMultiplier;
    public Multiplier highwindMateriaMultiplier;
    public Multiplier highwindSpecialSkillMultiplier;
    public Multiplier[] elementalMultipliers = new Multiplier[(int)ElementType.Dark + 1];
}

[Injection]
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
    private static int CalcDamage(int baseDamage, int skillCoefficient, int stanceBonusCoefficient) => baseDamage * skillCoefficient / 1000 * (1000 + stanceBonusCoefficient) / 1000;
    private static int CalcHP(int hp, int defense, int stanceReductionCoefficient) => (int)(hp * (1 + (defense - 100) * 0.005f) / ((1000 - stanceReductionCoefficient) / 1000f));
    private static int CalcHeal(int heal, int skillCoefficient, int stanceBonusCoefficient) => heal * (skillCoefficient * 450 / 1000) / 1000 * (1000 + stanceBonusCoefficient) / 1000 + 10;
    private static int CalcRegen(int heal, float duration, int stanceBonusCoefficient) => heal * 150 / 1000 * (1000 + stanceBonusCoefficient) / 1000 * (int)(duration / 2.9f);

    public static void DrawStats(PartyCharacterInfo* characterInfo)
    {
        var info = new CalculationInfo
        {
            physicalBaseDamage = CalcAllyBaseDamage(characterInfo->totalStatus->physicalAttack, 100),
            magicalBaseDamage = CalcAllyBaseDamage(characterInfo->totalStatus->magicalAttack, 100),
            hybridBaseDamage = CalcAllyBaseDamage((characterInfo->totalStatus->physicalAttack + characterInfo->totalStatus->magicalAttack) / 2, 100),
            heal = characterInfo->totalStatus->healingPower
        };

        foreach (var p in characterInfo->passiveSkillEffectInfos->PtrEnumerable)
        {
            switch (p.ptr->passiveSkillType)
            {
                case PassiveSkillType.ElementDamage:
                    info.elementalMultipliers[p.ptr->passiveDetailType].add += p.ptr->effectValue;
                    info.elementalMultipliers[p.ptr->passiveDetailType].coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.PhysicalDamage:
                    info.physicalMultiplier.add += p.ptr->effectValue;
                    info.physicalMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.MagicalDamage:
                    info.magicalMultiplier.add += p.ptr->effectValue;
                    info.magicalMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.LimitBreakDamage:
                    info.limitBreakMultiplier.add += p.ptr->effectValue;
                    info.limitBreakMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.SummonDamage:
                    info.summonMultiplier.add += p.ptr->effectValue;
                    info.summonMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.Parameter:
                    break;
            }
        }

        // TODO: Kill me
        for (int i = 0; i < characterInfo->highwindKeyItemEffects->count; i++)
        {
            var entry = (Unmanaged_Entry<HighwindKeyItemEffectType, int>*)((byte*)characterInfo->highwindKeyItemEffects->entries->items + i * 16);
            var key = (HighwindKeyItemEffectType)(int)entry->key;
            var value = ((int*)entry)[3];
            switch (key)
            {
                case HighwindKeyItemEffectType.WeaponAbilityPowerUp:
                    info.highwindSkillMultiplier.coefficient += value;
                    break;
                case HighwindKeyItemEffectType.SpecialSkillPowerUp:
                    info.highwindSpecialSkillMultiplier.coefficient += value;
                    break;
                case HighwindKeyItemEffectType.MateriaSkillPowerUp:
                    info.highwindMateriaMultiplier.coefficient += value;
                    break;
                default:
                    continue;
            }
        }

        if (characterInfo->mainWeaponInfo0 != null)
            DrawCalculatedDamage(SkillSlotType.MainWeapon, characterInfo->mainWeaponInfo0->weaponSkill->activeSkillInfo->baseSkillInfo, null, info);
        if (characterInfo->mainWeaponInfo1 != null)
            DrawCalculatedDamage(SkillSlotType.AbilityWeapon, characterInfo->mainWeaponInfo1->weaponSkill->activeSkillInfo->baseSkillInfo, null, info);
        if (characterInfo->materiaInfo0 != null)
            DrawCalculatedDamage(SkillSlotType.Materia1, characterInfo->materiaInfo0->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo0->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot0) ? characterInfo->mainWeaponInfo0->materiaSupportSlot0 : null, info);
        if (characterInfo->materiaInfo1 != null)
            DrawCalculatedDamage(SkillSlotType.Materia2, characterInfo->materiaInfo1->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo1->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot1) ? characterInfo->mainWeaponInfo0->materiaSupportSlot1 : null, info);
        if (characterInfo->materiaInfo2 != null)
            DrawCalculatedDamage(SkillSlotType.Materia3, characterInfo->materiaInfo2->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo2->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot2) ? characterInfo->mainWeaponInfo0->materiaSupportSlot2 : null, info);
        if (characterInfo->specialSkillInfo != null)
            DrawCalculatedDamage(characterInfo->specialSkillInfo->specialSkillType == SkillSpecialType.LimitBreak ? SkillSlotType.LimitBreak : SkillSlotType.Summon, characterInfo->specialSkillInfo->baseSkillInfo, null, info);

        ImGui.Spacing();
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

    private static void DrawCalculatedDamage(SkillSlotType skillSlotType, BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot, CalculationInfo calculationInfo)
    {
        var materiaDamageMultiplier = new CalculationInfo.Multiplier();
        var materiaHealingMultiplier = new CalculationInfo.Multiplier();
        if (materiaSupportSlot != null)
        {
            foreach (var p in materiaSupportSlot->materiaSupportEfefcts->PtrEnumerable.Where(p => p.ptr->materiaSupportEffectTriggerType == SupportEffectTriggerType.Always))
            {
                switch (p.ptr->supportEffectType)
                {
                    case SupportEffectType.Damage:
                        if (p.ptr->isPercent)
                            materiaDamageMultiplier.coefficient += p.ptr->value;
                        else
                            materiaDamageMultiplier.add += p.ptr->value;
                        break;
                    case SupportEffectType.Healing:
                        if (p.ptr->isPercent)
                            materiaHealingMultiplier.coefficient += p.ptr->value;
                        else
                            materiaHealingMultiplier.add += p.ptr->value;
                        break;
                }
            }
        }

        var conditionalCoefficient = 1000;
        var regenDuration = 0f;
        foreach (var p in baseSkillInfo->skillEffectDetailInfos->PtrEnumerable)
        {
            if (Il2CppType<SkillChangeEffectInfo>.Is(p.ptr->skillEffectDetail, out var skillChangeEffectInfo))
            {
                switch (skillChangeEffectInfo->skillAdditionalType)
                {
                    case SkillAdditionalType.DamageRateChange:
                        conditionalCoefficient = conditionalCoefficient * skillChangeEffectInfo->additiveValue / 1000;
                        break;
                }
            }
            else if (Il2CppType<SkillStatusChangeEffectInfo>.Is(p.ptr->skillEffectDetail, out var skillStatusChangeEffectInfo))
            {
                switch (skillStatusChangeEffectInfo->statusChangeType)
                {
                    case SkillStatusChangeType.RegenerateHealPower:
                        regenDuration = skillStatusChangeEffectInfo->duration;
                        break;
                }
            }
        }

        foreach (var p in baseSkillInfo->skillEffectDetailInfos->PtrEnumerable)
        {
            if (!Il2CppType<SkillDamageInfo>.Is(p.ptr->skillEffectDetail, out var skillDamageInfo) || skillDamageInfo->skillDamageType != SkillDamageType.Normal) continue;

            string damageType;
            var heal = false;
            int baseDamage;
            CalculationInfo.Multiplier passiveMultiplier;
            CalculationInfo.Multiplier materiaMultiplier;
            switch (skillDamageInfo->skillAttackType)
            {
                case SkillAttackType.Physical:
                    damageType = "Phys.";
                    baseDamage = calculationInfo.physicalBaseDamage;
                    passiveMultiplier = calculationInfo.physicalMultiplier;
                    materiaMultiplier = materiaDamageMultiplier;
                    break;
                case SkillAttackType.Magical:
                    damageType = "Mag.";
                    baseDamage = calculationInfo.magicalBaseDamage;
                    passiveMultiplier = calculationInfo.magicalMultiplier;
                    materiaMultiplier = materiaDamageMultiplier;
                    break;
                case SkillAttackType.Both:
                    damageType = "Phys./Mag.";
                    baseDamage = calculationInfo.hybridBaseDamage;
                    passiveMultiplier = (calculationInfo.physicalMultiplier + calculationInfo.magicalMultiplier) / 2;
                    materiaMultiplier = materiaDamageMultiplier;
                    break;
                case SkillAttackType.Heal:
                    damageType = "Heal";
                    heal = true;
                    baseDamage = calculationInfo.heal;
                    passiveMultiplier = calculationInfo.healMultiplier;
                    materiaMultiplier = materiaHealingMultiplier;
                    break;
                default:
                    damageType = "?";
                    baseDamage = 0;
                    passiveMultiplier = default;
                    materiaMultiplier = default;
                    break;
            }

            passiveMultiplier += calculationInfo.elementalMultipliers[(int)skillDamageInfo->damageElementType];

            var skillCoefficient = 0;
            switch (skillSlotType)
            {
                case SkillSlotType.MainWeapon:
                case SkillSlotType.AbilityWeapon:
                    skillCoefficient = CalcSkillDamageCoefficient(skillDamageInfo->damageMagnificationPermil.permilValue, passiveMultiplier, default, calculationInfo.highwindSkillMultiplier);
                    break;
                case SkillSlotType.Materia1:
                case SkillSlotType.Materia2:
                case SkillSlotType.Materia3:
                    skillCoefficient = CalcSkillDamageCoefficient(skillDamageInfo->damageMagnificationPermil.permilValue, passiveMultiplier, materiaMultiplier, calculationInfo.highwindMateriaMultiplier);
                    break;
                case SkillSlotType.LimitBreak:
                    skillCoefficient = CalcSkillDamageCoefficient(skillDamageInfo->damageMagnificationPermil.permilValue, passiveMultiplier + calculationInfo.limitBreakMultiplier, default, calculationInfo.highwindSpecialSkillMultiplier);
                    break;
                case SkillSlotType.Summon:
                    skillCoefficient = CalcSkillDamageCoefficient(skillDamageInfo->damageMagnificationPermil.permilValue, passiveMultiplier + calculationInfo.summonMultiplier, default, calculationInfo.highwindSpecialSkillMultiplier);
                    break;
            }

            var elementText = skillDamageInfo->damageElementType <= ElementType.No ? string.Empty : GetElementName(skillDamageInfo->damageElementType);
            var expectedAmount = skillDamageInfo->skillAttackType != SkillAttackType.Heal
                ? CalcDamage(baseDamage, skillCoefficient, 500)
                : CalcHeal(baseDamage, skillCoefficient, 500);
            var color = skillDamageInfo->skillAttackType != SkillAttackType.Heal
                ? GetElementColor(skillDamageInfo->damageElementType)
                : new Vector4(0, 1, 0, 1);

            var additionalInfo = string.Empty;
            var additionalAmount = expectedAmount;
            if (conditionalCoefficient != 1000)
                additionalAmount = additionalAmount * conditionalCoefficient / 1000;
            if (heal && regenDuration > 0)
                additionalAmount += CalcRegen(baseDamage, regenDuration, 500);
            if (additionalAmount != expectedAmount)
                additionalInfo = $" ({additionalAmount}*)";

            ImGui.TextUnformatted(GetSlotName(skillSlotType));
            ImGui.SameLine();
            ImGui.TextColored(color, $"{expectedAmount}{additionalInfo} {damageType} {elementText}");
            break;
        }
    }

    private static bool IsMateriaSupportActive(long materiaId, WeaponMateriaSupportSlotInfo* materiaSupportSlot) => materiaSupportSlot->targetMateriaIds->Enumerable.Any(id => id == materiaId);

    public static string GetSlotName(SkillSlotType skillSlotType) => skillSlotType switch
    {
        SkillSlotType.MainWeapon => "M:",
        SkillSlotType.AbilityWeapon => "A:",
        SkillSlotType.Materia1 => "1:",
        SkillSlotType.Materia2 => "2:",
        SkillSlotType.Materia3 => "3:",
        SkillSlotType.LimitBreak => "S:",
        SkillSlotType.Summon => "S:",
        _ => "?:"
    };

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

    [GameSymbol("Command.Battle.ActiveSkillDamageEffect$$get_DamageMagnificationPermil")]
    private static delegate* unmanaged<ActiveSkillDamageEffect*, nint, int> getDamageMagnificationPermil;
    public static int CalcSkillDamageCoefficient(int baseSkillCoefficient, CalculationInfo.Multiplier passiveMultiplier, CalculationInfo.Multiplier materiaMultiplier, CalculationInfo.Multiplier highwindMultiplier)
    {
        var skillDamageInfo = new SkillDamageInfo
        {
            damageMagnificationPermil = new Permil { permilValue = baseSkillCoefficient }
        };

        var activeSkillModel = new ActiveSkillModel
        {
            // Passive Damage Boost
            damageMagnificationPermilPassiveSkillEffectValue = passiveMultiplier.add,
            damageMagnificationPermilPassiveSkillEffectCoefficient = passiveMultiplier.coefficient,

            // Materia Support Boost
            damageMagnificationPermilSupportEffectValue = materiaMultiplier.add,
            damageMagnificationPermilSupportEffectCoefficient = materiaMultiplier.coefficient,

            // Enemy Permanent Buff Boost
            //damageMagnificationPermilPermanentBuffElementDamageEffectValue = 0,
            //damageMagnificationPermilPermanentBuffElementDamageEffectCoefficient = 0,

            // Highwind Boosts
            damageMagnificationPermilHighwindKeyItemEffectCoefficient = highwindMultiplier.coefficient,

            // Floor Effect Boost
            //damageMagnificationPermilBattleFieldEffectCoefficient = 0,

            // Dungeon Buff Boost
            //damageMagnificationPermilDungeonBuffEffectCoefficient = 0
        };

        var activeSkillDamageEffect = new ActiveSkillDamageEffect
        {
            skillAttackType = SkillAttackType.Physical,
            skillDamageInfo = &skillDamageInfo,
            srcActiveSkillModel = &activeSkillModel,
        };

        return getDamageMagnificationPermil(&activeSkillDamageEffect, 0);
    }
}