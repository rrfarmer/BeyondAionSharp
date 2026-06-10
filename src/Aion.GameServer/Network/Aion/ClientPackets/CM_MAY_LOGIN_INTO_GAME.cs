using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MAY_LOGIN_INTO_GAME (-Nemesiss-). Client asks whether it may log into the game; replies SM_MAY_LOGIN_INTO_GAME. red-tolerated.</summary>
public class CM_MAY_LOGIN_INTO_GAME : AionClientPacket
{
    public CM_MAY_LOGIN_INTO_GAME(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // empty
    }

    protected override void RunImpl()
    {
        AionConnection client = GetConnection();
        // TODO! check if may login into game [play time etc]
        client.SendPacket(new SM_MAY_LOGIN_INTO_GAME());
    }
}
