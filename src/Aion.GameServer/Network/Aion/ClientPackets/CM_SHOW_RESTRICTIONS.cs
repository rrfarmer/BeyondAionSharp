using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SHOW_RESTRICTIONS (Neon). Sent on /restriction; replies with the accuse-info message (level scales with reports). SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_SHOW_RESTRICTIONS : AionClientPacket
{
    public CM_SHOW_RESTRICTIONS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_INFO_NORMAL()); // can be STR_MSG_ACCUSE_INFO_1_LEVEL to STR_MSG_ACCUSE_INFO_4_LEVEL
    }
}
