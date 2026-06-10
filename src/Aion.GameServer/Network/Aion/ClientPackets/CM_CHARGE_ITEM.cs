using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Item;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHARGE_ITEM (ATracer). Charges a list of items at a target NPC. ItemChargeService red-tolerated.</summary>
public class CM_CHARGE_ITEM : AionClientPacket
{
    private int targetNpcObjectId;
    private int chargeLevel;
    private List<int> itemObjectIds;

    public CM_CHARGE_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetNpcObjectId = ReadD();
        chargeLevel = ReadUC();
        int itemsSize = ReadUH();
        itemObjectIds = new List<int>();
        for (int i = 0; i < itemsSize; i++)
            itemObjectIds.Add(ReadD());
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (!player.IsTargeting(targetNpcObjectId))
        {
            return; // TODO audit?
        }

        List<Item> itemsToCharge = new List<Item>();
        foreach (int itemObjId in itemObjectIds)
        {
            Item item = player.GetInventory().GetItemByObjId(itemObjId);
            if (item != null)
                itemsToCharge.Add(item);
        }
        ItemChargeService.ChargeItems(player, itemsToCharge, chargeLevel, false, true);
    }
}
