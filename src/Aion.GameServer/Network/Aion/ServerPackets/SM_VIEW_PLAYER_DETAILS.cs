using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_VIEW_PLAYER_DETAILS (Avol, xTz). Shows another player's equipped items (per-item info blob). getBuf()->GetBuf(). Item/ItemInfoBlob red-tolerated.</summary>
public class SM_VIEW_PLAYER_DETAILS : AionServerPacket
{
    private List<Item> items;
    private int itemSize;
    private int targetObjId;
    private Player player;

    public SM_VIEW_PLAYER_DETAILS(List<Item> items, Player player)
    {
        this.player = player;
        this.targetObjId = player.GetObjectId();
        this.items = items;
        this.itemSize = items.Count;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjId);
        WriteC(11);
        WriteH(itemSize);
        foreach (Item item in items)
        {
            WriteItemInfo(item);
        }
    }

    private void WriteItemInfo(Item item)
    {
        ItemTemplate template = item.GetItemTemplate();

        WriteD(0);
        WriteD(template.GetTemplateId());
        WriteS(template.GetL10n());

        ItemInfoBlob itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
        itemInfoBlob.WriteMe(GetBuf());
    }
}
