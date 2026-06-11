using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_FRIEND_DEL (Ben). Removes a friend by name. SocialService/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_FRIEND_DEL : AionClientPacket
{
    private string targetName;

    public CM_FRIEND_DEL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetName = ReadS();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        Friend friend = activePlayer.GetFriendList().GetFriend(targetName);
        if (friend == null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NOT_IN_LIST());
        }
        else
        {
            SocialService.DeleteFriend(activePlayer, friend);
        }
    }
}
