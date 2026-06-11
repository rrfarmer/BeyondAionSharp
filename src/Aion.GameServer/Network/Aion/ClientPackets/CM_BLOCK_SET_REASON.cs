using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BLOCK_SET_REASON (Ben). Sets the block reason for a blocked player. BlockedPlayer/SocialService/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_BLOCK_SET_REASON : AionClientPacket
{
    private string targetName;
    private string reason;

    public CM_BLOCK_SET_REASON(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetName = ReadS();
        reason = ReadS();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        BlockedPlayer target = activePlayer.GetBlockList().GetBlockedPlayer(targetName);

        if (target == null)
            SendPacket(SM_SYSTEM_MESSAGE.STR_BLOCKLIST_NOT_IN_LIST());
        else
        {
            SocialService.SetBlockedReason(activePlayer, target, reason);
        }
    }
}
