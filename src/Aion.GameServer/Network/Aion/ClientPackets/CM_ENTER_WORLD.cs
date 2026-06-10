using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Player;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ENTER_WORLD (-Nemesiss-, Avol, Neon). Client requests the given character (by oid) to enter the game world. PlayerEnterWorldService red-tolerated.</summary>
public class CM_ENTER_WORLD : AionClientPacket
{
    /// <summary>Object Id of player that is entering world</summary>
    private int objectId;

    public CM_ENTER_WORLD(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
    }

    protected override void RunImpl()
    {
        PlayerEnterWorldService.EnterWorld(GetConnection(), objectId);
    }
}
