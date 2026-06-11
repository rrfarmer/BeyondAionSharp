using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Item.Enums;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/PackAction.</summary>
public class PackAction : AbstractItemAction
{
    [XmlAttribute("target")] protected UseTarget target;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (targetItem == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_NO_TARGET_ITEM());
            return false;
        }
        if (Aion.GameServer.Configs.Main.GSConfig.ITEM_WRAP_LIMIT < 0 || Aion.GameServer.Configs.Main.GSConfig.ITEM_WRAP_LIMIT > 127 && Aion.GameServer.Configs.Main.GSConfig.ITEM_WRAP_LIMIT != 255)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_CANNOT(targetItem.GetL10n()));
            return false;
        }
        if (targetItem.IsEquipped())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_EQUIPED());
            return false;
        }
        if (targetItem.GetItemTemplate().IsTradeable())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_EXCHANGE());
            return false;
        }
        if (targetItem.IsSoulBound())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_SEAL());
            return false;
        }
        if (targetItem.GetFusionedItemId() != 0)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_COMPOSITION());
            return false;
        }
        if (!targetItem.IsIdentified())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_NEED_IDENTIFY());
            return false;
        }
        if (targetItem.GetItemTemplate().GetItemQuality().GetQualityId() > parentItem.GetItemTemplate().GetItemQuality().GetQualityId())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_QUALITY(parentItem.GetL10n(), targetItem.GetL10n()));
            return false;
        }
        if (targetItem.GetItemTemplate().GetLevel() > parentItem.GetItemTemplate().GetLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_LEVEL(targetItem.GetL10n(), targetItem.GetItemTemplate().GetLevel()));
            return false;
        }
        UseTarget? type = targetItem.GetItemTemplate().GetItemGroup() switch
        {
            ItemGroup.SWORD or ItemGroup.DAGGER or ItemGroup.MACE or ItemGroup.ORB or ItemGroup.SPELLBOOK or ItemGroup.BOW or ItemGroup.GREATSWORD or ItemGroup.POLEARM or ItemGroup.STAFF or ItemGroup.HARP or ItemGroup.GUN or ItemGroup.CANNON or ItemGroup.KEYBLADE => UseTarget.WEAPON,
            ItemGroup.SHIELD or ItemGroup.RB_TORSO or ItemGroup.RB_PANTS or ItemGroup.RB_SHOULDER or ItemGroup.RB_GLOVE or ItemGroup.RB_SHOES or ItemGroup.CL_TORSO or ItemGroup.CL_PANTS or ItemGroup.CL_SHOULDER or ItemGroup.CL_GLOVE or ItemGroup.CL_SHOES or ItemGroup.CH_TORSO or ItemGroup.CH_PANTS or ItemGroup.CH_SHOULDER or ItemGroup.CH_GLOVE or ItemGroup.CH_SHOES or ItemGroup.LT_TORSO or ItemGroup.LT_PANTS or ItemGroup.LT_SHOULDER or ItemGroup.LT_GLOVE or ItemGroup.LT_SHOES or ItemGroup.PL_TORSO or ItemGroup.PL_PANTS or ItemGroup.PL_SHOULDER or ItemGroup.PL_GLOVE or ItemGroup.PL_SHOES => UseTarget.ARMOR,
            ItemGroup.NECKLACE or ItemGroup.EARRING or ItemGroup.RING or ItemGroup.BELT or ItemGroup.HEAD => UseTarget.ACCESSORY,
            _ => (UseTarget?)null,
        };
        if (type == null || target != type)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_WRONG_TARGET_ITEM_CATEGORY(parentItem.GetL10n(), targetItem.GetL10n()));
            return false;
        }
        int packCount = targetItem.GetPackCount();
        if (packCount > 0) // only negative unpacked
        {
            return false;
        }
        if (Aion.GameServer.Configs.Main.GSConfig.ITEM_WRAP_LIMIT != 255)
        {
            if (packCount < 0)
            {
                packCount *= -1;
            }
            int allowedPackCount = targetItem.GetItemTemplate().GetPackCount();
            if (targetItem.GetEnchantLevel() >= 20)
            {
                allowedPackCount += targetItem.GetEnchantLevel() - 19;
            }
            if (packCount >= allowedPackCount)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_CANNOT(targetItem.GetL10n()));
                return false;
            }
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int parentItemId = parentItem.GetItemId();
        int parentObjectId = parentItem.GetObjectId();
        int packCount = targetItem.GetPackCount();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentObjectId, parentItemId, 0, 1, 1), true);
        if (!player.GetInventory().DecreaseByObjectId(parentObjectId, 1))
        {
            return;
        }
        if (packCount < 0)
        {
            packCount *= -1;
        }
        targetItem.SetPackCount(++packCount);
        targetItem.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryUpdateItem(player, targetItem));
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PACK_ITEM_SUCCEED(targetItem.GetL10n()));
    }

    public UseTarget GetTarget()
    {
        return target;
    }
}
