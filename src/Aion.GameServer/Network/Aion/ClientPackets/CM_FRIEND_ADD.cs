using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_FRIEND_ADD (Ben, Neon). Received when a user tries to add a friend; validates and raises a buddy-request question window. Anonymous RequestResponseHandler&lt;Player&gt; -> nested FriendAddResponseHandler. SocialService/SM_FRIEND_RESPONSE/World red-tolerated.</summary>
public class CM_FRIEND_ADD : AionClientPacket
{
    private string targetName;
    private string message;

    public CM_FRIEND_ADD(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetName = ReadS();
        message = ReadS();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        Player targetPlayer = World.GetInstance().GetPlayer(Util.ConvertName(targetName));

        if (targetPlayer == null || !targetPlayer.IsOnline())
        {
            SendPacket(SM_FRIEND_RESPONSE.TARGET_OFFLINE);
        }
        else if (activePlayer.Equals(targetPlayer))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_BUDDYLIST_BUSY());
        }
        else if (CustomConfig.FRIENDLIST_GM_RESTRICT && ((targetPlayer.IsStaff() && !activePlayer.IsStaff()) || (activePlayer.IsStaff() && !targetPlayer.IsStaff())))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_BUDDY_CANT_ADD_WHEN_HE_IS_ASKED_QUESTION(targetPlayer.GetName(true)));
        }
        else if (activePlayer.GetFriendList().GetFriend(targetPlayer.GetObjectId()) != null)
        {
            SendPacket(SM_FRIEND_RESPONSE.TARGET_ALREADY_FRIEND);
        }
        else if (activePlayer.GetRace() != targetPlayer.GetRace())
        {
            SendPacket(SM_FRIEND_RESPONSE.TARGET_NOT_FOUND);
        }
        else if (activePlayer.GetBlockList().Contains(targetPlayer.GetObjectId()))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NO_BLOCKED_CHARACTER());
        }
        else if (targetPlayer.GetBlockList().Contains(activePlayer.GetObjectId()))
        {
            SendPacket(SM_FRIEND_RESPONSE.TARGET_BLOCKED_YOU);
        }
        else if (activePlayer.GetFriendList().IsFull())
        {
            SendPacket(SM_FRIEND_RESPONSE.LIST_FULL);
        }
        else if (targetPlayer.GetFriendList().IsFull())
        {
            SendPacket(SM_FRIEND_RESPONSE.TARGET_LIST_FULL(targetPlayer.GetName()));
        }
        else if (targetPlayer.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.FRIEND))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_FRIEND(targetPlayer.GetName()));
        }
        else
        {
            RequestResponseHandler<Player> responseHandler = new FriendAddResponseHandler(activePlayer, this);

            bool requested = targetPlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_BUDDYLIST_ADD_BUDDY_REQUEST, responseHandler);
            // If the player is busy and could not be asked
            if (!requested)
            {
                SendPacket(SM_SYSTEM_MESSAGE.STR_BUDDYLIST_BUSY());
                return;
            }

            // Send question packet to buddy
            PacketSendUtility.SendPacket(targetPlayer,
                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_BUDDYLIST_ADD_BUDDY_REQUEST, activePlayer.GetObjectId(), 0, activePlayer.GetName(), message));
        }
    }

    private sealed class FriendAddResponseHandler : RequestResponseHandler<Player>
    {
        private readonly CM_FRIEND_ADD packet;

        public FriendAddResponseHandler(Player requester, CM_FRIEND_ADD packet) : base(requester)
        {
            this.packet = packet;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            if (requester.GetFriendList().IsFull())
                PacketSendUtility.SendPacket(responder, SM_FRIEND_RESPONSE.REQUESTER_LIST_FULL_CANT_ACCEPT(requester.GetName()));
            else if (!responder.GetFriendList().IsFull())
                SocialService.MakeFriends(requester, responder);
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            packet.SendPacket(SM_FRIEND_RESPONSE.TARGET_DENIED(responder.GetName()));
        }
    }
}
