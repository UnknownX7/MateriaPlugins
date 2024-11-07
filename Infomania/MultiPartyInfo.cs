using System;
using System.Collections.Generic;
using System.Linq;
using ECGen.Generated;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ImGuiNET;
using Materia.Game;

namespace Infomania;

public unsafe class MultiPartyInfo : ScreenInfo
{
    public override bool Enabled => Infomania.Config.EnablePartySelectInfo;
    public override Type[] ValidScreens { get; } = [ typeof(MultiAreaBattleMatchingRoomScreenPresenter) ];

    public override void Draw(Screen screen)
    {
        if (!Il2CppType<MultiAreaBattleMatchingRoomScreenPresenter>.Is(screen.NativePtr, out var matchingRoom)
            || matchingRoom->prevRoomMembers == null
            || matchingRoom->battleUserUiIndexDictionary == null)
            return;

        const int maxUsers = 3;
        var users = stackalloc long[maxUsers];
        foreach (var p in matchingRoom->battleUserUiIndexDictionary->Enumerable)
        {
            var index = (int)p.ptr->value;
            if (index < maxUsers)
                users[index] = (long)p.ptr->key;
        }

        var characterInfos = new List<nint>();
        var roomMembers = (Unmanaged_Array<RoomMember>*)matchingRoom->prevRoomMembers;
        for (int i = 0; i < maxUsers; i++)
        {
            var id = users[i];
            if (id == 0) continue;
            var member = roomMembers->PtrEnumerable.FirstOrDefault(ptr => ptr.ptr->battleUserId == id).ptr;
            if (member == null || member->userName == null) continue;
            if (i == 1 && characterInfos.Count > 0) // Left-most character
                characterInfos.Insert(0, (nint)member->partyCharacterInfo);
            else
                characterInfos.Add((nint)member->partyCharacterInfo);
        }

        if (characterInfos.Count == 0) return;

        Infomania.BeginInfoWindow("MultiPartySelectInfo");
        PartyEditInfo.DrawStats(characterInfos, matchingRoom);
        ImGui.End();
    }
}