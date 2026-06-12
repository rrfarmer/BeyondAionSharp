using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.ChatServer;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHAT_AUTH (Luno). Client sends this only once; triggers the chat-server login request. ChatServer red-tolerated.</summary>
public class CM_CHAT_AUTH : AionClientPacket
{
    public CM_CHAT_AUTH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        int objectId = ReadD(); // lol NC
        byte[] macAddress = ReadB(6);
    }

    protected override void RunImpl()
    {
        ChatServer.GetInstance().SendPlayerLoginRequest(GetConnection().GetActivePlayer());
    }
}
