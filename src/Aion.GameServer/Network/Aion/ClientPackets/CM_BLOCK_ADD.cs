using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BLOCK_ADD (Ben). Adds a player to the block list (self/full/not-found/buddy/already checks). equalsIgnoreCase->StringComparison.OrdinalIgnoreCase; converges SM_BLOCK_RESPONSE. SocialService/PlayerService/Util/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_BLOCK_ADD : AionClientPacket
{
    private string targetName;
    private string reason;

    public CM_BLOCK_ADD(int opcode, ISet<State> validStates)
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
        Player player = GetConnection().GetActivePlayer();
        PlayerCommonData target = PlayerService.GetOrLoadPlayerCommonData(Util.ConvertName(targetName));

        if (player.GetName().Equals(targetName, StringComparison.OrdinalIgnoreCase))
            SendPacket(new SM_BLOCK_RESPONSE(SM_BLOCK_RESPONSE.CANT_BLOCK_SELF, targetName));
        else if (player.GetBlockList().IsFull())
            SendPacket(new SM_BLOCK_RESPONSE(SM_BLOCK_RESPONSE.LIST_FULL, targetName));
        else if (target == null)
            SendPacket(new SM_BLOCK_RESPONSE(SM_BLOCK_RESPONSE.TARGET_NOT_FOUND, targetName));
        else if (player.GetFriendList().GetFriend(target.GetPlayerObjId()) != null)
            SendPacket(SM_SYSTEM_MESSAGE.STR_BLOCKLIST_NO_BUDDY());
        else if (player.GetBlockList().Contains(target.GetPlayerObjId()))
            SendPacket(SM_SYSTEM_MESSAGE.STR_BLOCKLIST_ALREADY_BLOCKED());
        else
            SocialService.AddBlockedUser(player, target, reason);
    }
}
