using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BLOCK_DEL (Ben). Removes a player from the block list. BlockedPlayer/SocialService/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_BLOCK_DEL : AionClientPacket
{
    private string targetName;

    public CM_BLOCK_DEL(int opcode, ISet<State> validStates)
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
        BlockedPlayer target = activePlayer.GetBlockList().GetBlockedPlayer(targetName);
        if (target == null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NOT_IN_LIST());
        }
        else
        {
            SocialService.DeleteBlockedUser(activePlayer, target);
        }
    }
}
