using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GF_WEBSHOP_TOKEN_REQUEST (Artur). Sent when the client is started with -st; replies with a (currently empty) webshop token. SM_GF_WEBSHOP_TOKEN_RESPONSE red-tolerated.</summary>
public class CM_GF_WEBSHOP_TOKEN_REQUEST : AionClientPacket
{
    public CM_GF_WEBSHOP_TOKEN_REQUEST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        SendPacket(new SM_GF_WEBSHOP_TOKEN_RESPONSE("")); // TODO
    }
}
