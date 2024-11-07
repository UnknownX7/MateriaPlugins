using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ECGen.Generated;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.MultiBattle;
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

public class CharacterCalculator
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
        public Multiplier materiaMultiplier;
        public Multiplier brandMultiplier;
        public int Coefficient => multiplier.Multiply(baseCoefficient);
        public int ConditionalCoefficient => (multiplier + conditional).Multiply(baseConditionalCoefficient);

        public unsafe SkillInfo(BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot, Unmanaged_Array<WeaponAttachmentEffectInfo>* brands)
        {
            baseAttackType = baseSkillInfo->baseAttackType;
            materiaMultiplier = ToMultiplier(materiaSupportSlot);
            brandMultiplier = ToMultiplier(brands);

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

                        attackType = skillDamageInfo->skillAttackType;
                        elementType = skillDamageInfo->damageElementType;
                        baseCoefficient = skillDamageInfo->damageMagnificationPermil.permilValue;
                        baseConditionalCoefficient = baseCoefficient;

                        if (skillDamageInfo->skillDamageEffectChangeParameterType != SkillDamageEffectChangeParameterType.None)
                        {
                            var multiplier = 0;
                            if (skillDamageInfo->skillDamageChangeInfoList != null && skillDamageInfo->skillDamageChangeInfoList->size > 0)
                                multiplier += skillDamageInfo->skillDamageChangeInfoList->PtrEnumerable.Max(c => c.ptr->damageChangePermil.permilValue);
                            if (skillDamageInfo->skillDamageDefenseChangeInfoList != null && skillDamageInfo->skillDamageDefenseChangeInfoList->size > 0)
                                multiplier += skillDamageInfo->skillDamageDefenseChangeInfoList->PtrEnumerable.Max(c => c.ptr->damageChangePermil.permilValue);

                            baseConditionalCoefficient = baseConditionalCoefficient * multiplier / 1000;
                        }

                        foundDamageEffect = true; // TODO: Multiple damage types?
                        break;
                    case SkillEffectType.Additional:
                        if (!Il2CppType<SkillChangeEffectInfo>.Is(p.ptr->skillEffectDetail, out var skillChangeEffectInfo)) continue;

                        switch (skillChangeEffectInfo->skillAdditionalType)
                        {
                            case SkillAdditionalType.DamageChange:
                                if (isConditional)
                                    conditional.add += skillChangeEffectInfo->additiveValue;
                                else
                                    multiplier.add += skillChangeEffectInfo->additiveValue;
                                break;
                            case SkillAdditionalType.DamageRateChange:
                                if (isConditional)
                                {
                                    conditional.coefficient += skillChangeEffectInfo->additiveValue - 1000;
                                    if (skillEffectInfo->skillTriggerType == SkillTriggerType.CriticalHit)
                                        shouldDisplayCrit = true;
                                }
                                else
                                {
                                    multiplier.coefficient += skillChangeEffectInfo->additiveValue - 1000;
                                }
                                break;
                            case SkillAdditionalType.AdditionalDamage:
                                if (isConditional)
                                    conditionalFlat += skillChangeEffectInfo->additiveValue;
                                else
                                    flat += skillChangeEffectInfo->additiveValue;
                                break;
                        }
                        break;
                    case SkillEffectType.StatusChange:
                        if (!Il2CppType<SkillStatusChangeEffectInfo>.Is(p.ptr->skillEffectDetail, out var skillStatusChangeEffectInfo)) continue;

                        switch (skillStatusChangeEffectInfo->statusChangeType)
                        {
                            case SkillStatusChangeType.RegenerateHealPower:
                                regenCoefficient = skillStatusChangeEffectInfo->effectCoefficient * (int)(skillStatusChangeEffectInfo->duration / 2.9f);
                                break;
                        }
                        break;
                }
            }
        }

        private unsafe Multiplier ToMultiplier(Unmanaged_Array<WeaponAttachmentEffectInfo>* brands)
        {
            var damageMultiplier = new Multiplier { coefficient = 1000 };
            if (brands == null || attackType is not (SkillAttackType.Physical or SkillAttackType.Magical or SkillAttackType.Both)) return damageMultiplier;

            foreach (var p in brands->PtrEnumerable)
            {
                switch (p.ptr->weaponAttachmentEffectType)
                {
                    case WeaponAttachmentEffectType.StatusUpCabilityPowerUp:
                        damageMultiplier.coefficient += (int)p.ptr->value;
                        break;
                }
            }

            return damageMultiplier;
        }

        private unsafe Multiplier ToMultiplier(WeaponMateriaSupportSlotInfo* materiaSupportSlot)
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
    }

    public StatusParamInfo statCache;
    public Dictionary<SkillSlotType, SkillInfo> skillCache = new();

    public int physicalBaseDamage;
    public int magicalBaseDamage;
    public int hybridBaseDamage;

    public int physicalResist;
    public int physicalHp;
    public int magicalResist;
    public int magicalHp;

    public Multiplier highwindSkillMultiplier;
    public Multiplier highwindMateriaMultiplier;
    public Multiplier highwindSpecialSkillMultiplier;

    public PassiveMultiplierInfo multipliers = new();
    public PassiveMultiplierInfo conditional = new();

    public PassiveMultiplierInfo materiaPotencyMultipliers = new();

    public unsafe CharacterCalculator(PartyCharacterInfo* characterInfo)
    {
        statCache = *characterInfo->totalStatus;
        physicalBaseDamage = CalcAllyBaseDamage(statCache.physicalAttack, 100);
        magicalBaseDamage = CalcAllyBaseDamage(statCache.magicalAttack, 100);
        hybridBaseDamage = CalcAllyBaseDamage((statCache.physicalAttack + statCache.magicalAttack) / 2, 100);

        physicalResist = CalcAllyDamageReduction(statCache.physicalDefence);
        physicalHp = CalcHP(statCache.hp, statCache.physicalDefence, 0);
        magicalResist = CalcAllyDamageReduction(statCache.magicalDefence);
        magicalHp = CalcHP(statCache.hp, statCache.magicalDefence, 0);

        foreach (var p in characterInfo->passiveSkillEffectInfos->PtrEnumerable)
        {
            var m = p.ptr->passiveSkillTriggerType switch
            {
                PassiveSkillTriggerType.Always => multipliers,
                PassiveSkillTriggerType.Tactics => multipliers,
                _ => conditional
            };

            switch (p.ptr->passiveSkillType)
            {
                case PassiveSkillType.ElementDamage:
                    m.elementalMultipliers[p.ptr->passiveDetailType].add += p.ptr->effectValue;
                    m.elementalMultipliers[p.ptr->passiveDetailType].coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.PhysicalDamage:
                    m.physicalMultiplier.add += p.ptr->effectValue;
                    m.physicalMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.MagicalDamage:
                    m.magicalMultiplier.add += p.ptr->effectValue;
                    m.magicalMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.LimitBreakDamage:
                    m.limitBreakMultiplier.add += p.ptr->effectValue;
                    m.limitBreakMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.SummonDamage:
                    m.summonMultiplier.add += p.ptr->effectValue;
                    m.summonMultiplier.coefficient += p.ptr->effectCoefficient;
                    break;
                case PassiveSkillType.Parameter:
                    break;
            }
        }

        var materiaParameters = Enumerable.Empty<Ptr<MateriaParameterInfo>>();
        if (characterInfo->materiaInfo0 != null)
            materiaParameters = materiaParameters.Concat(characterInfo->materiaInfo0->materiaParameters->PtrEnumerable);
        if (characterInfo->materiaInfo1 != null)
            materiaParameters = materiaParameters.Concat(characterInfo->materiaInfo1->materiaParameters->PtrEnumerable);
        if (characterInfo->materiaInfo2 != null)
            materiaParameters = materiaParameters.Concat(characterInfo->materiaInfo2->materiaParameters->PtrEnumerable);

        foreach (var p in materiaParameters)
        {
            var materiaParameter = p.ptr;
            switch (materiaParameter->type)
            {
                case MateriaParameterType.ElementDamage:
                    if (materiaParameter->grantValueType == GrantValueType.PercentageValue)
                        materiaPotencyMultipliers.elementalMultipliers[materiaParameter->detailType].coefficient += materiaParameter->value;
                    else
                        materiaPotencyMultipliers.elementalMultipliers[materiaParameter->detailType].add += materiaParameter->value;
                    break;
                case MateriaParameterType.LimitBreakDamage:
                    if (materiaParameter->grantValueType == GrantValueType.PercentageValue)
                        materiaPotencyMultipliers.limitBreakMultiplier.coefficient += materiaParameter->value;
                    else
                        materiaPotencyMultipliers.limitBreakMultiplier.add += materiaParameter->value;
                    break;
                case MateriaParameterType.SummonDamage:
                    if (materiaParameter->grantValueType == GrantValueType.PercentageValue)
                        materiaPotencyMultipliers.summonMultiplier.coefficient += materiaParameter->value;
                    else
                        materiaPotencyMultipliers.summonMultiplier.add += materiaParameter->value;
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
                    highwindSkillMultiplier.coefficient += value;
                    break;
                case HighwindKeyItemEffectType.SpecialSkillPowerUp:
                    highwindSpecialSkillMultiplier.coefficient += value;
                    break;
                case HighwindKeyItemEffectType.MateriaSkillPowerUp:
                    highwindMateriaMultiplier.coefficient += value;
                    break;
                default:
                    continue;
            }
        }

        if (characterInfo->mainWeaponInfo0 != null)
            CacheSkill(SkillSlotType.MainWeapon, characterInfo->mainWeaponInfo0);
        if (characterInfo->mainWeaponInfo1 != null)
            CacheSkill(SkillSlotType.AbilityWeapon, characterInfo->mainWeaponInfo1);
        if (characterInfo->legendaryWeaponInfo0 != null)
            CacheSkill(SkillSlotType.UltimateWeapon, characterInfo->legendaryWeaponInfo0);
        if (characterInfo->specialSkillInfo != null)
            CacheSkill(characterInfo->specialSkillInfo);
        if (characterInfo->materiaInfo0 != null)
            CacheSkill(SkillSlotType.Materia1, characterInfo->materiaInfo0, characterInfo->mainWeaponInfo0->materiaSupportSlot0);
        if (characterInfo->materiaInfo1 != null)
            CacheSkill(SkillSlotType.Materia2, characterInfo->materiaInfo1, characterInfo->mainWeaponInfo0->materiaSupportSlot1);
        if (characterInfo->materiaInfo2 != null)
            CacheSkill(SkillSlotType.Materia3, characterInfo->materiaInfo2, characterInfo->mainWeaponInfo0->materiaSupportSlot2);
    }

    private unsafe void CacheSkill(SkillSlotType slotType, WeaponInfo* weaponInfo) =>
        CacheSkill(slotType, slotType != SkillSlotType.UltimateWeapon
                ? weaponInfo->weaponSkill->activeSkillInfo->baseSkillInfo
                : weaponInfo->weaponSkill->legendarySkillInfo->baseSkillInfo,
            null, weaponInfo->weaponAttachmentEffectInfos);

    private unsafe void CacheSkill(SpecialSkillInfo* specialSkillInfo) =>
        CacheSkill(specialSkillInfo->specialSkillType == SkillSpecialType.LimitBreak ? SkillSlotType.LimitBreak : SkillSlotType.Summon, specialSkillInfo->baseSkillInfo, null, null);

    private unsafe void CacheSkill(SkillSlotType slotType, MateriaInfo* materiaInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot) =>
        CacheSkill(slotType, materiaInfo->activeSkillInfo->baseSkillInfo, IsMateriaSupportActive(materiaInfo->materiaId, materiaSupportSlot) ? materiaSupportSlot : null, null);

    private unsafe void CacheSkill(SkillSlotType slotType, BaseSkillInfo* baseSkillInfo, WeaponMateriaSupportSlotInfo* materiaSupportSlot, Unmanaged_Array<WeaponAttachmentEffectInfo>* brands) =>
        skillCache[slotType] = new SkillInfo(baseSkillInfo, materiaSupportSlot, brands);

    public (int damage, int conditionalDamage) CalculateDamage(SkillSlotType slotType)
    {
        if (!skillCache.TryGetValue(slotType, out var skillInfo)) return default;

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
                baseDamage = statCache.healingPower;
                passiveMultiplier = multipliers.healMultiplier;
                conditionalMultiplier = conditional.healMultiplier;
                break;
            default:
                passiveMultiplier = default;
                conditionalMultiplier = default;
                break;
        }

        Multiplier highwindMultiplier;
        Multiplier materiaPotencyMultiplier = default;
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
                materiaMultiplier = skillInfo.materiaMultiplier;
                break;
            case SkillSlotType.LimitBreak:
                highwindMultiplier = highwindSpecialSkillMultiplier;
                passiveMultiplier += multipliers.limitBreakMultiplier;
                conditionalMultiplier += conditional.limitBreakMultiplier;
                materiaPotencyMultiplier = materiaPotencyMultipliers.limitBreakMultiplier;
                break;
            case SkillSlotType.Summon:
                highwindMultiplier = highwindSpecialSkillMultiplier;
                passiveMultiplier += multipliers.summonMultiplier;
                conditionalMultiplier += conditional.summonMultiplier;
                materiaPotencyMultiplier = materiaPotencyMultipliers.summonMultiplier;
                break;
            //case SkillSlotType.UltimateWeapon:
            default:
                highwindMultiplier = default;
                break;
        }

        passiveMultiplier += multipliers.elementalMultipliers[(int)skillInfo.elementType];
        conditionalMultiplier += conditional.elementalMultipliers[(int)skillInfo.elementType];

        materiaPotencyMultiplier += materiaPotencyMultipliers.elementalMultipliers[(int)skillInfo.elementType];

        conditionalMultiplier += passiveMultiplier;

        var finalCoefficient = CalcSkillDamageCoefficient(skillInfo.Coefficient, passiveMultiplier, skillInfo.brandMultiplier, materiaMultiplier, highwindMultiplier, materiaPotencyMultiplier);
        var finalConditionalCoefficient = CalcSkillDamageCoefficient(skillInfo.ConditionalCoefficient, conditionalMultiplier, skillInfo.brandMultiplier, materiaMultiplier, highwindMultiplier, materiaPotencyMultiplier);

        if (skillInfo.shouldDisplayCrit)
            finalConditionalCoefficient = finalConditionalCoefficient * statCache.criticalDamageMagnificationPermil / 1000;

        return skillInfo.attackType != SkillAttackType.Heal
         ? (CalcDamage(baseDamage, finalCoefficient, 500) + skillInfo.flat, CalcDamage(baseDamage, finalConditionalCoefficient, 500) + skillInfo.conditionalFlat)
         : (CalcHeal(baseDamage, finalCoefficient, 500) + skillInfo.flat, CalcHeal(baseDamage, finalConditionalCoefficient, 500) + skillInfo.conditionalFlat + CalcRegen(baseDamage, skillInfo.regenCoefficient, 500));
    }

    public static int CalcAllyBaseDamage(int attack, int defense) => (int)(attack * 50 / (defense * 2.2f + 100));
    public static int CalcAllyDamageReduction(int defense) => 100 - 2000000 / (defense * 100 + 10000);
    public static int CalcDamage(int baseDamage, int skillCoefficient, int stanceBonusCoefficient) => baseDamage * skillCoefficient / 1000 * (1000 + stanceBonusCoefficient) / 1000;
    public static int CalcHP(int hp, int defense, int stanceReductionCoefficient) => (int)(hp * (1 + (defense - 100) * 0.005f) / ((1000 - stanceReductionCoefficient) / 1000f));
    public static int CalcHeal(int heal, int skillCoefficient, int stanceBonusCoefficient) => heal * (skillCoefficient * 450 / 1000) / 1000 * (1000 + stanceBonusCoefficient) / 1000 + 10;
    public static int CalcRegen(int heal, int coefficient, int stanceBonusCoefficient) => heal * (coefficient * 500 / 1000) / 1000 * (1000 + stanceBonusCoefficient) / 1000;

    private static unsafe bool IsMateriaSupportActive(long materiaId, WeaponMateriaSupportSlotInfo* materiaSupportSlot) => materiaSupportSlot->targetMateriaIds->Enumerable.Any(id => id == materiaId);

    public static unsafe int CalcSkillDamageCoefficient(int baseSkillCoefficient, Multiplier passiveMultiplier, Multiplier brandMultiplier, Multiplier materiaMultiplier, Multiplier highwindMultiplier, Multiplier materiaPotencyMultiplier)
    {
        var skillDamageInfo = new SkillDamageInfo
        {
            damageMagnificationPermil = new Permil { permilValue = brandMultiplier.Multiply(baseSkillCoefficient) }
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
            //damageMagnificationPermilDungeonBuffEffectCoefficient = 0,

            // Materia Damage Potency Stats
            damageMagnificationPermilMateriaParameterEffectValue = materiaPotencyMultiplier.add,
            damageMagnificationPermilMateriaParameterEffectCoefficient = materiaPotencyMultiplier.coefficient
        };

        var activeSkillDamageEffect = new ActiveSkillDamageEffect
        {
            skillAttackType = SkillAttackType.Physical,
            skillDamageInfo = &skillDamageInfo,
            srcActiveSkillModel = &activeSkillModel,
        };

        return PartyEditInfo.getDamageMagnificationPermil(&activeSkillDamageEffect, 0);
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

    private CharacterCalculator? lockedCalculator;
    private PartyCharacterInfo* cachedAfterSelectPartyCharacterInfo;
    private CharacterCalculator? cachedCalculator;

    //public override void Activate() => lockedCalculator = null;

    public override void Draw(Screen screen)
    {
        if (!Il2CppType<PartyEditTopScreenPresenterBase>.Is(screen.NativePtr, out var partyEdit)) return;

        var characterInfo = partyEdit->currentPartyInfo->partyCharacterInfos->GetPtr(partyEdit->selectIndex);
        if (characterInfo == null || characterInfo->characterId == 0) return;

        var characterCalculator = new CharacterCalculator(characterInfo);

        Infomania.BeginInfoWindow("PartyEditInfo", () =>
        {
            if (ImGui.Selectable(lockedCalculator == null ? "Pin Current Stats" : "Unpin stats"))
                lockedCalculator = lockedCalculator == null ? characterCalculator : null;
        });

        using (_ = ImGuiEx.GroupBlock.Begin())
            DrawStats(characterCalculator);

        if (partyEdit->afterSelectPartyCharacterInfo != null && partyEdit->partyEditSelectType is not (PartyEditSelectType.None or PartyEditSelectType.Character or PartyEditSelectType.Costume or PartyEditSelectType.DisplayWeapon))
        {
            if (cachedAfterSelectPartyCharacterInfo != partyEdit->afterSelectPartyCharacterInfo)
            {
                cachedCalculator = new CharacterCalculator(GetStatusParamInfo(partyEdit->afterSelectPartyCharacterInfo, partyEdit->currentPartyInfo->partyCharacterInfos));
                cachedAfterSelectPartyCharacterInfo = partyEdit->afterSelectPartyCharacterInfo;
            }

            if (cachedCalculator != null)
            {
                ImGui.SameLine();
                ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
                ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.5f, 0)));
                ImGui.SameLine();
                using (_ = ImGuiEx.GroupBlock.Begin())
                    DrawStats(cachedCalculator);
            }
        }

        if (lockedCalculator != null)
        {
            ImGui.SameLine();
            ImGui.Dummy(Vector2.One * ImGuiEx.Scale * 10);
            ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.25f, 0)));
            ImGuiEx.AddVerticalLine(ImGuiEx.GetItemRectPosPercent(new Vector2(0.75f, 0)));
            ImGui.SameLine();
            using (_ = ImGuiEx.GroupBlock.Begin())
                DrawStats(lockedCalculator);
        }
        ImGui.End();
    }

    public static void DrawStats(CharacterCalculator calculator)
    {
        foreach (var (slot, _) in calculator.skillCache)
            DrawCalculatedDamage(slot, calculator);

        ImGui.Spacing();
        ImGui.TextUnformatted($"P.Res: {calculator.physicalResist}% ({calculator.physicalHp} HP)");
        ImGui.TextUnformatted($"M.Res: {calculator.magicalResist}% ({calculator.magicalHp} HP)");
    }

    public static void DrawStats(PartyCharacterInfo*[] characterInfos, MultiAreaBattleMatchingRoomScreenPresenter* multi = null)
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
                DrawStats(new CharacterCalculator(multi == null ? characterInfo : GetStatusParamInfo(characterInfo, multi)));
            first = false;
        }
    }

    public static void DrawStats(IReadOnlyList<nint> characterInfos, MultiAreaBattleMatchingRoomScreenPresenter* multi = null)
    {
        if (characterInfos.Count == 0) return;
        var array = new PartyCharacterInfo*[characterInfos.Count];
        for (int i = 0; i < characterInfos.Count; i++)
            array[i] = (PartyCharacterInfo*)characterInfos[i];
        DrawStats(array, multi);
    }

    private static void DrawCalculatedDamage(SkillSlotType slotType, CharacterCalculator calculator)
    {
        var skillInfo = calculator.skillCache[slotType];
        var (damage, conditionalDamage) = calculator.CalculateDamage(slotType);

        var elementText = skillInfo.elementType <= ElementType.No ? string.Empty : GetElementName(skillInfo.elementType);
        var color = skillInfo.attackType != SkillAttackType.Heal ? GetElementColor(skillInfo.elementType) : new Vector4(0, 1, 0, 1);

        var additionalInfo = string.Empty;
        if (damage != conditionalDamage)
            additionalInfo = $" ({conditionalDamage}*)";

        ImGui.TextColored(new Vector4(0.75f), GetSlotName(slotType));
        ImGui.SameLine();
        ImGui.TextColored(color, $"{damage}{additionalInfo} {GetAttackTypeName(skillInfo.attackType, skillInfo.baseAttackType)}{elementText}");
    }

    public static string GetSlotName(SkillSlotType skillSlotType) => skillSlotType switch
    {
        SkillSlotType.MainWeapon => "M:",
        SkillSlotType.AbilityWeapon => "A:",
        SkillSlotType.UltimateWeapon => "U:",
        SkillSlotType.Materia1 => "1:",
        SkillSlotType.Materia2 => "2:",
        SkillSlotType.Materia3 => "3:",
        SkillSlotType.LimitBreak or SkillSlotType.Summon => "L:",
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
    public static delegate* unmanaged<ActiveSkillDamageEffect*, nint, int> getDamageMagnificationPermil;

    [GameSymbol("Command.Work.PartyWork$$GetPartyPassiveSkillEffectInfoListDictionary")]
    private static delegate* unmanaged<PartyWork*, Unmanaged_Array<PartyCharacterInfo>*, nint, Unmanaged_Dictionary<long, Unmanaged_List<PassiveSkillEffectInfo>>*> getPartyPassiveSkillEffectInfoListDictionary;

    [GameSymbol("Command.OutGame.MultiBattle.MultiAreaBattleMatchingRoomScreenPresenter$$GetPartyPassiveSkillEffectInfoListDictionary")]
    private static delegate* unmanaged<MultiAreaBattleMatchingRoomScreenPresenter*, Unmanaged_IReadOnlyList<RoomMember>*, nint, Unmanaged_Dictionary<long, Unmanaged_List<PassiveSkillEffectInfo>>*> multiGetPartyPassiveSkillEffectInfoListDictionary;

    [GameSymbol("Command.Work.PartyWork$$GetOtherCharacterAllTargetTypePassiveSkillList")]
    private static delegate* unmanaged<PartyWork*, Unmanaged_Dictionary<long, Unmanaged_List<PassiveSkillEffectInfo>>*, long, nint, Unmanaged_List<PassiveSkillEffectInfo>*> getOtherCharacterAllTargetTypePassiveSkillList;

    [GameSymbol("Command.Work.PartyWork$$GetStatusParamInfo")]
    private static delegate* unmanaged<PartyWork*, PartyCharacterInfo*, Unmanaged_List<PassiveSkillEffectInfo>*, Unmanaged_IReadOnlyList<ISkillArmouryInfo>*, nint, PartyCharacterInfo*> getStatusParamInfo;

    private static PartyCharacterInfo* GetStatusParamInfo(PartyCharacterInfo* characterInfo, Unmanaged_Array<PartyCharacterInfo>* characterInfos)
    {
        var party = WorkManager.NativePtr->party;
        return getStatusParamInfo(party, characterInfo, getOtherCharacterAllTargetTypePassiveSkillList(party, getPartyPassiveSkillEffectInfoListDictionary(party, characterInfos, 0), characterInfo->partyMemberId, 0), null, 0);
    }

    private static PartyCharacterInfo* GetStatusParamInfo(PartyCharacterInfo* characterInfo, MultiAreaBattleMatchingRoomScreenPresenter* multi)
    {
        var party = WorkManager.NativePtr->party;
        return getStatusParamInfo(party, characterInfo, getOtherCharacterAllTargetTypePassiveSkillList(party, multiGetPartyPassiveSkillEffectInfoListDictionary(multi, multi->prevRoomMembers, 0), characterInfo->partyMemberId, 0), null, 0);
    }
}