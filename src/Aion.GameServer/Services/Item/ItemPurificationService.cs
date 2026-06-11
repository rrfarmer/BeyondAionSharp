using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Items.Bonuses;
using Aion.GameServer.Model.Templates.Items.Purification;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemPurificationService (Ranastic, Estrayl). isPurificationAllowed (validate template/result/identify/enchant/AP/kinah/materials), decreaseMaterials (consume materials + AP + kinah + base item), upgradeItem (build result item carrying over sockets/creator/tune/enchant/amplify/fusion/stones/godstone/tempering/soulbound/bonus stats/color). Map.get->GetValueOrDefault; Math.max/min->Math.Max/Min. PurificationResult/RequiredMaterial/templates/SM_ red-tolerated.</summary>
public class ItemPurificationService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ItemPurificationService));

    public static bool IsPurificationAllowed(Player player, Item baseItem, int resultItemId)
    {
        ItemPurificationTemplate itemPurificationTemplate = DataManager.ITEM_PURIFICATION_DATA.GetItemPurificationTemplate(baseItem.GetItemId());
        if (itemPurificationTemplate == null)
        {
            log.LogWarning("Item purification template is not available for [resultItemId=" + resultItemId + "]");
            return false;
        }

        Dictionary<int, PurificationResult> resultItemMap = DataManager.ITEM_PURIFICATION_DATA.GetResultItemMap(baseItem.GetItemId());
        PurificationResult purificationResult = resultItemMap.GetValueOrDefault(resultItemId);
        if (purificationResult == null)
        {
            AuditLogger.Log(player,
                "tried to purify an item to an invalid result [baseItemId=" + baseItem.GetItemId() + ", resultItemId=" + resultItemId + "]");
            return false;
        }

        if (!baseItem.IsIdentified())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REGISTER_ITEM_MSG_UPGRADE_CANNOT_NO_IDENTIFY());
            return false;
        }

        if (baseItem.GetEnchantLevel() < purificationResult.GetMinEnchantCount())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REGISTER_ITEM_MSG_UPGRADE_CANNOT(baseItem.GetL10n()));
            return false;
        }

        if (player.GetAbyssRank().GetAp() < purificationResult.GetNecessaryAbyssPoints())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REGISTER_ITEM_MSG_UPGRADE_CANNOT_NEED_AP());
            return false;
        }

        if (player.GetInventory().GetKinah() < purificationResult.GetNecessaryKinah())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REGISTER_ITEM_MSG_UPGRADE_CANNOT_NEED_QINA());
            return false;
        }

        foreach (RequiredMaterial reqMat in purificationResult.GetRequiredMaterials())
            if (player.GetInventory().GetItemCountByItemId(reqMat.GetItemId()) < reqMat.GetItemCount())
                return false;

        string resultItemL10n = DataManager.ITEM_DATA.GetItemTemplate(resultItemId).GetL10n();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_UPGRADE_MSG_UPGRADE_SUCCESS(baseItem.GetL10n(), resultItemL10n));
        return true;
    }

    public static bool DecreaseMaterials(Player player, Item baseItem, int resultItemId)
    {
        Dictionary<int, PurificationResult> resultItemMap = DataManager.ITEM_PURIFICATION_DATA.GetResultItemMap(baseItem.GetItemId());

        PurificationResult purificationResult = resultItemMap.GetValueOrDefault(resultItemId);

        foreach (RequiredMaterial reqMaterial in purificationResult.GetRequiredMaterials())
        {
            if (!player.GetInventory().DecreaseByItemId(reqMaterial.GetItemId(), reqMaterial.GetItemCount()))
            {
                AuditLogger.Log(player, "tried to use item purification with insufficient materials [baseItemId=" + baseItem.GetItemId() + ", resultItemId="
                    + resultItemId + ", reqMaterialId=" + reqMaterial.GetItemId() + ", reqMaterialCount=" + reqMaterial.GetItemCount() + "]");
                return false;
            }
        }

        if (purificationResult.GetNecessaryAbyssPoints() > 0)
            AbyssPointsService.AddAp(player, -purificationResult.GetNecessaryAbyssPoints());

        if (purificationResult.GetNecessaryKinah() > 0)
            player.GetInventory().DecreaseKinah(-purificationResult.GetNecessaryKinah());

        player.GetInventory().DecreaseByObjectId(baseItem.GetObjectId(), 1);

        return true;
    }

    public static void UpgradeItem(Player player, Item sourceItem, int targetItemId)
    {
        Item newItem = ItemFactory.NewItem(targetItemId, 1);
        newItem.SetOptionalSockets(sourceItem.GetOptionalSockets());
        newItem.SetItemCreator(sourceItem.GetItemCreator());
        newItem.SetTuneCount(Math.Max(0, Math.Min(sourceItem.GetTuneCount(), newItem.GetItemTemplate().GetMaxTuneCount())));
        newItem.SetEnchantLevel(sourceItem.GetEnchantLevel() - 5);
        newItem.SetEnchantBonus(sourceItem.GetEnchantBonus());
        newItem.SetAmplified(sourceItem.IsAmplified() && newItem.GetEnchantLevel() >= newItem.GetMaxEnchantLevel());
        if (newItem.IsAmplified() && newItem.GetEnchantLevel() >= 20)
        {
            newItem.SetBuffSkill(sourceItem.GetBuffSkill());
        }
        if (sourceItem.HasFusionedItem())
        {
            newItem.SetFusionedItem(sourceItem.GetFusionedItemTemplate(), sourceItem.GetFusionedItemBonusStatsId(),
                sourceItem.GetFusionedItemOptionalSockets());
        }
        if (sourceItem.HasManaStones())
        {
            foreach (ManaStone manaStone in sourceItem.GetItemStones())
                ItemSocketService.AddManaStone(newItem, manaStone.GetItemId(), false);
        }
        if (sourceItem.HasFusionStones())
        {
            foreach (ManaStone manaStone in sourceItem.GetFusionStones())
                ItemSocketService.AddManaStone(newItem, manaStone.GetItemId(), true);
        }
        if (sourceItem.GetGodStone() != null)
            newItem.AddGodStone(sourceItem.GetGodStone().GetItemId(), sourceItem.GetGodStone().GetActivatedCount());
        if (sourceItem.GetTempering() > 0)
            newItem.SetTempering(sourceItem.GetTempering());
        if (sourceItem.IsSoulBound())
            newItem.SetSoulBound(true);
        if (sourceItem.GetBonusStatsId() > 0)
        {
            int statBonusId = sourceItem.GetBonusStatsId();
            if (!DataManager.ITEM_RANDOM_BONUSES.AreBonusSetsEqual(StatBonusType.INVENTORY, sourceItem.GetItemTemplate().GetStatBonusSetId(),
                newItem.GetItemTemplate().GetStatBonusSetId()))
            {
                statBonusId = TuningAction.GetRandomStatBonusIdFor(newItem);
            }
            newItem.SetBonusStats(statBonusId, true);
        }
        newItem.SetItemColor(sourceItem.GetItemColor());
        player.GetInventory().Add(newItem);
    }
}
