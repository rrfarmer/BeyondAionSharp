using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_SEND_EMBLEM_INFO (cura). Sends legion emblem info (without the following EMBLEM_DATA packets). LegionService/SM_LEGION_SEND_EMBLEM red-tolerated.</summary>
public class CM_LEGION_SEND_EMBLEM_INFO : AionClientPacket
{
    private int legionId;

    public CM_LEGION_SEND_EMBLEM_INFO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        legionId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        if (activePlayer == null)
            return;

        Legion legion = LegionService.GetInstance().GetLegion(legionId);
        if (legion != null)
            SendPacket(new SM_LEGION_SEND_EMBLEM(legionId, legion.GetLegionEmblem(), 0, legion.GetName())); // send only info without following EMBLEM_DATA packets
    }
}
