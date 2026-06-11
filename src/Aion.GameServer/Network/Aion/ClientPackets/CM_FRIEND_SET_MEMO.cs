using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_FRIEND_SET_MEMO (ginho1). Sets a memo note on a friend. SocialService/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_FRIEND_SET_MEMO : AionClientPacket
{
    private string targetName;
    private string memo;

    public CM_FRIEND_SET_MEMO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetName = ReadS();
        memo = ReadS();
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
            SocialService.SetFriendMemo(activePlayer, friend, memo);
        }
    }
}
