using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Listeners;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using ItemTemplate = Aion.GameServer.Model.Templates.Items.ItemTemplate;
using Rates = Aion.GameServer.Model.GameObjects.Players.Rates;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/EnchantService (ATracer, Wakizashi, Source, vlog, Neon).</summary>
public class EnchantService
{
    public static bool BreakItem(Player player, Item targetItem, Item parentItem)
    {
        Storage inventory = player.GetInventory();
        if (inventory.GetItemByObjId(targetItem.GetObjectId()) == null || inventory.GetItemByObjId(parentItem.GetObjectId()) == null)
            return false;

        ItemTemplate itemTemplate = targetItem.GetItemTemplate();
        if (!itemTemplate.IsArmor() && !itemTemplate.IsWeapon())
        {
            AuditLogger.Log(player, "tried to break down incompatible item type");
            return false;
        }

        int effectiveLevel = CalculateEffectiveLevel(itemTemplate.GetItemQuality(), itemTemplate.GetLevel());
        if (effectiveLevel == 0)
            throw new ArgumentException("Invalid item quality for breaking item " + itemTemplate.GetItemQuality());

        int rndEffectiveLevel = effectiveLevel + Rnd.Get(0, 10);
        if (itemTemplate.IsWeapon())
            rndEffectiveLevel += 5;

        // Omega Stones are limited to Drops
        int stoneId;
        if (rndEffectiveLevel >= CalculateEffectiveLevel(EnchantmentStone.EPSILON))
            stoneId = 166000195;
        else if (rndEffectiveLevel >= CalculateEffectiveLevel(EnchantmentStone.DELTA))
            stoneId = 166000194;
        else if (rndEffectiveLevel >= CalculateEffectiveLevel(EnchantmentStone.GAMMA))
            stoneId = 166000193;
        else if (rndEffectiveLevel >= CalculateEffectiveLevel(EnchantmentStone.BETA))
            stoneId = 166000192;
        else
            stoneId = 166000191; // Alpha

        if (inventory.Delete(targetItem) != null)
        {
            if (inventory.DecreaseByObjectId(parentItem.GetObjectId(), 1))
                ItemService.AddItem(player, stoneId, itemTemplate.IsWeapon() ? Rnd.Get(2, 5) : Rnd.Get(1, 3));
        }
        else
        {
            AuditLogger.Log(player, "possibly used break item hack");
        }
        return true;
    }

    private static int CalculateEffectiveLevel(EnchantmentStone enchantmentStone)
    {
        return CalculateEffectiveLevel(enchantmentStone.GetBaseQuality(), enchantmentStone.GetBaseLevel());
    }

    private static int CalculateEffectiveLevel(ItemQuality itemQuality, int itemLevel)
    {
        switch (itemQuality)
        {
            case ItemQuality.COMMON: // same as rare, since there's no EnchantmentStone enum having COMMON as a base
            case ItemQuality.RARE:
                return itemLevel + 5;
            case ItemQuality.LEGEND:
                return itemLevel + 10;
            case ItemQuality.UNIQUE:
                return itemLevel + 15;
            case ItemQuality.EPIC:
                return itemLevel + 20;
            case ItemQuality.MYTHIC:
                return itemLevel + 25;
            default:
                return 0;
        }
    }

