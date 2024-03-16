using System;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Boss;
using Materia;
using Materia.Attributes;
using Materia.Game;

namespace Infomania;

[Injection]
public unsafe class BossSelectDetailInfo : ModalInfo
{
    private static BossDataSelectModel* cachedBossModel;

    public override bool Enabled => Infomania.Config.EnableBossDetailInfo;
    public override Type[] ValidModals { get; } = [ typeof(BossSelectDetailModalPresenter) ];
    public override void Draw(Modal modal) => BossDetailInfo.DrawStats(cachedBossModel);

    private delegate void SetupContentDelegate(BossDetailDescriptionContent* bossDetailDescriptionContent, BossDataSelectModel* model, nint method);
    [GameSymbol("Command.OutGame.Boss.BossDetailDescriptionContent$$SetupContent")]
    private static IMateriaHook<SetupContentDelegate>? SetupContentHook;
    private static void SetupContentDetour(BossDetailDescriptionContent* bossDetailDescriptionContent, BossDataSelectModel* model, nint method)
    {
        cachedBossModel = model;
        SetupContentHook!.Original(bossDetailDescriptionContent, model, method);
    }
}