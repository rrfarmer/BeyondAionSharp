using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Items;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ITEM_PURIFICATION (FinalNovas, Navyan). Purifies/upgrades an item if allowed and materials are available. ItemPurificationService red-tolerated.</summary>
public class CM_ITEM_PURIFICATION : AionClientPacket
{
    private int playerObjectId, requireItemObjectId1, requireItemObjectId2, requireItemObjectId3, requireItemObjectId4, requireItemObjectId5;
    private int upgradedItemObjectId;
    private int resultItemId;

    public CM_ITEM_PURIFICATION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        playerObjectId = ReadD();
        upgradedItemObjectId = ReadD();
        resultItemId = ReadD();
        requireItemObjectId1 = ReadD();
        requireItemObjectId2 = ReadD();
        requireItemObjectId3 = ReadD();
        requireItemObjectId4 = ReadD();
        requireItemObjectId5 = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        Item baseItem = player.GetInventory().GetItemByObjId(upgradedItemObjectId);
        if (!ItemPurificationService.IsPurificationAllowed(player, baseItem, resultItemId))
            return;

        if (!ItemPurificationService.DecreaseMaterials(player, baseItem, resultItemId))
            return;

        ItemPurificationService.UpgradeItem(player, baseItem, resultItemId);
    }
}
