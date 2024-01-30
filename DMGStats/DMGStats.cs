using ECGen.Generated;
using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using Materia.Utilities;
using System.Numerics;

namespace DMGStats;

public unsafe class DMGStats : IMateriaPlugin
{
    public string Name => "DMGStats";
    public string Description => "Displays extra information when editing equipment";

    public DMGStats(PluginServiceManager pluginServiceManager) => pluginServiceManager.EventHandler.Draw += Draw;

    // TODO: Use enums
    public void Draw()
    {
        var screenManager = GameInterop.ScreenManager;
        if (screenManager == null || screenManager->currentScreen == null || DebugUtil.GetTypeName(screenManager->currentScreen) is not ("Command.OutGame.Party.PartyEditTopScreenPresenter" or "Command.OutGame.Party.MultiAreaBattlePartyEditPresenter")) return;

        var partyEdit = (Command_OutGame_Party_PartyEditTopScreenPresenter*)screenManager->currentScreen;
        var characterInfo = partyEdit->currentPartyInfo->partyCharacterInfos->Get(partyEdit->selectIndex);
        if (characterInfo == null || characterInfo->characterId == 0) return;

        if (partyEdit->afterSelectPartyCharacterInfo != null && partyEdit->partyEditSelectType is 2 or 4 or 6 or 7 or 8 or 9)
            characterInfo = (Command_Work_PartyCharacterInfo*)partyEdit->rightPanelParameter->centerPanel->partyEditPassiveSkillComparisonPanel->afterPartyCharacterInfo;

        ImGui.Begin("DMGStats", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);

        var physAdd = 0;
        var physCoefficient = 0;
        var magAdd = 0;
        var magCoefficient = 0;
        var elementalPotencies = stackalloc (int, int)[8];

        for (int i = 0; i < characterInfo->passiveSkillEffectInfos->max_length; i++)
        {
            var skillEffectInfo = characterInfo->passiveSkillEffectInfos->Get(i);
            switch (skillEffectInfo->passiveSkillType)
            {
                case 2: // Element
                    var element = skillEffectInfo->passiveDetailType - 2;
                    elementalPotencies[element].Item1 += skillEffectInfo->effectValue;
                    elementalPotencies[element].Item2 += skillEffectInfo->effectCoefficient;
                    break;
                case 22: // Phys damage
                    physAdd += skillEffectInfo->effectValue;
                    physCoefficient += skillEffectInfo->effectCoefficient;
                    break;
                case 23: // Mag damage
                    magAdd += skillEffectInfo->effectValue;
                    magCoefficient += skillEffectInfo->effectCoefficient;
                    break;
                case 1: // Parameter
                case 10: // LB Damage
                case 11: // Summon Damage
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

            var typeStr = i switch
            {
                0 => "Fire",
                1 => "Ice",
                2 => "Lightning",
                3 => "Earth",
                4 => "Water",
                5 => "Wind",
                6 => "Holy",
                7 => "Dark",
                _ => "?"
            };

            var color = i switch
            {
                0 => new Vector4(1, 0.4f, 0.4f, 1),
                1 => new Vector4(0.4f, 0.55f, 1, 1),
                2 => new Vector4(0.9f, 1, 0.15f, 1),
                3 => new Vector4(1, 0.6f, 0.25f, 1),
                4 => new Vector4(0.4f, 1, 1, 1),
                5 => new Vector4(0.4f, 0.9f, 0.45f, 1),
                6 => new Vector4(1, 1, 0.67f, 1),
                7 => new Vector4(0.9f, 0.4f, 0.9f, 1),
                _ => Vector4.One
            };

            ImGui.TextColored(color, $"{typeStr}: {CalcDamage(baseDamage, 1000, a + add, c + coefficient, 0)}");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted($"Phys. Res.: {CalcOwnDamageReduction(characterInfo->totalStatus->physicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->physicalDefence, 0)} HP)");
        ImGui.TextUnformatted($"Mag. Res.: {CalcOwnDamageReduction(characterInfo->totalStatus->magicalDefence)}% ({CalcHP(characterInfo->totalStatus->hp, characterInfo->totalStatus->magicalDefence, 0)} HP)");

        ImGui.End();
    }

    private static int CalcAllyBaseDamage(int attack, int defense) => (int)(attack * 50 / (defense * 2.2f + 100));

    private static int CalcOwnDamageReduction(int defense) => 100 - 2000000 / (defense * 100 + 10000);

    private static int CalcEnemyBaseDamage(int attack, int defense) => attack * 2000 / (defense * 100 + 10000);

    private static int CalcDamage(int baseDamage, int skillCoefficient, int potencyAdd, int potencyCoefficient, int stanceBonusCoefficient)
    {
        var skill = (skillCoefficient + potencyAdd) * (1000 + potencyCoefficient) / 1000;
        return baseDamage * skill / 1000 * (1000 + stanceBonusCoefficient) / 1000;
    }

    private static int CalcHP(int hp, int defense, int stanceReductionCoefficient) => (int)(hp * (1 + (defense - 100) * 0.005f) / ((1000 - stanceReductionCoefficient) / 1000f));
}