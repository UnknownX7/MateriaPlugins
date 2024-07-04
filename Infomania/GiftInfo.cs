using System;
using System.Linq;
using ECGen.Generated.Command.Api;
using ECGen.Generated.Command.Enums;
using ECGen.Generated.Command.OutGame.Gift;
using ImGuiNET;
using Materia.Game;

namespace Infomania;

public unsafe class GiftInfo : ModalInfo
{
    public override bool Enabled => Infomania.Config.EnableGiftInfo;
    public override Type[] ValidModals { get; } = [ typeof(GiftModalPresenter) ];
    private ECGen.Generated.Ptr<GiftRewardInfo>[] gifts = [];

    private struct GiftRewardTypeInfo
    {
        public RewardType type;
        public long id;
        public long count;
        public long permanentCount;
        public long tempCount;
        public long firstExpiry;
        public long firstExpiryCount;
    }

    public override void Draw(Modal modal)
    {
        if (!Il2CppType<GiftModalPresenter>.Is(modal.NativePtr, out var giftModal)) return;

        gifts = giftModal->itemModel->giftListCache->Enumerable.SelectMany(p => p.ptr->value->PtrEnumerable).ToArray();
        /*var giftRewardTypeInfos = new Dictionary<(RewardType, long),GiftRewardTypeInfo>();
        foreach (var p in gifts)
        {
            var gift = p.ptr;
            var key = (gift->rewardType, gift->targetId);
            if (!giftRewardTypeInfos.TryGetValue(key, out var giftRewardTypeInfo))
                giftRewardTypeInfos.Add(key, giftRewardTypeInfo = new GiftRewardTypeInfo { type = gift->rewardType, id = gift->targetId });
        }*/

        if (gifts.Length == 0) return;

        var staminaInfo = GetGiftTypeRewardInfo(RewardType.Item, 17002);
        var drawTicketInfo = GetGiftTypeRewardInfo(RewardType.Item, 100001);

        Infomania.BeginInfoWindow("GiftInfo");
        DrawGiftRewardTypeInfo("Stamina Tonics", staminaInfo);
        DrawGiftRewardTypeInfo("Draw Tickets", drawTicketInfo);
        ImGui.End();
    }

    private GiftRewardTypeInfo GetGiftTypeRewardInfo(RewardType type, long id)
    {
        var filteredGifts = gifts.Where(p => p.ptr->rewardType == type && p.ptr->targetId == id).ToArray();
        var info = new GiftRewardTypeInfo
        {
            type = type,
            id = id,
            count = filteredGifts.Sum(p => p.ptr->count)
        };

        if (info.count <= 0) return info;

        info.permanentCount = filteredGifts.Where(p => p.ptr->giftInfo->expireDatetime == 7258118400000).Sum(p => p.ptr->count);
        info.tempCount = info.count - info.permanentCount;
        if (info.tempCount <= 0) return info;

        var firstExpiry = filteredGifts.MinBy(p => p.ptr->giftInfo->expireDatetime);
        info.firstExpiry = firstExpiry.ptr->giftInfo->expireDatetime;
        info.firstExpiryCount = firstExpiry.ptr->count;
        return info;
    }

    private static void DrawGiftRewardTypeInfo(string name, GiftRewardTypeInfo info)
    {
        if (info.count <= 0) return;
        ImGui.TextUnformatted($"{name}: {info.count} ({info.permanentCount} + {info.tempCount} Expiring)");
        if (info.tempCount > 0)
            ImGui.TextUnformatted($"  First Expiry: {DateTimeOffset.FromUnixTimeMilliseconds(info.firstExpiry).ToLocalTime():g} ({info.firstExpiryCount})");
    }
}