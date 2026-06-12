using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TIME_CHECK (-Nemesiss-). Likely a ping/pong time-sync; echoes nanoTime via SM_TIME_CHECK after SM_AFTER_TIME_CHECK_4_7_5. SM_* red-tolerated.</summary>
public class CM_TIME_CHECK : AionClientPacket
{
    /// <summary>Nano time / 1000000</summary>
    private int nanoTime;

    public CM_TIME_CHECK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        nanoTime = ReadD();
    }

    protected override void RunImpl()
    {
        // int timeNow = (int) (System.nanoTime() / 1000000);
        // int diff = timeNow - nanoTime;
        // System.out.println("CM_TIME_CHECK: " + nanoTime + " =?= " + timeNow + " dif: " + diff);
        AionConnection client = GetConnection();
        client.SendPacket(new SM_AFTER_TIME_CHECK_4_7_5()); // don't know what is this doing it is send after this on retail
        client.SendPacket(new SM_TIME_CHECK(nanoTime));
    }
}
