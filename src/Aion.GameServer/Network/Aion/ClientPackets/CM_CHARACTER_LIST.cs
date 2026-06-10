using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHARACTER_LIST (-Nemesiss-). Client requests the character list; replies with account properties + the character list. SM_ACCOUNT_PROPERTIES/SM_CHARACTER_LIST red-tolerated.</summary>
public class CM_CHARACTER_LIST : AionClientPacket
{
    /// <summary>PlayOk2 - we dont care...</summary>
    private int playOk2;

    public CM_CHARACTER_LIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        playOk2 = ReadD();
    }

    protected override void RunImpl()
    {
        SendPacket(new SM_ACCOUNT_PROPERTIES());
        SendPacket(new SM_CHARACTER_LIST(playOk2));
    }
}
