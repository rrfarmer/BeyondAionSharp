using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EXCHANGE_REQUEST (-Avol-). Requests a trade with a target player (range/hide/race/deny guards) and raises a question window. Anonymous RequestResponseHandler&lt;Player&gt; -> nested ExchangeResponseHandler. ExchangeService/World/SM_* red-tolerated.</summary>
public class CM_EXCHANGE_REQUEST : AionClientPacket
{
    public int? targetObjectId;

    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_EXCHANGE_REQUEST));

    public CM_EXCHANGE_REQUEST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        Player targetPlayer = World.GetInstance().GetPlayer(targetObjectId.Value);

        if (targetPlayer == null || activePlayer.Equals(targetPlayer))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_EXCHANGE_NO_ONE_TO_EXCHANGE());
            return;
        }

        if (activePlayer.IsDead() || targetPlayer.IsDead())
        {
            log.LogWarning("CM_EXCHANGE_REQUEST dead players target from {ActiveOid} to {TargetOid}", activePlayer.GetObjectId(), targetObjectId);
            return;
        }

        if (!PositionUtil.IsInRange(activePlayer, targetPlayer, 5))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_EXCHANGE_TOO_FAR_TO_EXCHANGE());
            return;
        }

        if (activePlayer.IsInAnyHide())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_WHILE_INVISIBLE());
            return;
        }

        if (targetPlayer.IsInAnyHide())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_WITH_INVISIBLE_USER());
            return;
        }

        if (!activePlayer.GetRace().Equals(targetPlayer.GetRace()))
        {
            log.LogInformation("[AUDIT] Player " + activePlayer.GetName() + " tried trade with player (" + targetPlayer.GetName() + ") another race.");
            return;
        }

        if (targetPlayer.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.TRADE))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_TRADE(targetPlayer.GetName()));
            return;
        }

        RequestResponseHandler<Player> responseHandler = new ExchangeResponseHandler(activePlayer);

        bool requested = targetPlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_EXCHANGE_DO_YOU_ACCEPT_EXCHANGE, responseHandler);
        if (requested)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_EXCHANGE_ASKED_EXCHANGE_TO_HIM(targetPlayer.GetName()));
            PacketSendUtility.SendPacket(targetPlayer,
                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_EXCHANGE_DO_YOU_ACCEPT_EXCHANGE, 0, 0, activePlayer.GetName()));
        }
        else
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_ASK_WHEN_HE_IS_ASKED_QUESTION(targetPlayer.GetName()));
        }
    }

    private sealed class ExchangeResponseHandler : RequestResponseHandler<Player>
    {
        public ExchangeResponseHandler(Player requester) : base(requester)
        {
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            ExchangeService.GetInstance().RegisterExchange(requester, responder);
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_EXCHANGE_HE_REJECTED_EXCHANGE(responder.GetName()));
        }
    }
}
