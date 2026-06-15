using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/PortalAI (xTz).</summary>
[AIName("portal")]
public class PortalAI : ActionItemNpcAI
{
    protected TeleporterTemplate teleportTemplate;

    public PortalAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        return true;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        teleportTemplate = DataManager.TELEPORTER_DATA.GetTeleporterTemplateByNpcId(GetNpcId());
    }

    protected override void HandleDialogStart(Player player)
    {
        QuestEngine.QuestEngine.GetInstance().OnDialog(new QuestEnv(GetOwner(), player, 0, DialogAction.USE_OBJECT));
        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PortalPath portalPath = DataManager.PORTAL2_DATA.GetPortalUsePath(GetNpcId(), player);
        if (portalPath != null)
        {
            PortalService.Port(portalPath, player, GetOwner());
        }
        else if (teleportTemplate != null)
        {
            TeleportService.TeleportToFirstTeleportLocation(player, GetOwner(), TeleportAnimation.FADE_OUT_BEAM);
        }
        else
        {
            base.HandleUseItemFinish(player);
        }
    }
}
