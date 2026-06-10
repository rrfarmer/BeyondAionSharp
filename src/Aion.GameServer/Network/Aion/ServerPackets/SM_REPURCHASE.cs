using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_REPURCHASE (xTz, KID). Sends the NPC repurchase list (obj/template/name + item info blob + repurchase price). Converges RepurchaseService. Collection->ICollection; ItemInfoBlob.writeMe(getBuf())->WriteMe(GetBuf()). Item/ItemInfoBlob red-tolerated.</summary>
public class SM_REPURCHASE : AionServerPacket
{
    private Player player;
    private readonly int targetObjectId;
    private readonly ICollection<Item> items;

    public SM_REPURCHASE(Player player, int npcId)
    {
        this.player = player;
        this.targetObjectId = npcId;
        items = RepurchaseService.GetInstance().GetRepurchaseItems(player.GetObjectId());
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjectId);
        WriteD(1);
        WriteH(items.Count);

        foreach (Item item in items)
        {
            ItemTemplate itemTemplate = item.GetItemTemplate();

            WriteD(item.GetObjectId());
            WriteD(itemTemplate.GetTemplateId());
            WriteS(itemTemplate.GetL10n());

            ItemInfoBlob itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
            itemInfoBlob.WriteMe(GetBuf());

            WriteQ(item.GetRepurchasePrice());
        }
    }
}
