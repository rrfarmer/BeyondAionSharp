using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INVENTORY_INFO (-Nemesiss-, alexa026, Avol, ATracer, Rolandas, Artur). Sends inventory contents (cube expands + per-item info blob). removeAll(singletonList(null))->RemoveAll(x=>x==null); getBuf()->GetBuf(). Item/ItemInfoBlob red-tolerated.</summary>
public class SM_INVENTORY_INFO : AionServerPacket
{
    private bool isFirstPacket;
    private List<Item> items;
    private Player player;

    public SM_INVENTORY_INFO(bool isFirstPacket, List<Item> items, Player player)
    {
        // this should prevent client crashes but need to discover when item is null
        items.RemoveAll(x => x == null);
        this.isFirstPacket = isFirstPacket;
        this.items = items;
        this.player = player;
    }

    protected override void WriteImpl(AionConnection con)
    {
        // something wrong with cube part.
        WriteC(isFirstPacket ? 1 : 0);
        WriteC(player.GetNpcExpands()); // cube size from npc (so max 5 for now)
        WriteC(player.GetQuestExpands()); // cube size from quest (so max 2 for now)
        WriteC(player.GetItemExpands()); // count of ticket expands
        WriteH(items.Count); // number of entries
        foreach (Item item in items)
            WriteItemInfo(item);
    }

    private void WriteItemInfo(Item item)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteD(item.GetObjectId());
        WriteD(itemTemplate.GetTemplateId());
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
        itemInfoBlob.WriteMe(GetBuf());

        // invisible -1, visible is a slot
        WriteH((int)(item.GetEquipmentSlot() & 0xFFFF));

        // probably a right to equip the item, related to passive skill learn
        WriteC(itemTemplate.IsCloth() ? 1 : 0);
    }
}
