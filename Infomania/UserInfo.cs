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
        HighwindBattle,
        Gacha
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
            case ProfileDetailType.Gacha:
                DrawGachaDetails();
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

    private static long totalDraws = 0L;
    private static long totalFreeDraws = 0L;
    private static long totalTicketDraws = 0L;
    private static long totalCrystals = 0L;
    private static long totalStamps = 0L;
    private static void DrawGachaDetails()
    {
        ImGui.TextUnformatted($"Total Draws: {totalDraws} Crystal, {totalFreeDraws} Free, {totalTicketDraws} Tickets");
        ImGui.TextUnformatted($"Total Crystals Used: {totalCrystals} ({totalStamps} Stamps)");

        if (!ImGui.BeginTable("GachaTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        totalDraws = 0L;
        totalFreeDraws = 0L;
        totalTicketDraws = 0L;
        totalCrystals = 0L;
        totalStamps = 0L;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Draws", ImGuiTableColumnFlags.None, 0.14f);
        ImGui.TableSetupColumn("Crystals", ImGuiTableColumnFlags.None, 0.2f);
        ImGui.TableSetupColumn("Stamps", ImGuiTableColumnFlags.None, 0.08f);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 1);
        ImGui.TableHeadersRow();

        foreach (var p in WorkManager.NativePtr->gacha->gachaStores->values->PtrEnumerable.OrderBy(p => p.ptr->masterGacha->id))
        {
            var gachaType = (GachaType)p.ptr->masterGacha->gachaType;
            var draws = 0L;
            var crystals = 0L;
            var stamps = Il2CppType<GachaWork.GachaStampSheetGroupStore>.Is(p.ptr->gachaStampSheetGroupInfo, out var stampSheetGroupStore) && stampSheetGroupStore->userGachaStampSheetGroup != null
                ? stampSheetGroupStore->userGachaStampSheetGroup->totalStampCount
                : 0;

            // Actually IGachaStepGroupInfo[]
            foreach (var p2 in ((Unmanaged_Array<GachaWork.GachaStepGroupStore>*)p.ptr->gachaStepGroupInfos)->PtrEnumerable)
            {
                var userGachaStepGroup = p2.ptr->userGachaStepGroup;
                if (userGachaStepGroup == null) continue;

                var totalDrawCount = userGachaStepGroup->totalDrawCount;
                if (totalDrawCount <= 0) continue;

                if (gachaType == GachaType.Ticket)
                {
                    totalTicketDraws += totalDrawCount;
                    continue;
                }

                draws += totalDrawCount;

                // Actually IGachaStepInfo[]
                var steps = ((Unmanaged_Array<GachaWork.GachaStepStore>*)p2.ptr->gachaStepInfos)->PtrEnumerable.OrderBy(p3 => p3.ptr->masterGachaStep->seq).ToArray();
                if (steps.Length == 0) continue;

                var i = 0;
                while (totalDrawCount > 0)
                {
                    var stepStore = steps[i].ptr;
                    i = (int)stepStore->masterGachaStep->nextSeq - 1;
                    var drawCount = stepStore->masterGachaStep->drawCount;
                    if (drawCount == 0) break;

                    totalDrawCount -= drawCount;
                    var consumptionType = (GachaConsumptionType)stepStore->masterGachaStep->gachaConsumptionType;
                    switch (consumptionType)
                    {
                        case GachaConsumptionType.PaidStone:
                        case GachaConsumptionType.Stone:
                        case GachaConsumptionType.PaidStoneAndTicket:
                        case GachaConsumptionType.StoneAndTicket:
                            crystals += stepStore->masterGachaStep->consumptionCount;
                            break;
                    }
                }
            }

            if (draws == 0) continue;

            if (crystals == 0)
            {
                totalFreeDraws += draws;
                continue;
            }

            totalDraws += draws;
            totalCrystals += crystals;
            totalStamps += stamps;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(draws.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(crystals.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(stamps.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(GameInterop.GetLocalizedText(LocalizeTextCategory.Gacha, p.ptr->masterGacha->nameLanguageId));
        }

        ImGui.EndTable();
    }
}