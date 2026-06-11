using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Itemset;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Stats.Listeners;

/// <summary>Java parity: model/stats/listeners/ItemEquipmentListener (xavier, Wakizashi). Static util. Consumer+varargs→Action+params; CreatureGameStats&lt;?&gt; wildcard→non-generic CreatureGameStats base; Set&lt;? extends ManaStone&gt;→ISet&lt;ManaStone&gt;; Collections.emptyList→new List; switch-with-continue preserved; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. NOTE: cgs.AddEffect param is List&lt;IStatFunction&gt; in C# base vs Java List&lt;? extends IStatFunction&gt; — List&lt;StatFunction&gt; passed directly (red-tolerated variance). Most item/stat/service deps red-tolerated.</summary>
public class ItemEquipmentListener
{
    public static void OnItemEquipment(Item item, Player owner)
    {
        owner.GetController().CancelUseItem();
        ItemTemplate itemTemplate = item.GetItemTemplate();

        AddWeaponStats(item, owner.GetGameStats());

        if (itemTemplate.IsItemSet())
            RecalculateItemSet(itemTemplate.GetItemSet(), owner);
        if (item.HasManaStones())
            AddStonesStats(item, item.GetItemStones(), owner.GetGameStats());
        if (item.HasFusionStones())
            AddStonesStats(item, item.GetFusionStones(), owner.GetGameStats());

        IdianStone idianStone = item.GetIdianStone();
        if (idianStone != null)
            idianStone.OnEquip(owner, item.GetEquipmentSlot());

        if (item.GetBuffSkill() != 0)
        {
            SkillTemplate buffSkill = DataManager.SKILL_DATA.GetSkillTemplate(item.GetBuffSkill());
            SkillLearnService.LearnTemporarySkill(owner, item.GetBuffSkill(), 1);
            long currTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long oldCooldown = owner.GetSkillCoolDown(buffSkill.GetCooldownId());
            long newCooldown;
            if (oldCooldown - currTime > 15000) // cd active
                newCooldown = oldCooldown;
            else
                newCooldown = currTime + 15000;
            owner.SetSkillCoolDown(buffSkill.GetCooldownId(), newCooldown);
            PacketSendUtility.SendPacket(owner, new SM_SKILL_COOLDOWN(buffSkill.GetSkillId(), newCooldown));
        }
        ForEachBonusStats(bonusStats => bonusStats.ApplyEffect(owner), item.GetBonusStatsEffect(), item.GetFusionedItemBonusStatsEffect());
        if (item.GetConditioningInfo() != null)
        {
            owner.GetObserveController().AddObserver(item.GetConditioningInfo());
            item.GetConditioningInfo().SetPlayer(owner);
        }
        if (item.GetEnchantLevel() > 0)
            EnchantService.ApplyEnchantEffect(item, owner, item.GetEnchantLevel());
        if (item.GetTempering() > 0)
            TemperingEffect.Apply(owner, item);
        owner.GetGameStats().UpdateArmorMasteryStats(owner.GetEquipment().GetEquippedItems());
    }

    private static void ForEachBonusStats(Action<RandomBonusEffect> action, params RandomBonusEffect[] bonusStatsEffects)
    {
        foreach (RandomBonusEffect bonusStats in bonusStatsEffects)
            if (bonusStats != null)
                action(bonusStats);
    }

    public static void OnItemUnequipment(Item item, Player owner)
    {
        owner.GetController().CancelUseItem();

        ItemTemplate itemTemplate = item.GetItemTemplate();
        // Check if belongs to ItemSet
        if (itemTemplate.IsItemSet())
            RecalculateItemSet(itemTemplate.GetItemSet(), owner);

        owner.GetGameStats().EndEffect(item);

        if (item.HasManaStones())
            RemoveStoneStats(item.GetItemStones(), owner.GetGameStats());

        if (item.HasFusionStones())
            RemoveStoneStats(item.GetFusionStones(), owner.GetGameStats());

        if (item.GetConditioningInfo() != null)
        {
            owner.GetObserveController().RemoveObserver(item.GetConditioningInfo());
            item.GetConditioningInfo().SetPlayer(null);
        }
        IdianStone idianStone = item.GetIdianStone();
        if (idianStone != null)
            idianStone.OnUnEquip(owner);
        ForEachBonusStats(bonusStats => bonusStats.EndEffect(owner), item.GetBonusStatsEffect(), item.GetFusionedItemBonusStatsEffect());
        if (item.GetEnchantEffect() != null)
        {
            item.GetEnchantEffect().EndEffect(owner);
            item.SetEnchantEffect(null);
        }
        if (item.GetTemperingEffect() != null)
        {
            item.GetTemperingEffect().EndEffect(owner);
            item.SetTemperingEffect(null);
        }
        if (item.GetBuffSkill() != 0)
            SkillLearnService.RemoveSkill(owner, item.GetBuffSkill());
        owner.GetGameStats().UpdateArmorMasteryStats(owner.GetEquipment().GetEquippedItems());
    }

