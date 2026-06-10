using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LOOT_ITEMLIST (alexa026, Avol, Metos, ATracer, KID, Sykra). Drop list for a corpse: per-item index/itemId/count/socket + loot-confirmation flag. (int) cast on count; Set/Collection iteration. Drop/DropItem/DropNpc/DataManager red-tolerated.</summary>
public class SM_LOOT_ITEMLIST : AionServerPacket
{
    private readonly int targetObjectId;
    private readonly bool teamMembersNearby;
    private readonly List<DropItem> dropItems;

    public SM_LOOT_ITEMLIST(DropNpc dropNpc, ISet<DropItem> setItems, Player player)
    {
        this.targetObjectId = dropNpc.GetObjectId();
        ICollection<Player> playersInRange = dropNpc.GetInRangePlayers();
        this.teamMembersNearby = playersInRange.Count > 1 && playersInRange.Contains(player);
        this.dropItems = new List<DropItem>();
        foreach (DropItem item in setItems)
            if (item.CanViewDropItem(player.GetObjectId()))
                dropItems.Add(item);
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player activePlayer = con.GetActivePlayer();
        if (activePlayer == null)
            return;
        WriteD(targetObjectId);
        WriteC(dropItems.Count);

        foreach (DropItem dropItem in dropItems)
        {
            Drop drop = dropItem.GetDropTemplate();
            WriteC(dropItem.GetIndex()); // index in droplist
            WriteD(drop.GetItemId());
            WriteD((int)dropItem.GetCount());
            WriteC(dropItem.GetOptionalSocket());
            WriteC(0);
            WriteC(0); // 3.5
            ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(drop.GetItemId());
            bool showLootConfirmation = !template.IsTradeable();
            if (dropItem.IsOnlyPossibleLooter(activePlayer) || !teamMembersNearby)
                showLootConfirmation = false;
            WriteC(showLootConfirmation ? 1 : 0);
        }
    }
}