    public static bool EnchantItem(Player player, Item enchantmentStoneItem, Item targetItem, Item supplementItem)
    {
        float successChance;

        if (targetItem.IsAmplified())
        {
            successChance = Rates.Get(player, RatesConfig.ENCHANTMENT_STONE_AMPLIFIED_CHANCES);
        }
        else
        {
            successChance = Rates.Get(player, RatesConfig.ENCHANTMENT_STONE_BASE_CHANCES);

            EnchantmentStone enchantmentStone = EnchantmentStoneExtensions.GetByItemId(enchantmentStoneItem.GetItemId());
            int itemLevel = targetItem.GetItemTemplate().GetLevel();
            if (itemLevel < EnchantmentStone.ALPHA.GetBaseLevel()) // ensure low lvl items don't get too high success chances
                itemLevel = EnchantmentStone.ALPHA.GetBaseLevel();
            int stoneToItemLevelDiff = enchantmentStone.GetBaseLevel() - itemLevel;
            int stoneToItemQualityDiff = enchantmentStone.GetBaseQuality().GetQualityId() - targetItem.GetItemTemplate().GetItemQuality().GetQualityId();

            successChance += stoneToItemLevelDiff; // absolutely increase/reduce chance by 1% for every level difference
            successChance += stoneToItemQualityDiff * 5; // absolutely increase/reduce chance by 5% for each quality difference

            if (targetItem.GetEnchantLevel() == 0) // boost enchant chance for +1 by 20%
                successChance *= 1.2f;
            else if (targetItem.GetEnchantLevel() < 5) // boost enchant chance up to +5 by 10%
                successChance *= 1.1f;
            else if (targetItem.GetEnchantLevel() >= 10) // reduce enchant chance from +10 by 10%
                successChance *= 0.9f;

            // Retail Tests: 80% = Success Cap for Enchanting without Supplements
            if (successChance >= 80)
                successChance = 80;

            // Supplement is used
            if (supplementItem != null)
            {
                // Amount of supplement items
                int supplementUseCount = 1;
                // Additional success rate for the supplement
                ItemTemplate supplementTemplate = supplementItem.GetItemTemplate();

                EnchantItemAction action = supplementTemplate.GetActions().GetEnchantAction();
                if (action != null)
                {
                    if (action.IsManastoneOnly())
                        return false;
                    // Add success rate of the supplement to the overall chance
                    successChance += action.GetChance();
                }

                action = enchantmentStoneItem.GetItemTemplate().GetActions().GetEnchantAction();
                if (action != null)
                    supplementUseCount = action.GetCount();

                // Beginning from enchanting to +11, there are 2 times more supplements required
                if (targetItem.GetEnchantLevel() >= 10)
                    supplementUseCount = supplementUseCount * 2;

                // Check the required amount of the supplements
                if (player.GetInventory().GetItemCountByItemId(supplementTemplate.GetTemplateId()) < supplementUseCount)
                    return false;

                // Put supplements to wait for update
                player.SubtractSupplements(supplementUseCount, supplementTemplate.GetTemplateId());

                // Success can't be higher than 95%
                if (successChance >= 95)
                    successChance = 95;
            }
        }

        bool result = Rnd.Chance() < successChance;

        if (player.HasAccess(AdminConfig.ENCHANT_INFO))
            PacketSendUtility.SendMessage(player, (result ? "Success" : "Fail") + " (success chance:" + successChance + "%)");

        return result;
    }

