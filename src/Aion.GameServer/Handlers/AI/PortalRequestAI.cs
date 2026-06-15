using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/PortalRequestAI (xTz).</summary>
[AIName("portal_request")]
public class PortalRequestAI : PortalAI
{
    public PortalRequestAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        TeleportLocation firstLoc = teleportTemplate.GetTeleLocIdData().GetTelelocations()[0];
        TelelocationTemplate locationTemplate = DataManager.TELELOCATION_DATA.GetTelelocationTemplate(firstLoc.GetLocId());
        RequestResponseHandler<Npc> portal = new PortalRequestResponseHandler(GetOwner(), firstLoc);
        long transportationPrice = PricesService.GetPriceForService(firstLoc.GetPrice(), player.GetRace());
        if (player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_TELEPORT_NEED_CONFIRM, portal))
        {
            PacketSendUtility.SendPacket(player,
                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_TELEPORT_NEED_CONFIRM, GetObjectId(), GetObjectTemplate().GetTalkDistance(), ((IL10n)locationTemplate).GetL10n(), transportationPrice));
        }
    }

    private sealed class PortalRequestResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly TeleportLocation firstLoc;

        public PortalRequestResponseHandler(Npc requester, TeleportLocation firstLoc)
            : base(requester)
        {
            this.firstLoc = firstLoc;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            TeleportService.Teleport(responder, firstLoc, TeleportAnimation.JUMP_IN);
        }
    }
}
