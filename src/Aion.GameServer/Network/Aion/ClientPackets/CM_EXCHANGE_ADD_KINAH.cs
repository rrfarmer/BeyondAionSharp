using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EXCHANGE_ADD_KINAH (Avol). Adds kinah to the active trade. ExchangeService red-tolerated.</summary>
public class CM_EXCHANGE_ADD_KINAH : AionClientPacket
{
    private long kinahCount;

    public CM_EXCHANGE_ADD_KINAH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        kinahCount = ReadQ();
    }

    protected override void RunImpl()
    {
        ExchangeService.GetInstance().AddKinah(GetConnection().GetActivePlayer(), kinahCount);
    }
}