    public static void EnchantItemAct(Player player, Item parentItem, Item targetItem, Item supplementItem, int currentEnchant, bool success)
    {
        int addLevel = 1;

        int maxEnchant = targetItem.GetItemTemplate().GetMaxEnchantLevel(); // max enchant level from item_templates
        maxEnchant += targetItem.GetEnchantBonus();
        if (targetItem.GetEnchantLevel() < 20)
        {
            float chance = Rnd.Chance(); // crit modifier
            if (chance < 5)
                addLevel = 3;
            else if (chance < 10)
                addLevel = 2;
        }

        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
        {
            AuditLogger.Log(player, "possibly used enchant hack");
            return;
        }
        // Decrease required supplements
        player.UpdateSupplements();

        // Items that are Fabled or Eternal can get up to +15.
        if (success)
        {
            if (!targetItem.IsAmplified() && currentEnchant + addLevel > maxEnchant)
                currentEnchant = maxEnchant;
            else
                currentEnchant += addLevel;
        }
        else
        {
            // Retail: http://powerwiki.na.aiononline.com/aion/Patch+Notes:+1.9.0.1
            // When socketing fails at +11~+15, the value falls back to +10.
            if (targetItem.IsAmplified())
            {
                currentEnchant = maxEnchant;
                targetItem.SetAmplified(false);
            }
            else if (currentEnchant > 10 && maxEnchant > 10)
            {
                currentEnchant = 10;
            }
            else if (currentEnchant > 0)
            {
                currentEnchant -= 1;
            }
        }

        if (targetItem.IsAmplified())
            maxEnchant = 255;

        SetEnchantLevel(player, targetItem, Math.Min(currentEnchant, maxEnchant));

        if (success)
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEED_NEW(targetItem.GetL10n(), targetItem.GetEnchantLevel()));
        }
        else
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_FAILED(targetItem.GetL10n()));
            if (targetItem.GetItemTemplate().GetEnchantType() > 0)
            {
                if (targetItem.IsEquipped())
                    player.GetEquipment().DecreaseEquippedItemCount(targetItem.GetObjectId(), 1);
                else
                    player.GetInventory().DecreaseByObjectId(targetItem.GetObjectId(), 1);
                PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_TYPE1_ENCHANT_FAIL(targetItem.GetL10n()));
            }
            else
            {
                targetItem.RemoveRemainingTuningCountIfPossible();
            }
        }
    }

    public static void SetEnchantLevel(Player player, Item item, int enchantLevel)
    {
        item.SetEnchantLevel(enchantLevel);
        int oldBuffId = item.GetBuffSkill();
        int newBuffId = 0;
        if (enchantLevel >= 20)
            newBuffId = GetEquipBuff(item);
        if (newBuffId != oldBuffId)
        {
            item.SetBuffSkill(newBuffId);
            if (item.IsEquipped())
            {
                if (oldBuffId != 0)
                    SkillLearnService.RemoveSkill(player, oldBuffId);
                if (newBuffId != 0)
                    SkillLearnService.LearnTemporarySkill(player, newBuffId, 1);
            }
        }
        if (newBuffId != 0)
            PacketSendUtility.SendPacket(player,
                Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_SKILL_ENCHANT(item.GetL10n(), enchantLevel, DataManager.SKILL_DATA.GetSkillTemplate(newBuffId).GetL10n()));
        if (item.GetEnchantEffect() != null)
        {
            item.GetEnchantEffect().EndEffect(player);
            item.SetEnchantEffect(null);
        }
        if (item.IsEquipped())
        {
            player.GetGameStats().UpdateStatsVisually();
            if (enchantLevel > 0)
                ApplyEnchantEffect(item, player, enchantLevel);
        }

        ItemPacketService.UpdateItemAfterInfoChange(player, item, ItemPacketService.ItemUpdateType.STATS_CHANGE);
        if (item.IsEquipped())
            player.GetEquipment().SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED);
        else
            player.GetInventory().SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public static void ApplyEnchantEffect(Item targetItem, Player owner, int enchantLevel)
    {
        Dictionary<int, List<EnchantStat>> enchant = DataManager.ENCHANT_DATA.GetTemplates(targetItem.GetItemTemplate());
        if (enchant == null)
            return;
        int maxTemplateLevel = enchant.Keys.Max();
        List<EnchantStat> stats;
        if (enchantLevel > maxTemplateLevel && maxTemplateLevel < 21) // usually only test templates have max level < 21
            throw new ArgumentException("Missing bonus stats for +" + enchantLevel + " (item:" + targetItem.GetItemId() + ") in enchant templates");
        else if (enchantLevel < maxTemplateLevel)
            stats = enchant[enchantLevel];
        else
        {
            // maxTemplateLevel - 1 (second to last template entry) = maximum stats
            stats = new List<EnchantStat>(enchant[maxTemplateLevel - 1]);
            // maxTemplateLevel (last template entry) = bonus stats per level above max
            List<EnchantStat> limitlessBoni = enchant[maxTemplateLevel];
            for (int i = 0; i <= enchantLevel - maxTemplateLevel; i++)
                stats.AddRange(limitlessBoni);
        }
        if (targetItem.GetEnchantEffect() != null)
            targetItem.GetEnchantEffect().EndEffect(owner);
        targetItem.SetEnchantEffect(new EnchantEffect(targetItem, owner, stats));
    }

    public static bool SocketManastone(Player player, Item manastone, Item targetItem, Item supplementItem, int fusionedWeaponLevel)
    {
        int targetItemLevel;

        // Fusioned weapon. Primary weapon level.
        if (fusionedWeaponLevel == 1)
            targetItemLevel = targetItem.GetItemTemplate().GetLevel();
        // Fusioned weapon. Secondary weapon level.
        else
            targetItemLevel = targetItem.GetFusionedItemTemplate().GetLevel();

        int stoneLevel = manastone.GetItemTemplate().GetLevel();
        int slotLevel = (int)(10 * Math.Ceiling((targetItemLevel + 10) / 10d));

        // The current amount of socketed stones
        int stoneCount;

        // Manastone level shouldn't be greater as 20 + item level
        // Example: item level: 1 - 10. Manastone level should be <= 20
        if (stoneLevel > slotLevel)
            return false;

        // Fusioned weapon. Primary weapon slots.
        if (fusionedWeaponLevel == 1)
            // Count the inserted stones in the primary weapon
            stoneCount = targetItem.GetItemStones().Count;
        // Fusioned weapon. Secondary weapon slots.
        else
            // Count the inserted stones in the secondary weapon
            stoneCount = targetItem.GetFusionStones().Count;

        // Fusioned weapon. Primary weapon slots.
        if (fusionedWeaponLevel == 1)
        {
            // Find all free slots in the primary weapon
            if (stoneCount >= targetItem.GetSockets(false))
            {
                AuditLogger.Log(player, "Manastone socket overload");
                return false;
            }
        }
        // Fusioned weapon. Secondary weapon slots.
        else if (!targetItem.HasFusionedItem() || stoneCount >= targetItem.GetSockets(true))
        {
            // Find all free slots in the secondary weapon
            AuditLogger.Log(player, "Manastone socket overload");
            return false;
        }

        // Start value of success
        float successChance = Rates.Get(player, RatesConfig.MANASTONE_CHANCES);

        if (manastone.GetItemTemplate().GetItemQuality().GetQualityId() >= ItemQuality.RARE.GetQualityId())
            successChance *= 0.8f;

        // Next socket difficulty modifier
        float socketDiff = stoneCount * 1.25f + 1.75f;

        // Level difference
        successChance += (slotLevel - stoneLevel) / socketDiff;

        // The supplement item is used
        if (supplementItem != null)
        {
            int supplementUseCount = 0;
            ItemTemplate manastoneTemplate = manastone.GetItemTemplate();

            int manastoneCount;
            // Not fusioned
            if (fusionedWeaponLevel == 1)
                manastoneCount = targetItem.GetItemStones().Count + 1;
            // Fusioned
            else
                manastoneCount = targetItem.GetFusionStones().Count + 1;

            // Additional success rate for the supplement
            ItemTemplate supplementTemplate = supplementItem.GetItemTemplate();

            bool isManastoneOnly = false;
            EnchantItemAction action = manastoneTemplate.GetActions().GetEnchantAction();
            if (action != null)
                supplementUseCount = action.GetCount();

            action = supplementTemplate.GetActions().GetEnchantAction();
            if (action != null)
            {
                // Add successRate
                successChance += action.GetChance();
                isManastoneOnly = action.IsManastoneOnly();
            }

            if (isManastoneOnly)
                supplementUseCount = 1;
            else if (stoneCount > 0)
                supplementUseCount = supplementUseCount * manastoneCount;

            if (player.GetInventory().GetItemCountByItemId(supplementTemplate.GetTemplateId()) < supplementUseCount)
                return false;

            // Put up supplements to wait for update
            player.SubtractSupplements(supplementUseCount, supplementTemplate.GetTemplateId());
        }

        bool result = Rnd.Chance() < successChance;

        // For test purpose. To use by administrator
        if (player.HasAccess(AdminConfig.ENCHANT_INFO))
            PacketSendUtility.SendMessage(player, (result ? "Success" : "Fail") + " (success chance:" + successChance + "%)");

        return result;
    }

    public static bool SocketManastoneAct(Player player, Item parentItem, Item targetItem, Item supplementItem, int targetWeapon, bool result)
    {
        // Decrease required supplements
        player.UpdateSupplements();
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return false;
        if (result)
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_SUCCEED(targetItem.GetL10n()));

            ManaStone manaStone = Aion.GameServer.Services.Items.ItemSocketService.AddManaStone(targetItem, parentItem.GetItemTemplate().GetTemplateId(), targetWeapon != 1);
            if (targetItem.IsEquipped())
            {
                ItemEquipmentListener.AddStoneStats(targetItem, manaStone, player.GetGameStats());
                player.GetGameStats().UpdateStatsAndSpeedVisually();
            }
        }
        else
        {
            targetItem.RemoveRemainingTuningCountIfPossible();
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_FAILED(targetItem.GetL10n()));
        }

        ItemPacketService.UpdateItemAfterInfoChange(player, targetItem, ItemPacketService.ItemUpdateType.STATS_CHANGE);
        return true;
    }

    public static int GetEquipBuff(Item item)
    {
        int[] skills = item.GetItemTemplate().GetExceedEnchantSkill() switch
        {
            ExceedEnchantSkillSetType.RANK1_SET1_MAGICAL_GLOVES => new int[] { 13042, 13046, 13055 },
            ExceedEnchantSkillSetType.RANK1_SET1_MAGICAL_PANTS => new int[] { 13071, 13075, 13078 },
            ExceedEnchantSkillSetType.RANK1_SET1_MAGICAL_SHOES => new int[] { 13108, 13118, 13121 },
            ExceedEnchantSkillSetType.RANK1_SET1_MAGICAL_SHOULDER => new int[] { 13104, 13097, 13098 },
            ExceedEnchantSkillSetType.RANK1_SET1_MAGICAL_TORSO => new int[] { 13128, 13132, 13144 },
            ExceedEnchantSkillSetType.RANK1_SET1_MAGICAL_WEAPON => new int[] { 13011, 13012, 13027 },
            ExceedEnchantSkillSetType.RANK1_SET1_PHYSICAL_GLOVES => new int[] { 13042, 13046, 13055 },
            ExceedEnchantSkillSetType.RANK1_SET1_PHYSICAL_PANTS => new int[] { 13071, 13075, 13078 },
            ExceedEnchantSkillSetType.RANK1_SET1_PHYSICAL_SHOES => new int[] { 13108, 13118, 13121 },
            ExceedEnchantSkillSetType.RANK1_SET1_PHYSICAL_SHOULDER => new int[] { 13104, 13097, 13098 },
            ExceedEnchantSkillSetType.RANK1_SET1_PHYSICAL_TORSO => new int[] { 13128, 13132, 13144 },
            ExceedEnchantSkillSetType.RANK1_SET1_PHYSICAL_WEAPON => new int[] { 13011, 13012, 13027 },
            ExceedEnchantSkillSetType.RANK1_SET2_MAGICAL_GLOVES => new int[] { 13046, 13058, 13056 },
            ExceedEnchantSkillSetType.RANK1_SET2_MAGICAL_PANTS => new int[] { 13075, 13061, 13067 },
            ExceedEnchantSkillSetType.RANK1_SET2_MAGICAL_SHOES => new int[] { 13121, 13114, 13119 },
            ExceedEnchantSkillSetType.RANK1_SET2_MAGICAL_SHOULDER => new int[] { 13104, 13094, 13192 },
            ExceedEnchantSkillSetType.RANK1_SET2_MAGICAL_TORSO => new int[] { 13144, 13135, 13133 },
            ExceedEnchantSkillSetType.RANK1_SET2_MAGICAL_WEAPON => new int[] { 13029, 13003, 13023 },
            ExceedEnchantSkillSetType.RANK1_SET2_PHYSICAL_GLOVES => new int[] { 13046, 13058, 13056 },
            ExceedEnchantSkillSetType.RANK1_SET2_PHYSICAL_PANTS => new int[] { 13075, 13064, 13069 },
            ExceedEnchantSkillSetType.RANK1_SET2_PHYSICAL_SHOES => new int[] { 13121, 13114, 13119 },
            ExceedEnchantSkillSetType.RANK1_SET2_PHYSICAL_SHOULDER => new int[] { 13104, 13094, 13192 },
            ExceedEnchantSkillSetType.RANK1_SET2_PHYSICAL_TORSO => new int[] { 13144, 13135, 13133 },
            ExceedEnchantSkillSetType.RANK1_SET2_PHYSICAL_WEAPON => new int[] { 13029, 13006, 13023 },
            ExceedEnchantSkillSetType.RANK1_SET3_MAGICAL_WEAPON => new int[] { 13031, 13022, 13026 },
            ExceedEnchantSkillSetType.RANK1_SET3_PHYSICAL_WEAPON => new int[] { 13031, 13022, 13026 },
            ExceedEnchantSkillSetType.RANK2_SET1_MAGICAL_GLOVES => new int[] { 13050, 13047, 13057 },
            ExceedEnchantSkillSetType.RANK2_SET1_MAGICAL_PANTS => new int[] { 13072, 13075, 13068 },
            ExceedEnchantSkillSetType.RANK2_SET1_MAGICAL_SHOES => new int[] { 13125, 13122, 13120 },
            ExceedEnchantSkillSetType.RANK2_SET1_MAGICAL_SHOULDER => new int[] { 13088, 13105, 13193 },
            ExceedEnchantSkillSetType.RANK2_SET1_MAGICAL_TORSO => new int[] { 13139, 13145, 13134 },
            ExceedEnchantSkillSetType.RANK2_SET1_MAGICAL_WEAPON => new int[] { 13008, 13010, 13024 },
            ExceedEnchantSkillSetType.RANK2_SET1_PHYSICAL_GLOVES => new int[] { 13050, 13047, 13057 },
            ExceedEnchantSkillSetType.RANK2_SET1_PHYSICAL_PANTS => new int[] { 13072, 13075, 13070 },
            ExceedEnchantSkillSetType.RANK2_SET1_PHYSICAL_SHOES => new int[] { 13125, 13122, 13120 },
            ExceedEnchantSkillSetType.RANK2_SET1_PHYSICAL_SHOULDER => new int[] { 13091, 13105, 13193 },
            ExceedEnchantSkillSetType.RANK2_SET1_PHYSICAL_TORSO => new int[] { 13139, 13145, 13134 },
            ExceedEnchantSkillSetType.RANK2_SET2_MAGICAL_WEAPON => new int[] { 13010, 13032, 13004 },
            ExceedEnchantSkillSetType.RANK2_SET2_PHYSICAL_GLOVES => new int[] { 13050, 13043, 13059 },
            ExceedEnchantSkillSetType.RANK2_SET2_PHYSICAL_PANTS => new int[] { 13072, 13078, 13065 },
            ExceedEnchantSkillSetType.RANK2_SET2_PHYSICAL_SHOES => new int[] { 13125, 13109, 13115 },
            ExceedEnchantSkillSetType.RANK2_SET2_PHYSICAL_SHOULDER => new int[] { 13091, 13099, 13095 },
            ExceedEnchantSkillSetType.RANK2_SET2_PHYSICAL_TORSO => new int[] { 13139, 13129, 13136 },
            ExceedEnchantSkillSetType.RANK2_SET2_PHYSICAL_WEAPON => new int[] { 13010, 13032, 13007 },
            ExceedEnchantSkillSetType.RANK2_SET1_PHYSICAL_WEAPON => new int[] { 13008, 13010, 13024 },
            ExceedEnchantSkillSetType.RANK2_SET3_MAGICAL_WEAPON => new int[] { 13008, 13013, 13030 },
            ExceedEnchantSkillSetType.RANK2_SET3_PHYSICAL_WEAPON => new int[] { 13008, 13013, 13030 },
            ExceedEnchantSkillSetType.RANK3_SET1_MAGICAL_GLOVES => new int[] { 13038, 13051, 13060 },
            ExceedEnchantSkillSetType.RANK3_SET1_MAGICAL_PANTS => new int[] { 13080, 13073, 13063 },
            ExceedEnchantSkillSetType.RANK3_SET1_MAGICAL_SHOES => new int[] { 13112, 13126, 13116 },
            ExceedEnchantSkillSetType.RANK3_SET1_MAGICAL_SHOULDER => new int[] { 13082, 13089, 13096 },
            ExceedEnchantSkillSetType.RANK3_SET1_MAGICAL_TORSO => new int[] { 13142, 13140, 13137 },
            ExceedEnchantSkillSetType.RANK3_SET1_MAGICAL_WEAPON => new int[] { 13034, 13018, 13009 },
            ExceedEnchantSkillSetType.RANK3_SET1_PHYSICAL_GLOVES => new int[] { 13040, 13051, 13060 },
            ExceedEnchantSkillSetType.RANK3_SET1_PHYSICAL_PANTS => new int[] { 13080, 13073, 13066 },
            ExceedEnchantSkillSetType.RANK3_SET1_PHYSICAL_SHOES => new int[] { 13112, 13126, 13116 },
            ExceedEnchantSkillSetType.RANK3_SET1_PHYSICAL_SHOULDER => new int[] { 13084, 13092, 13096 },
            ExceedEnchantSkillSetType.RANK3_SET1_PHYSICAL_TORSO => new int[] { 13142, 13140, 13137 },
            ExceedEnchantSkillSetType.RANK3_SET1_PHYSICAL_WEAPON => new int[] { 13036, 13020, 13009 },
            ExceedEnchantSkillSetType.RANK3_SET2_MAGICAL_GLOVES => new int[] { 13038, 13048, 13044 },
            ExceedEnchantSkillSetType.RANK3_SET2_MAGICAL_PANTS => new int[] { 13080, 13076, 13079 },
            ExceedEnchantSkillSetType.RANK3_SET2_MAGICAL_SHOES => new int[] { 13112, 13123, 13110 },
            ExceedEnchantSkillSetType.RANK3_SET2_MAGICAL_SHOULDER => new int[] { 13082, 13106, 13100 },
            ExceedEnchantSkillSetType.RANK3_SET2_MAGICAL_TORSO => new int[] { 13142, 13146, 13130 },
            ExceedEnchantSkillSetType.RANK3_SET2_MAGICAL_WEAPON => new int[] { 13034, 13014, 13025 },
            ExceedEnchantSkillSetType.RANK3_SET2_PHYSICAL_GLOVES => new int[] { 13040, 13048, 13044 },
            ExceedEnchantSkillSetType.RANK3_SET2_PHYSICAL_PANTS => new int[] { 13080, 13076, 13079 },
            ExceedEnchantSkillSetType.RANK3_SET2_PHYSICAL_SHOES => new int[] { 13112, 13123, 13110 },
            ExceedEnchantSkillSetType.RANK3_SET2_PHYSICAL_SHOULDER => new int[] { 13084, 13106, 13100 },
            ExceedEnchantSkillSetType.RANK3_SET2_PHYSICAL_TORSO => new int[] { 13142, 13146, 13130 },
            ExceedEnchantSkillSetType.RANK3_SET2_PHYSICAL_WEAPON => new int[] { 13036, 13014, 13025 },
            ExceedEnchantSkillSetType.RANK3_SET3_MAGICAL_WEAPON => new int[] { 13018, 13014, 13033 },
            ExceedEnchantSkillSetType.RANK3_SET3_PHYSICAL_WEAPON => new int[] { 13020, 13014, 13033 },
            ExceedEnchantSkillSetType.RANK2_SET2_MAGICAL_GLOVES => new int[] { 13050, 13043, 13059 },
            ExceedEnchantSkillSetType.RANK2_SET2_MAGICAL_PANTS => new int[] { 13072, 13078, 13062 },
            ExceedEnchantSkillSetType.RANK2_SET2_MAGICAL_SHOES => new int[] { 13125, 13109, 13115 },
            ExceedEnchantSkillSetType.RANK2_SET2_MAGICAL_SHOULDER => new int[] { 13088, 13099, 13095 },
            ExceedEnchantSkillSetType.RANK2_SET2_MAGICAL_TORSO => new int[] { 13139, 13129, 13136 },
            ExceedEnchantSkillSetType.RANK4_SET1_MAGICAL_GLOVES => new int[] { 13053, 13039, 13045 },
            ExceedEnchantSkillSetType.RANK4_SET1_MAGICAL_PANTS => new int[] { 13077, 13081, 13079 },
            ExceedEnchantSkillSetType.RANK4_SET1_MAGICAL_SHOES => new int[] { 13117, 13113, 13111 },
            ExceedEnchantSkillSetType.RANK4_SET1_MAGICAL_SHOULDER => new int[] { 13086, 13083, 13101 },
            ExceedEnchantSkillSetType.RANK4_SET1_MAGICAL_TORSO => new int[] { 13138, 13143, 13131 },
            ExceedEnchantSkillSetType.RANK4_SET1_MAGICAL_WEAPON => new int[] { 13015, 13028, 13017 },
            ExceedEnchantSkillSetType.RANK4_SET1_PHYSICAL_GLOVES => new int[] { 13054, 13041, 13045 },
            ExceedEnchantSkillSetType.RANK4_SET1_PHYSICAL_PANTS => new int[] { 13077, 13081, 13079 },
            ExceedEnchantSkillSetType.RANK4_SET1_PHYSICAL_SHOES => new int[] { 13117, 13113, 13111 },
            ExceedEnchantSkillSetType.RANK4_SET1_PHYSICAL_SHOULDER => new int[] { 13087, 13085, 13101 },
            ExceedEnchantSkillSetType.RANK4_SET1_PHYSICAL_TORSO => new int[] { 13138, 13143, 13131 },
            ExceedEnchantSkillSetType.RANK4_SET1_PHYSICAL_WEAPON => new int[] { 13016, 13028, 13017 },
            ExceedEnchantSkillSetType.RANK4_SET2_MAGICAL_GLOVES => new int[] { 13053, 13052, 13049 },
            ExceedEnchantSkillSetType.RANK4_SET2_MAGICAL_PANTS => new int[] { 13077, 13074, 13076 },
            ExceedEnchantSkillSetType.RANK4_SET2_MAGICAL_SHOES => new int[] { 13117, 13127, 13124 },
            ExceedEnchantSkillSetType.RANK4_SET2_MAGICAL_SHOULDER => new int[] { 13086, 13090, 13107 },
            ExceedEnchantSkillSetType.RANK4_SET2_MAGICAL_TORSO => new int[] { 13138, 13141, 13147 },
            ExceedEnchantSkillSetType.RANK4_SET2_MAGICAL_WEAPON => new int[] { 13019, 13017, 13005 },
            ExceedEnchantSkillSetType.RANK4_SET2_PHYSICAL_GLOVES => new int[] { 13054, 13052, 13049 },
            ExceedEnchantSkillSetType.RANK4_SET2_PHYSICAL_PANTS => new int[] { 13077, 13074, 13076 },
            ExceedEnchantSkillSetType.RANK4_SET2_PHYSICAL_SHOES => new int[] { 13117, 13127, 13124 },
            ExceedEnchantSkillSetType.RANK4_SET2_PHYSICAL_SHOULDER => new int[] { 13087, 13093, 13107 },
            ExceedEnchantSkillSetType.RANK4_SET2_PHYSICAL_TORSO => new int[] { 13138, 13141, 13147 },
            ExceedEnchantSkillSetType.RANK4_SET2_PHYSICAL_WEAPON => new int[] { 13021, 13017, 13005 },
            ExceedEnchantSkillSetType.RANK4_SET3_MAGICAL_WEAPON => new int[] { 13035, 13001, 13005 },
            ExceedEnchantSkillSetType.RANK4_SET3_PHYSICAL_WEAPON => new int[] { 13037, 13001, 13005 },
            ExceedEnchantSkillSetType.RANK5_SET1_MAGICAL_TORSO => new int[] { 13235, 13236, 13238 },
            ExceedEnchantSkillSetType.RANK5_SET1_MAGICAL_GLOVES => new int[] { 13248, 13253, 13251 },
            ExceedEnchantSkillSetType.RANK5_SET1_MAGICAL_PANTS => new int[] { 13241, 13079, 13240 },
            ExceedEnchantSkillSetType.RANK5_SET1_MAGICAL_SHOULDER => new int[] { 13269, 13279, 13247 },
            ExceedEnchantSkillSetType.RANK5_SET1_MAGICAL_SHOES => new int[] { 13245, 13266, 13246 },
            ExceedEnchantSkillSetType.RANK5_SET1_PHYSICAL_TORSO => new int[] { 13235, 13236, 13238 },
            ExceedEnchantSkillSetType.RANK5_SET1_PHYSICAL_GLOVES => new int[] { 13251, 13249, 13248 },
            ExceedEnchantSkillSetType.RANK5_SET1_PHYSICAL_SHOULDER => new int[] { 13270, 13247, 13269 },
            ExceedEnchantSkillSetType.RANK5_SET1_PHYSICAL_PANTS => new int[] { 13241, 13079, 13240 },
            ExceedEnchantSkillSetType.RANK5_SET1_PHYSICAL_SHOES => new int[] { 13245, 13244, 13266 },
            ExceedEnchantSkillSetType.RANK5_SET1_MAGICAL_WEAPON => new int[] { 13228, 13234, 13231 },
            ExceedEnchantSkillSetType.RANK5_SET1_PHYSICAL_WEAPON => new int[] { 13229, 13234, 13231 },
            _ => new int[] { 0 },
        };
        return Rnd.Get(skills);
    }

    public static void AmplifyItem(Player player, int targetItemObjId, int materialId, int toolId)
    {
        if (player == null)
            return;
        Item targetItem = player.GetEquipment().GetEquippedItemByObjId(targetItemObjId);

        if (targetItem == null)
            targetItem = player.GetInventory().GetItemByObjId(targetItemObjId);

        Item material = player.GetInventory().GetItemByObjId(materialId);
        Item tool = player.GetInventory().GetItemByObjId(toolId);

        if (targetItem == null || material == null || tool == null)
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_NO_TARGET_ITEM());
            return;
        }
        if (targetItem.IsAmplified())
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_ALREADY());
            return;
        }
        if (!targetItem.GetItemTemplate().CanExceedEnchant())
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_CANNOT_01(targetItem.GetL10n()));
            return;
        }
        if (targetItem.GetEnchantLevel() < targetItem.GetMaxEnchantLevel())
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_CANNOT_02());
            return;
        }
        if (targetItem.GetItemId() != material.GetItemId() && material.GetItemId() != 166500002 && material.GetItemId() != 166500005)
        {
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_NO_TARGET_ITEM());
            return;
        }
        if (player.GetInventory().DecreaseByObjectId(material.GetObjectId(), 1) && player.GetInventory().DecreaseByObjectId(tool.GetObjectId(), 1))
        {
            targetItem.SetAmplified(true);
            PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_SUCCEED(targetItem.GetL10n()));
            ItemPacketService.UpdateItemAfterInfoChange(player, targetItem);

            if (targetItem.IsEquipped())
                player.GetEquipment().SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED);
            else
                player.GetInventory().SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED);
        }
    }
}
