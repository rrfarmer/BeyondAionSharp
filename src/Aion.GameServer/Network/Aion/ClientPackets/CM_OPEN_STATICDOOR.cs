using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_OPEN_STATICDOOR (rhys2002 &amp; Wakizashi). Opens a static door by id. StaticDoorService red-tolerated.</summary>
public class CM_OPEN_STATICDOOR : AionClientPacket
{
    private int doorId;

    public CM_OPEN_STATICDOOR(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        doorId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = this.GetConnection().GetActivePlayer();
        StaticDoorService.GetInstance().OpenStaticDoor(player, doorId);
    }
}
