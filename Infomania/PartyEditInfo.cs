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
    UltimateWeapon,
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
        public int Multiply(int other) => (other + add) * coefficient / 1000;
    }

    public class PassiveMultiplierInfo
    {
        public Multiplier physicalMultiplier;
        public Multiplier magicalMultiplier;
        public Multiplier limitBreakMultiplier;
        public Multiplier summonMultiplier;
        public Multiplier healMultiplier;
        public Multiplier[] elementalMultipliers = new Multiplier[(int)ElementType.Dark + 1];
    }

    public class SkillInfo
    {
        public SkillSlotType slotType;
        public SkillAttackType attackType;
        public SkillBaseAttackType baseAttackType;
        public ElementType elementType;
        public int baseCoefficient;
        public int regenCoefficient;
        public int flat;
        public int baseConditionalCoefficient;
        public int conditionalFlat;
        public bool shouldDisplayCrit;
        public Multiplier multiplier = new() { coefficient = 1000 };
        public Multiplier conditional;
        public int Coefficient => multiplier.Multiply(baseCoefficient);
        public int ConditionalCoefficient => (multiplier + conditional).Multiply(baseConditionalCoefficient);
    }

    public int physicalBaseDamage;
    public int magicalBaseDamage;
    public int hybridBaseDamage;
    public int heal;
    public int criticalCoefficient;

    public Multiplier highwindSkillMultiplier;
    public Multiplier highwindMateriaMultiplier;
    public Multiplier highwindSpecialSkillMultiplier;

    public PassiveMultiplierInfo multipliers = new();
    public PassiveMultiplierInfo conditional = new();

    public static unsafe SkillInfo ToSkillInfo(SkillSlotType slotType, BaseSkillInfo* baseSkillInfo)
    {
        var skillInfo = new SkillInfo { slotType = slotType, baseAttackType = baseSkillInfo->baseAttackType };

        var foundDamageEffect = false;
        foreach (var p in baseSkillInfo->skillEffectDetailInfos->PtrEnumerable)
        {
            var skillEffectInfo = p.ptr->skillEffectInfo;
            var isConditional = skillEffectInfo->skillTriggerType switch
            {
                SkillTriggerType.None => false,
                SkillTriggerType.Hit => false,
                SkillTriggerType.Tactics => false,
                _ => true
            };

            switch (skillEffectInfo->skillEffectType)
            {
                case SkillEffectType.Damage:
                    if (foundDamageEffect || !Il2CppType<SkillDamageInfo>.Is(p.ptr->skillEffectDetail, out var skillDamageInfo) || skillDamageInfo->skillDamageType != SkillDamageType.Normal) continue;

                    skillInfo.attackType = skillDamageInfo->skillAttackType;
                    skillInfo.elementType = skillDamageInfo->damageElementType;
                    skillInfo.baseCoefficient = skillDamageInfo->damageMagnificationPermil.permilValue;
                    skillInfo.baseConditionalCoefficient = skillInfo.baseCoefficient;

                    if (skillDamageInfo->skillDamageEffectChangeParameterType != SkillDamageEffectChangeParameterType.None)
                    {
                        var multiplier = 0;
                        if (skillDamageInfo->skillDamageChangeInfoList != null && skillDamageInfo->skillDamageChangeInfoList->size > 0)
                            multiplier += skillDamageInfo->skillDamageChangeInfoList->PtrEnumerable.Max(c => c.ptr->damageChangePermil.permilValue);
                        if (skillDamageInfo->skillDamageDefenseChangeInfoList != null && skillDamageInfo->skillDamageDefenseChangeInfoList->size > 0)
                            multiplier += skillDamageInfo->skillDamageDefenseChangeInfoList->PtrEnumerable.Max(c => c.ptr->damageChangePermil.permilValue);

                        skillInfo.baseConditionalCoefficient = skillInfo.baseConditionalCoefficient * multiplier / 1000;
                    }

                    foundDamageEffect = true; // TODO: Multiple damage types?
                    break;
                case SkillEffectType.Additional:
                    if (!Il2CppType<SkillChangeEffectInfo>.Is(p.ptr->skillEffectDetail, out var skillChangeEffectInfo)) continue;

                    switch (skillChangeEffectInfo->skillAdditionalType)
                    {
                        case SkillAdditionalType.DamageChange:
                            if (isConditional)
                                skillInfo.conditional.add += skillChangeEffectInfo->additiveValue;
                            else
                                skillInfo.multiplier.add += skillChangeEffectInfo->additiveValue;
                            break;
                        case SkillAdditionalType.DamageRateChange:
                            if (isConditional)
                            {
                                skillInfo.conditional.coefficient += skillChangeEffectInfo->additiveValue - 1000;
                                if (skillEffectInfo->skillTriggerType == SkillTriggerType.CriticalHit)
                                    skillInfo.shouldDisplayCrit = true;
                            }
                            else
                            {
                                skillInfo.multiplier.coefficient += skillChangeEffectInfo->additiveValue - 1000;
                            }
                            break;
                        case SkillAdditionalType.AdditionalDamage:
                            if (isConditional)
                                skillInfo.conditionalFlat += skillChangeEffectInfo->additiveValue;
                            else
                                skillInfo.flat += skillChangeEffectInfo->additiveValue;
                            break;
                    }
                    break;
                case SkillEffectType.StatusChange:
                    if (!Il2CppType<SkillStatusChangeEffectInfo>.Is(p.ptr->skillEffectDetail, out var skillStatusChangeEffectInfo)) continue;

                    switch (skillStatusChangeEffectInfo->statusChangeType)
                    {
                        case SkillStatusChangeType.RegenerateHealPower:
                            skillInfo.regenCoefficient = skillStatusChangeEffectInfo->effectCoefficient * (int)(skillStatusChangeEffectInfo->duration / 2.9f);
                            break;
                    }
                    break;
            }
        }

        return skillInfo;
    }

    public static unsafe Multiplier ToMultiplier(SkillAttackType attackType, WeaponMateriaSupportSlotInfo* materiaSupportSlot)
    {
        if (materiaSupportSlot == null) return default;

        var supportSlotDamageMultiplier = new Multiplier();
        var supportSlotHealingMultiplier = new Multiplier();
        foreach (var p in materiaSupportSlot->materiaSupportEfefcts->PtrEnumerable)
        {
            switch (p.ptr->supportEffectType)
            {
                case SupportEffectType.Damage:
                    if (p.ptr->isPercent)
                        supportSlotDamageMultiplier.coefficient += p.ptr->value;
                    else
                        supportSlotDamageMultiplier.add += p.ptr->value;
                    break;
                case SupportEffectType.Healing:
                    if (p.ptr->isPercent)
                        supportSlotHealingMultiplier.coefficient += p.ptr->value;
                    else
                        supportSlotHealingMultiplier.add += p.ptr->value;
                    break;
            }
        }

        return attackType != SkillAttackType.Heal ? supportSlotDamageMultiplier : supportSlotHealingMultiplier;
    }

    public unsafe (int damage, int conditionalDamage) CalculateDamage(SkillSlotType slotType, BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot) =>
        CalculateDamage(slotType, ToSkillInfo(slotType, baseSkillInfo), materiaSupportSlot);

    public unsafe (int damage, int conditionalDamage) CalculateDamage(SkillSlotType slotType, SkillInfo skillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot)
    {
        var supportSlotMultiplier = ToMultiplier(skillInfo.attackType, materiaSupportSlot);
        Multiplier materiaMultiplier = default;

        var baseDamage = 0;
        Multiplier passiveMultiplier;
        Multiplier conditionalMultiplier;
        switch (skillInfo.attackType)
        {
            case SkillAttackType.Physical:
                baseDamage = physicalBaseDamage;
                passiveMultiplier = multipliers.physicalMultiplier;
                conditionalMultiplier = conditional.physicalMultiplier;
                break;
            case SkillAttackType.Magical:
                baseDamage = magicalBaseDamage;
                passiveMultiplier = multipliers.magicalMultiplier;
                conditionalMultiplier = conditional.magicalMultiplier;
                break;
            case SkillAttackType.Both:
                baseDamage = hybridBaseDamage;
                passiveMultiplier = (multipliers.physicalMultiplier + multipliers.magicalMultiplier) / 2;
                conditionalMultiplier = (conditional.physicalMultiplier + conditional.magicalMultiplier) / 2;
                break;
            case SkillAttackType.Heal:
                baseDamage = heal;
                passiveMultiplier = multipliers.healMultiplier;
                conditionalMultiplier = conditional.healMultiplier;
                break;
            default:
                passiveMultiplier = default;
                conditionalMultiplier = default;
                break;
        }

        Multiplier highwindMultiplier;
        switch (slotType)
        {
            case SkillSlotType.MainWeapon:
            case SkillSlotType.AbilityWeapon:
                highwindMultiplier = highwindSkillMultiplier;
                break;
            case SkillSlotType.Materia1:
            case SkillSlotType.Materia2:
            case SkillSlotType.Materia3:
                highwindMultiplier = highwindMateriaMultiplier;
                materiaMultiplier = supportSlotMultiplier;
                break;
            case SkillSlotType.LimitBreak:
                highwindMultiplier = highwindSpecialSkillMultiplier;
                passiveMultiplier += multipliers.limitBreakMultiplier;
                conditionalMultiplier += conditional.limitBreakMultiplier;
                break;
            case SkillSlotType.Summon:
                highwindMultiplier = highwindSpecialSkillMultiplier;
                passiveMultiplier += multipliers.summonMultiplier;
                conditionalMultiplier += conditional.summonMultiplier;
                break;
            //case SkillSlotType.UltimateWeapon:
            default:
                highwindMultiplier = default;
                break;
        }

        passiveMultiplier += multipliers.elementalMultipliers[(int)skillInfo.elementType];
        conditionalMultiplier += conditional.elementalMultipliers[(int)skillInfo.elementType];

        conditionalMultiplier += passiveMultiplier;

        var finalCoefficient = PartyEditInfo.CalcSkillDamageCoefficient(skillInfo.Coefficient, passiveMultiplier, materiaMultiplier, highwindMultiplier);
        var finalConditionalCoefficient = PartyEditInfo.CalcSkillDamageCoefficient(skillInfo.ConditionalCoefficient, conditionalMultiplier, materiaMultiplier, highwindMultiplier);

        if (skillInfo.shouldDisplayCrit)
            finalConditionalCoefficient = finalConditionalCoefficient * criticalCoefficient / 1000;

        return skillInfo.attackType != SkillAttackType.Heal
         ? (PartyEditInfo.CalcDamage(baseDamage, finalCoefficient, 500) + skillInfo.flat, PartyEditInfo.CalcDamage(baseDamage, finalConditionalCoefficient, 500) + skillInfo.conditionalFlat)
         : (PartyEditInfo.CalcHeal(baseDamage, finalCoefficient, 500) + skillInfo.flat, PartyEditInfo.CalcHeal(baseDamage, finalConditionalCoefficient, 500) + skillInfo.conditionalFlat + PartyEditInfo.CalcRegen(baseDamage, skillInfo.regenCoefficient, 500));
    }
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

    public static int CalcAllyBaseDamage(int attack, int defense) => (int)(attack * 50 / (defense * 2.2f + 100));
    public static int CalcAllyDamageReduction(int defense) => 100 - 2000000 / (defense * 100 + 10000);
    public static int CalcDamage(int baseDamage, int skillCoefficient, int stanceBonusCoefficient) => baseDamage * skillCoefficient / 1000 * (1000 + stanceBonusCoefficient) / 1000;
    public static int CalcHP(int hp, int defense, int stanceReductionCoefficient) => (int)(hp * (1 + (defense - 100) * 0.005f) / ((1000 - stanceReductionCoefficient) / 1000f));
    public static int CalcHeal(int heal, int skillCoefficient, int stanceBonusCoefficient) => heal * (skillCoefficient * 450 / 1000) / 1000 * (1000 + stanceBonusCoefficient) / 1000 + 10;
    public static int CalcRegen(int heal, int coefficient, int stanceBonusCoefficient) => heal * (coefficient * 500 / 1000) / 1000 * (1000 + stanceBonusCoefficient) / 1000;

    public static void DrawStats(PartyCharacterInfo* characterInfo)
    {
        var info = new CalculationInfo
        {
            physicalBaseDamage = CalcAllyBaseDamage(characterInfo->totalStatus->physicalAttack, 100),
            magicalBaseDamage = CalcAllyBaseDamage(characterInfo->totalStatus->magicalAttack, 100),
            hybridBaseDamage = CalcAllyBaseDamage((characterInfo->totalStatus->physicalAttack + characterInfo->totalStatus->magicalAttack) / 2, 100),
            heal = characterInfo->totalStatus->healingPower,
            criticalCoefficient = characterInfo->totalStatus->criticalDamageMagnificationPermil
        };

        foreach (var p in characterInfo->passiveSkillEffectInfos->PtrEnumerable)
        {
            var multipliers = p.ptr->passiveSkillTriggerType switch
            {
                PassiveSkillTriggerType.Always => info.multipliers,
                PassiveSkillTriggerType.Tactics => info.multipliers,
                _ => info.conditional
            };

            switch (p.ptr->passiveSkillType)
            {
                case PassiveSkillType.ElementDamage:
                    multipliers.elementalMultipliers[p.ptr->passiveDetailType].add += p.ptr->effectValue;
                    multipliers.elementalMultipliers[p.ptr->passiveDetailType].coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.PhysicalDamage:
                    multipliers.physicalMultiplier.add += p.ptr->effectValue;
                    multipliers.physicalMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.MagicalDamage:
                    multipliers.magicalMultiplier.add += p.ptr->effectValue;
                    multipliers.magicalMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.LimitBreakDamage:
                    multipliers.limitBreakMultiplier.add += p.ptr->effectValue;
                    multipliers.limitBreakMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.SummonDamage:
                    multipliers.summonMultiplier.add += p.ptr->effectValue;
                    multipliers.summonMultiplier.coefficient += p.ptr->effectCoefficient;
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
        if (characterInfo->legendaryWeaponInfo0 != null)
            DrawCalculatedDamage(SkillSlotType.UltimateWeapon, characterInfo->legendaryWeaponInfo0->weaponSkill->legendarySkillInfo->baseSkillInfo, null, info);
        if (characterInfo->specialSkillInfo != null)
            DrawCalculatedDamage(characterInfo->specialSkillInfo->specialSkillType == SkillSpecialType.LimitBreak ? SkillSlotType.LimitBreak : SkillSlotType.Summon, characterInfo->specialSkillInfo->baseSkillInfo, null, info);
        if (characterInfo->materiaInfo0 != null)
            DrawCalculatedDamage(SkillSlotType.Materia1, characterInfo->materiaInfo0->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo0->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot0) ? characterInfo->mainWeaponInfo0->materiaSupportSlot0 : null, info);
        if (characterInfo->materiaInfo1 != null)
            DrawCalculatedDamage(SkillSlotType.Materia2, characterInfo->materiaInfo1->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo1->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot1) ? characterInfo->mainWeaponInfo0->materiaSupportSlot1 : null, info);
        if (characterInfo->materiaInfo2 != null)
            DrawCalculatedDamage(SkillSlotType.Materia3, characterInfo->materiaInfo2->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(characterInfo->materiaInfo2->materiaId, characterInfo->mainWeaponInfo0->materiaSupportSlot2) ? characterInfo->mainWeaponInfo0->materiaSupportSlot2 : null, info);

        ImGui.Spacing();
        ImGui.TextUnformatted($"P.Res: {CalcAllyDamageReduction(characterInfo->totalStatus->physicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->physicalDefence, 0)} HP)");
        ImGui.TextUnformatted($"M.Res: {CalcAllyDamageReduction(characterInfo->totalStatus->magicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->magicalDefence, 0)} HP)");
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

    private static void DrawCalculatedDamage(SkillSlotType slotType, BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot, CalculationInfo calculationInfo)
    {
        var skillInfo = CalculationInfo.ToSkillInfo(slotType, baseSkillInfo);
        var (damage, conditionalDamage) = calculationInfo.CalculateDamage(slotType, skillInfo, materiaSupportSlot);

        var elementText = skillInfo.elementType <= ElementType.No ? string.Empty : GetElementName(skillInfo.elementType);
        var color = skillInfo.attackType != SkillAttackType.Heal ? GetElementColor(skillInfo.elementType) : new Vector4(0, 1, 0, 1);

        var additionalInfo = string.Empty;
        if (damage != conditionalDamage)
            additionalInfo = $" ({conditionalDamage}*)";

        ImGui.TextColored(new Vector4(0.75f), GetSlotName(slotType));
        ImGui.SameLine();
        ImGui.TextColored(color, $"{damage}{additionalInfo} {GetAttackTypeName(skillInfo.attackType, skillInfo.baseAttackType)}{elementText}");
    }

    private static bool IsMateriaSupportActive(long materiaId, WeaponMateriaSupportSlotInfo* materiaSupportSlot) => materiaSupportSlot->targetMateriaIds->Enumerable.Any(id => id == materiaId);

    public static string GetSlotName(SkillSlotType skillSlotType) => skillSlotType switch
    {
        SkillSlotType.MainWeapon => "M:",
        SkillSlotType.AbilityWeapon => "A:",
        SkillSlotType.UltimateWeapon => "U:",
        SkillSlotType.Materia1 => "1:",
        SkillSlotType.Materia2 => "2:",
        SkillSlotType.Materia3 => "3:",
        SkillSlotType.LimitBreak => "L:",
        SkillSlotType.Summon => "L:",
        _ => "?:"
    };

    public static string GetAttackTypeName(SkillAttackType attackType, SkillBaseAttackType baseAttackType) => attackType switch
    {
        SkillAttackType.Physical => "P.",
        SkillAttackType.Magical => "M.",
        SkillAttackType.Both => "P./M.",
        SkillAttackType.Heal => baseAttackType switch
        {
            SkillBaseAttackType.Physical => "P.Heal",
            SkillBaseAttackType.Magical => "M.Heal",
            SkillBaseAttackType.Both => "P./M.Heal",
            _ => "? Heal"
        },
        _ => "?"
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