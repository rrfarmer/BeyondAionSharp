using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Drop;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GROUP_LOOT (Rhys2002). Group-loot roll/bid response. DropDistributionService red-tolerated.</summary>
public class CM_GROUP_LOOT : AionClientPacket
{
    private int groupId;
    private int index;
    private int unk1;
    private int itemId;
    private int unk2;
    private int unk3;
    private int npcObjId;
    private int distributionMode;
    private int roll;
    private long bid;
    private int unk4;
    private int unk5;

    public CM_GROUP_LOOT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        groupId = ReadD();
        index = ReadD();
        unk1 = ReadD();
        itemId = ReadD();
        unk2 = ReadUC();
        unk3 = ReadUC(); // 3.0
        unk4 = ReadUC(); // 3.5
        npcObjId = ReadD();
        distributionMode = ReadUC();// 2: Roll 3: Bid
        roll = ReadD();// 0: Never Rolled 1: Rolled
        bid = ReadQ();// 0: No Bid else bid amount
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;
        DropDistributionService.GetInstance().HandleRollOrBid(player, distributionMode, roll, bid, itemId, npcObjId, index);
    }
}
