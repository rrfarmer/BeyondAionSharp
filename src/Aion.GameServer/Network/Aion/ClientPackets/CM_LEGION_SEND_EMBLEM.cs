using System.Collections.Generic;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_SEND_EMBLEM (Simple, cura, Neon). Sends the full emblem data for a legion. LegionService red-tolerated.</summary>
public class CM_LEGION_SEND_EMBLEM : AionClientPacket
{
    private int legionId;

    public CM_LEGION_SEND_EMBLEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        legionId = ReadD();
    }

    protected override void RunImpl()
    {
        Legion legion = LegionService.GetInstance().GetLegion(legionId);
        if (legion != null)
            LegionService.GetInstance().SendEmblemData(GetConnection().GetActivePlayer(), legion.GetLegionEmblem(), legionId, legion.GetName());
    }
}
