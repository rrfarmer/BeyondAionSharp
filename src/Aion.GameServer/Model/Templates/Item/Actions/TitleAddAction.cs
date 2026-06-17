using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/TitleAddAction.</summary>
[XmlType("TitleAddAction")]
public class TitleAddAction : AbstractItemAction
{
    [XmlAttribute("titleid")] public int titleid;
    // Java parity: @XmlAttribute("minutes") Integer (nullable). XmlSerializer cannot bind Nullable<T> as an
    // attribute, so round-trip via a string proxy (null when absent).
    [XmlIgnore] public int? minutes;

    [XmlAttribute("minutes")]
    public string MinutesRaw
    {
        get => minutes?.ToString();
        set => minutes = value == null ? (int?)null : int.Parse(value);
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (titleid == 0 || parentItem == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_ERROR());
            return false;
        }
        if (player.GetTitleList().Contains(titleid))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_TOOLTIP_LEARNED_TITLE());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), itemTemplate.GetTemplateId()), true);

        if (player.GetTitleList().AddTitle(titleid, false, minutes == null ? 0 : ((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)) + minutes.Value * 60))
        {
            Item item = player.GetInventory().GetItemByObjId(parentItem.GetObjectId());
            player.GetInventory().Delete(item);
        }
    }
}
