using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/DyeAction (IceReaper, Neon).</summary>
[XmlType("DyeAction")]
public class DyeAction : AbstractItemAction
{
    [XmlAttribute("color")] protected string color;

    // Java parity: @XmlAttribute private Integer minutes; (nullable).
    [XmlIgnore] private int? minutes;

    [XmlAttribute("minutes")]
    public string MinutesXml
    {
        get => minutes?.ToString();
        set => minutes = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value);
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        HouseObject<Aion.GameServer.Model.Templates.Housing.PlaceableHouseObject> targetHouseObject = (HouseObject<Aion.GameServer.Model.Templates.Housing.PlaceableHouseObject>)@params[0];
        if (targetHouseObject == null && targetItem == null) // nothing to dye
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_COLOR_ERROR());
            return false;
        }
        if (targetHouseObject != null)
        {
            if (color.Equals("no") && targetHouseObject.GetColor() == null)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_PAINT_ERROR_CANNOTREMOVE());
                return false;
            }
            if (!targetHouseObject.GetObjectTemplate().GetCanDye())
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_PAINT_ERROR_CANNOTPAINT());
                return false;
            }
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        HouseObject<Aion.GameServer.Model.Templates.Housing.PlaceableHouseObject> targetHouseObject = (HouseObject<Aion.GameServer.Model.Templates.Housing.PlaceableHouseObject>)@params[0];
        if (targetHouseObject == null)
            DyeItem(player, parentItem, targetItem);
        else
            DyeHouseObject(player, parentItem, targetHouseObject);
    }

    private void DyeItem(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
    {
        if (!targetItem.GetItemSkinTemplate().IsItemDyePermitted())
            return;
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return;
        targetItem.SetItemColor(GetColor());
        if (minutes != null)
            targetItem.SetColorExpireTime((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000 + minutes.Value * 60));
        else
            targetItem.SetColorExpireTime(0);
        if (targetItem.GetItemColor() == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_COLOR_REMOVE_SUCCEED(targetItem.GetL10n()));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_COLOR_CHANGE_SUCCEED(targetItem.GetL10n(), parentItem.GetL10n()));
        }

        // item is equipped, so need broadcast packet
        if (player.GetEquipment().GetEquippedItemByObjId(targetItem.GetObjectId()) != null)
        {
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmUpdatePlayerAppearance(player.GetObjectId(), player.GetEquipment().GetEquippedForAppearance()), true);
            player.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
        else // item is not equipped
        {
            player.GetInventory().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }

        Aion.GameServer.Services.Items.ItemPacketService.UpdateItemAfterInfoChange(player, targetItem);
    }

    public int? GetColor()
    {
        return color.Equals("no") ? (int?)null : Convert.ToInt32(color, 16);
    }

    private void DyeHouseObject(Aion.GameServer.Model.GameObjects.Players.Player player, Item dyeItem, HouseObject<Aion.GameServer.Model.Templates.Housing.PlaceableHouseObject> houseObject)
    {
        if (!player.GetInventory().DecreaseByObjectId(dyeItem.GetObjectId(), 1))
            return;
        houseObject.SetColor(GetColor());
        float x = houseObject.GetX();
        float y = houseObject.GetY();
        float z = houseObject.GetZ();
        int rotation = houseObject.GetRotation();
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmHouseEdit(7, 0, houseObject.GetObjectId()));
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmHouseEdit(5, houseObject.GetObjectId(), x, y, z, rotation));
        houseObject.Spawn();
        string objectName = houseObject.GetObjectTemplate().GetL10n();
        if (houseObject.GetColor() == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_PAINT_REMOVE_SUCCEED(objectName));
        }
        else
        {
            string paintName = dyeItem.GetItemTemplate().GetL10n();
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_PAINT_SUCCEED(objectName, paintName));
        }
    }
}
