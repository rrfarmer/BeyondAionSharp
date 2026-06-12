using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GODSTONE_SOCKET (ATracer). Sockets a godstone into an equipped weapon at a target NPC in talk range. ItemSocketService red-tolerated.</summary>
public class CM_GODSTONE_SOCKET : AionClientPacket
{
    private int npcObjectId;
    private int weaponId;
    private int stoneId;

    public CM_GODSTONE_SOCKET(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        npcObjectId = ReadD();
        weaponId = ReadD();
        stoneId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        VisibleObject npc = player.GetTarget();
        if (npc is Npc && npc.GetObjectId() == npcObjectId && PositionUtil.IsInTalkRange(player, (Npc)npc))
            ItemSocketService.SocketGodstone(player, player.GetEquipment().GetEquippedItemByObjId(weaponId), stoneId);
    }
}
