using System;
using System.Collections.Generic;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ECGen.Generated.Command.OutGame.Party;
using ImGuiNET;
using Materia.Game;

namespace Infomania;

public unsafe class PartySelectInfo : ScreenInfo
{
    public override bool Enabled => Infomania.Config.EnablePartySelectInfo;

    public override Type[] ValidScreens { get; } =
    [
        typeof(PartySelectScreenPresenter),
        typeof(SoloPartySelectScreenPresenter),
        typeof(StoryPartySelectScreenPresenter),
        typeof(MultiPartySelectScreenPresenter),
        typeof(MultiAreaBattlePartySelectPresenter),
        typeof(GuildMainBattlePartySelectScreenPresenter),
        typeof(DungeonPartySelectPresenter)
    ];

    public override void Draw(Screen screen)
    {
        var partySelect = (PartySelectScreenPresenterBase<PartySelectScreenSetupParameter>*)screen.NativePtr;
        var selectedParty = partySelect->partySelect->selectPartyInfo;
        if (selectedParty == null) return;

        switch (selectedParty->partyCharacterInfos->size)
        {
            case 1:
            {
                var character = selectedParty->partyCharacterInfos->GetPtr(0);
                if (character->characterId == 0) return;
                Infomania.BeginInfoWindow("PartySelectInfo");
                PartyEditInfo.DrawStats(new CharacterCalculator(character));
                ImGui.End();
                break;
            }
            case 3:
            {
                var characterInfos = new List<nint>(3);

                var leftCharacter = selectedParty->partyCharacterInfos->GetPtr(1);
                if (leftCharacter->characterId != 0)
                    characterInfos.Add((nint)leftCharacter);

                var middleCharacter = selectedParty->partyCharacterInfos->GetPtr(0);
                if (middleCharacter->characterId != 0)
                    characterInfos.Add((nint)middleCharacter);

                var rightCharacter = selectedParty->partyCharacterInfos->GetPtr(2);
                if (rightCharacter->characterId != 0)
                    characterInfos.Add((nint)rightCharacter);

                if (characterInfos.Count == 0) return;

                Infomania.BeginInfoWindow("PartySelectInfo");
                PartyEditInfo.DrawStats(characterInfos);
                ImGui.End();
                break;
            }
        }
    }
}