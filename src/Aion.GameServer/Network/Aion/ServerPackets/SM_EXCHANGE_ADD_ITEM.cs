using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EXCHANGE_ADD_ITEM (Avol, ATracer). Sends an item added to an exchange slot: action byte, template/object id, name, then the full ItemInfoBlob. getBuf()->GetBuf(); ItemInfoBlob red-tolerated.</summary>
public class SM_EXCHANGE_ADD_ITEM : AionServerPacket
{
    private Player player;
    private int action;
    private Item item;

    public SM_EXCHANGE_ADD_ITEM(int action, Item item, Player player)
    {
        this.player = player;
        this.action = action;
        this.item = item;
    }

    protected override void WriteImpl(AionConnection con)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteC(action); // 0 -self 1-other

        WriteD(itemTemplate.GetTemplateId());
        WriteD(item.GetObjectId());
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
        itemInfoBlob.WriteMe(GetBuf());
    }
}
