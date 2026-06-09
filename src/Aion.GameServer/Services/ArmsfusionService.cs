using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using PersistentState = Aion.GameServer.Model.GameObjects.IPersistable.PersistentState;

namespace Aion.GameServer.Services;

/// <summary>
/// This class is responsible for armsfusion related tasks (fusion and breaking, called COMPOUND and DECOMPOUND by the client).
/// Java parity: services/ArmsfusionService (Wakizashi, Source, xTz, Neon).
/// </summary>
public class ArmsfusionService
{
    public static void FusionWeapons(Player player, int mainWeaponObjId, int fuseWeaponObjId)
    {
        Item mainWeapon = player.GetInventory().GetItemByObjId(mainWeaponObjId);
        Item fuseWeapon = player.GetInventory().GetItemByObjId(fuseWeaponObjId);

        // Check if item is in bag
        if (mainWeapon == null || fuseWeapon == null)
        {
            if (player.GetEquipment().GetEquippedItemByObjId(mainWeaponObjId) != null
                || player.GetEquipment().GetEquippedItemByObjId(fuseWeaponObjId) != null)
                PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorEquipedItem());
            else
            {
                PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundItemNoTargetItem());
                AuditLogger.Log(player, "tried to fuse weapons he doesn't have (obj IDs:" + mainWeaponObjId + ", " + fuseWeaponObjId + ")");
            }
            return;
        }

        if (!mainWeapon.GetItemTemplate().IsCanFuse() || !fuseWeapon.GetItemTemplate().IsCanFuse())
        {
            Item item = mainWeapon.GetItemTemplate().IsCanFuse() ? mainWeapon : fuseWeapon;
            PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorNotAvailable(item.GetL10n()));
            AuditLogger.Log(player,
                "tried to fuse item " + fuseWeapon.GetItemId() + " onto " + mainWeapon.GetItemId() + " (" + item.GetItemId() + " isn't fusible)");
            return;
        }

        long basePricePerLevelSquared = GetBasePricePerLevelSquared(mainWeapon.GetItemTemplate().GetItemQuality());
        int level = mainWeapon.GetItemTemplate().GetLevel();
        long price = PricesService.GetPriceForService(basePricePerLevelSquared * level * level, player.GetRace());

        if (player.GetInventory().GetKinah() < price)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorNotEnoughMoney(mainWeapon.GetL10n(), fuseWeapon.GetL10n()));
            return;
        }

        if (mainWeapon.GetTemporaryExchangeTime() != 0)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorTemporaryExchangeItem());
            return;
        }

        // Fusioned weapons must be not fusioned
        if (mainWeapon.HasFusionedItem() || fuseWeapon.HasFusionedItem())
        {
            Item item = mainWeapon.HasFusionedItem() ? mainWeapon : fuseWeapon;
            PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorNotAvailable(item.GetL10n()));
            return;
        }

        // Fusioned weapons must have same type
        if (mainWeapon.GetItemTemplate().GetItemGroup() != fuseWeapon.GetItemTemplate().GetItemGroup())
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorDifferentType());
            return;
        }

        // Second weapon must have inferior or equal lvl. in relation to first weapon
        if (fuseWeapon.GetItemTemplate().GetLevel() > mainWeapon.GetItemTemplate().GetLevel())
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorMainRequireHigherLevel());
            return;
        }

        // You can not combine Conditioning and Augmenting
        if (mainWeapon.GetImprovement() != null && fuseWeapon.GetImprovement() != null)
        {
            if (mainWeapon.GetImprovement().GetChargeWay() != fuseWeapon.GetImprovement().GetChargeWay())
            {
                PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundErrorNotComparableItem());
                return;
            }
        }

        if (!player.GetInventory().DecreaseByObjectId(fuseWeaponObjId, 1))
            return;
        mainWeapon.SetFusionedItem(fuseWeapon);
        ItemSocketService.CopyFusionStones(fuseWeapon, mainWeapon);
        mainWeapon.SetPersistentState(PersistentState.UPDATE_REQUIRED);
        InventoryDAO.Store(mainWeapon, player);

        ItemPacketService.UpdateItemAfterInfoChange(player, mainWeapon);
        player.GetInventory().DecreaseKinah(price);
        PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundSuccess(mainWeapon.GetL10n(), fuseWeapon.GetL10n()));
    }

    private static long GetBasePricePerLevelSquared(ItemQuality rarity)
    {
        switch (rarity)
        {
            case ItemQuality.JUNK:
            case ItemQuality.COMMON:
                return 200;
            case ItemQuality.RARE:
                return 250;
            case ItemQuality.LEGEND:
                return 300;
            case ItemQuality.UNIQUE:
                return 400;
            case ItemQuality.EPIC:
                return 500;
            case ItemQuality.MYTHIC:
            default:
                return 600;
        }
    }

    public static void BreakWeapons(Player player, int weaponToBreakUniqueId)
    {
        Item weaponToBreak = player.GetInventory().GetItemByObjId(weaponToBreakUniqueId);

        if (weaponToBreak == null)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.DecompoundItemNoTargetItem());
            return;
        }

        if (!weaponToBreak.HasFusionedItem())
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.DecompoundErrorNotAvailable(weaponToBreak.GetL10n()));
            return;
        }

        weaponToBreak.SetFusionedItem(null);
        InventoryDAO.Store(weaponToBreak, player);

        ItemPacketService.UpdateItemAfterInfoChange(player, weaponToBreak);

        PacketSendUtility.SendPacket(player, SmSystemMessage.CompoundedItemDecompoundSuccess(weaponToBreak.GetL10n()));
    }
}
