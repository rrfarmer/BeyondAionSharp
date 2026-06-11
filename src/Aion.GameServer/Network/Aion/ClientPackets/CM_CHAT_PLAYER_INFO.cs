using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHAT_PLAYER_INFO (prix, Neon). Requests chat-window info for a named player. World/SM_CHAT_WINDOW/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_CHAT_PLAYER_INFO : AionClientPacket
{
    private string playerName;

    public CM_CHAT_PLAYER_INFO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        playerName = ReadS();
    }

    protected override void RunImpl()
    {
        Player target = World.GetInstance().GetPlayer(ChatUtil.GetRealCharName(playerName));
        if (target == null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }
        if (!GetConnection().GetActivePlayer().GetKnownList().Knows(target))
            SendPacket(new SM_CHAT_WINDOW(target, false));
    }
}
