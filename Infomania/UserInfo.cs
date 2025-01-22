using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ECGen.Generated;
using ECGen.Generated.Command.DB.UserData;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame.Profile;
using ECGen.Generated.Command.UI;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Game;
using WorkManager = Materia.Game.WorkManager;

namespace Infomania;

public unsafe class UserInfo : ModalInfo
{
    public override bool Enabled => Infomania.Config.EnableUserInfo;
    public override Type[] ValidModals { get; } = [ typeof(ProfileModalPresenter) ];

    private enum ProfileDetailType
    {
        //[Display(Name = "")]
        Items,
        Materia,
        HighwindBattle
    }

    private ProfileDetailType selectedDetailType;
    private static MemoryDatabase* UserData => DataStore.NativePtr->userData->dB;

    private static string GetEnumName<T>(T v) where T : struct, Enum
    {
        var name = Enum.GetName(v) ?? string.Empty;
        return typeof(T).GetField(name)?.GetCustomAttribute<DisplayAttribute>()?.Name ?? name;
    }

    public override void Draw(Modal modal)
    {
        if (!Il2CppType<ProfileModalPresenter>.Is(modal.NativePtr, out var profileModal) || profileModal->profileModel->friendType != FriendType.Myself) return;

        //Infomania.BeginInfoWindow("UserInfo");
        ImGui.SetNextWindowSizeConstraints(new Vector2(600, 300) * ImGuiEx.Scale, new Vector2(10000));
        ImGui.Begin("UserInfo");
        ImGui.BeginChild("ProfileDetailTypeSelection", new Vector2(120 * ImGuiEx.Scale, 0), true);
        foreach (var type in Enum.GetValues<ProfileDetailType>())
        {
            if (ImGui.Selectable(GetEnumName(type), type == selectedDetailType))
                selectedDetailType = type;
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("ProfileDetail", Vector2.Zero, true);
        switch (selectedDetailType)
        {
            case ProfileDetailType.Items:
                DrawItemDetails();
                break;
            case ProfileDetailType.Materia:
                DrawMateriaDetails();
                break;
            case ProfileDetailType.HighwindBattle:
                DrawHighwindBattleDetails();
                break;
        }
        ImGui.EndChild();

        ImGui.End();
    }

    private static void DrawItemDetails()
    {
        if (!ImGui.BeginTable("ItemTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("Owned", ImGuiTableColumnFlags.None, 0.2f);
        ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.None, 0.2f);
        ImGui.TableHeadersRow();

        foreach (var p in UserData->userItemTable->dictionary->Enumerable.OrderBy(p => (long)p.ptr->key))
        {
            var itemStore = WorkManager.GetItemStore(p.ptr->value->itemId);
            var userItem = itemStore->userItem;
            var name = GameInterop.GetLocalizedText(LocalizeTextCategory.Item, itemStore->masterItem->nameLanguageId);
            if (string.IsNullOrEmpty(name)) continue;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(userItem->count.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(userItem->totalObtainCount.ToString());
        }

        ImGui.EndTable();
    }

    private static void DrawMateriaDetails()
    {
        foreach (var p in UserData->userMateriaCollectionTable->dictionary->Enumerable.OrderBy(p => (long)p.ptr->key))
        {
            ImGui.TextUnformatted($"{p.ptr->value->materiaEvolveId}");
            //var materiaEvolveStore = WorkManager.GetMateriaEvolveStore(p.ptr->value->materiaEvolveId);
            //ImGui.TextUnformatted($"{GameInterop.GetLocalizedText(LocalizeTextCategory.SkillBase, materiaEvolveStore->masterSkillBase->nameLanguageId)} {p.ptr->value->enhanceCount} {p.ptr->value->qualityFiveCraftCount}");
        }

        foreach (var p in UserData->userMateriaRecipeTable->dictionary->Enumerable.OrderBy(p => (long)p.ptr->key))
        {
            var userMateriaRecipe = p.ptr->value;
            var materiaRecipeStore = WorkManager.GetMateriaRecipeStore(userMateriaRecipe->materiaRecipeId);
            ImGui.TextUnformatted($"{GameInterop.GetLocalizedText(LocalizeTextCategory.MateriaRecipe, materiaRecipeStore->masterMateriaRecipe->titleLanguageId)} {materiaRecipeStore->masterMateriaRecipe->id} {userMateriaRecipe->materiaRecipeId} {userMateriaRecipe->craftCount}");
        }
    }

    private static void DrawHighwindBattleDetails()
    {
        var battles = UserData->userHighwindBattleTable->dictionary->Enumerable
            .Select(p => (long)p.ptr->key)
            .Order()
            .Select(id => new Ptr<HighwindWork.HighwindBattleStore>(WorkManager.GetHighwindBattleStore(id)))
            .ToArray();

        var rareBattles = battles.Where(p => p.ptr->highwindBattleType == HighwindBattleType.Rare).ToArray();

        ImGui.TextUnformatted($"Normal: {rareBattles.Where(p => p.ptr->masterHighwindBattle->highwindBattleReleaseDirectionType == 0).Sum(p => p.ptr->winCount)}");
        ImGui.SameLine();
        ImGui.TextUnformatted($"Gold: {rareBattles.Where(p => p.ptr->masterHighwindBattle->highwindBattleReleaseDirectionType == 1).Sum(p => p.ptr->winCount)}");
        ImGui.SameLine();
        ImGui.TextUnformatted($"Bomb: {rareBattles.Where(p => p.ptr->masterHighwindBattle->highwindBattleReleaseDirectionType == 2).Sum(p => p.ptr->winCount)}");

        if (!ImGui.BeginTable("ItemTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.None);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None);
        ImGui.TableSetupColumn("Wins", ImGuiTableColumnFlags.None);
        ImGui.TableHeadersRow();

        foreach (var p in battles)
        {
            var highwindBattleStore = p.ptr;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(highwindBattleStore->masterHighwindBattle->id.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(highwindBattleStore->highwindBattleType != HighwindBattleType.Rare
                ? highwindBattleStore->highwindBattleType.ToString()
                : ((HighwindBattleReleaseDirectionType)highwindBattleStore->masterHighwindBattle->highwindBattleReleaseDirectionType).ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(highwindBattleStore->winCount.ToString());
        }

        ImGui.EndTable();
    }
}