    private static void AddWeaponStats(Item item, CreatureGameStats cgs)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();
        List<StatFunction> mainWeaponModifiers = itemTemplate.GetModifiers();
        if (mainWeaponModifiers == null)
            mainWeaponModifiers = new List<StatFunction>();

        List<StatFunction> modifiersToApply;
        if ((item.GetEquipmentSlot() & ItemSlot.MAIN_OR_SUB.GetSlotIdMask()) != 0)
        {
            modifiersToApply = ExtractApplicableWeaponModifiers(item, mainWeaponModifiers);
            if (item.HasFusionedItem())
            {
                // add all bonus modifiers according to rules
                ItemTemplate fusionedItemTemplate = item.GetFusionedItemTemplate();
                ItemGroup weaponType = fusionedItemTemplate.GetItemGroup();
                List<StatFunction> fusionedItemModifiers = fusionedItemTemplate.GetModifiers();
                if (fusionedItemModifiers != null)
                    modifiersToApply.AddRange(ExtractApplicableWeaponModifiers(item, fusionedItemModifiers));

                // add 10% of Magic Boost and Attack
                WeaponStats weaponStats = fusionedItemTemplate.GetWeaponStats();
                if (weaponStats != null)
                {
                    int boostMagicalSkill = (int)(0.1f * weaponStats.GetBoostMagicalSkill());
                    int attack = (int)(0.1f * weaponStats.GetMeanDamage());
                    if (weaponType == ItemGroup.ORB || weaponType == ItemGroup.STAFF || weaponType == ItemGroup.SPELLBOOK || weaponType == ItemGroup.GUN
                        || weaponType == ItemGroup.CANNON || weaponType == ItemGroup.HARP || weaponType == ItemGroup.KEYBLADE)
                    {
                        modifiersToApply.Add(new StatAddFunction(StatEnum.BOOST_MAGICAL_SKILL, boostMagicalSkill, false));
                    }
                    modifiersToApply.Add(new StatAddFunction(
                        item.GetItemTemplate().GetAttackType().IsMagical() ? StatEnum.MAGICAL_ATTACK : StatEnum.PHYSICAL_ATTACK, attack, false));
                }
            }
        }
        else
        {
            modifiersToApply = mainWeaponModifiers;
        }
        item.SetCurrentModifiers(modifiersToApply);
        cgs.AddEffect(item, modifiersToApply);
    }

    private static List<StatFunction> ExtractApplicableWeaponModifiers(Item item, List<StatFunction> modifiers)
    {
        List<StatFunction> allModifiers = new();
        foreach (StatFunction modifier in modifiers)
        {
            switch (modifier.GetName())
            {
                case StatEnum.ATTACK_SPEED:
                case StatEnum.PVP_ATTACK_RATIO:
                case StatEnum.BOOST_CASTING_TIME:
                    continue;
                default:
                    allModifiers.Add(modifier);
                    break;
            }
        }
        return allModifiers;
    }

    private static void RecalculateItemSet(ItemSetTemplate itemSetTemplate, Player player)
    {
        if (itemSetTemplate == null)
            return;

        // TODO quite
        player.GetGameStats().EndEffect(itemSetTemplate);
        // 1.- Check equipment for items already equip with this itemSetTemplate id
        int itemSetPartsEquipped = player.GetEquipment().ItemSetPartsEquipped(itemSetTemplate.GetId());

        // 2.- Check Item Set Parts and add effects one by one if not done already
        foreach (PartBonus itempartbonus in itemSetTemplate.GetPartbonus())
            if (itempartbonus.GetCount() <= itemSetPartsEquipped)
                player.GetGameStats().AddEffect(itemSetTemplate, itempartbonus.GetModifiers());

        // 3.- Finally check if all items are applied and set the full bonus if not already applied
        FullBonus fullbonus = itemSetTemplate.GetFullbonus();
        if (fullbonus != null && itemSetPartsEquipped == fullbonus.GetCount())
        {
            // Add the full bonus with index = total parts + 1 to avoid confusion with part bonus equal to number of
            // objects
            player.GetGameStats().AddEffect(itemSetTemplate, fullbonus.GetModifiers());
        }
    }

    private static void AddStonesStats(Item item, ISet<ManaStone> itemStones, CreatureGameStats cgs)
    {
        if (itemStones == null || itemStones.Count == 0)
            return;
        foreach (ManaStone stone in itemStones)
            AddStoneStats(item, stone, cgs);
    }

    public static void AddStoneStats(Item item, ManaStone stone, CreatureGameStats cgs)
    {
        if (stone == null || stone.GetModifiers() == null)
            return;
        cgs.AddEffect(stone, stone.GetModifiers());
    }

    public static void RemoveStoneStats(ISet<ManaStone> itemStones, CreatureGameStats cgs)
    {
        if (itemStones == null || itemStones.Count == 0)
            return;
        foreach (ManaStone stone in itemStones)
        {
            List<StatFunction> modifiers = stone.GetModifiers();
            if (modifiers != null)
                cgs.EndEffect(stone);
        }
    }
}
