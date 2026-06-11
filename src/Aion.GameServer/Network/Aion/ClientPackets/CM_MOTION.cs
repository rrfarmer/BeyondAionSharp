using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MOTION (MrPoke). Sets a motion active/inactive. Motions red-tolerated.</summary>
public class CM_MOTION : AionClientPacket
{
    private int motionId;
    private int motionType;

    public CM_MOTION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadC(); // unk 4
        motionId = ReadUH();
        motionType = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        player.GetMotions().SetActive(motionId, motionType);
    }
}
