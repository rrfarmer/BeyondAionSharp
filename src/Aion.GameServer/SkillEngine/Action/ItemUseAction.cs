using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Action;

/// <summary>Java parity: skillengine/action/ItemUseAction (ATracer) : Action. @XmlAttribute itemid/count; act: Player→ITEM_DATA template, inventory.decreaseByItemId(itemid, count) false→STR_SKILL_NOT_ENOUGH_ITEM false. ItemTemplate/Storage red-tolerated.</summary>
[XmlType("ItemUseAction")]
public class ItemUseAction : Action
{
    [XmlAttribute]
    protected int itemid;

    [XmlAttribute]
    protected int count;

    public override bool Act(Skill skill)
    {
        if (skill.GetEffector() is Player)
        {
            ItemTemplate item = DataManager.ITEM_DATA.GetItemTemplate(itemid);
            Player player = (Player)skill.GetEffector();
            Storage inventory = player.GetInventory();
            if (!inventory.DecreaseByItemId(itemid, count))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_ITEM(item.GetL10n()));
                return false;
            }
        }
        return true;
    }
}
