using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using SM_SYSTEM_MESSAGE = Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE;
using SM_INVENTORY_UPDATE_ITEM = Aion.GameServer.Network.Aion.ServerPackets.SM_INVENTORY_UPDATE_ITEM;
using SM_ITEM_USAGE_ANIMATION = Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION;
using Stigma = Aion.GameServer.Model.Templates.Items.Stigma;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/StigmaService (ATracer, cura, Neon).</summary>
public class StigmaService
{
    public static bool NotifyEquipAction(Player player, Item resultItem, long slot)
    {
        if (resultItem.GetItemTemplate().IsStigma())
        {
            Stigma stigmaInfo = resultItem.GetItemTemplate().GetStigma();
            int stigmaLevel = resultItem.GetEnchantLevel();
            string stigmaName = resultItem.GetItemName().Replace(" ", "").ToUpperInvariant();
            bool replace = false;
            foreach (Item i in player.GetEquipment().GetEquippedItemsAllStigma())
            {
                if (i.GetEquipmentSlot() == slot)
                {
                    if (!stigmaName.Equals(i.GetItemName().Replace(" ", "").ToUpperInvariant(), StringComparison.Ordinal))
                        return false;
                    RemoveStigmaSkills(player, i.GetItemTemplate().GetStigma(), i.GetEnchantLevel(), i.GetEnchantLevel() > resultItem.GetEnchantLevel());
                    replace = true;
                    break;
                }
            }
            if (!replace)
            {
                // check the number of stigma wearing
                if (ItemSlotExtensions.IsRegularStigma(slot) && GetPossibleStigmaCount(player) <= player.GetEquipment().GetEquippedItemsRegularStigma().Count)
                {
                    AuditLogger.Log(player, "tried to equip stigma, exceeding the socket limit");
                    return false;
                }
                else if (ItemSlotExtensions.IsAdvancedStigma(slot)
                    && GetPossibleAdvancedStigmaCount(player) <= player.GetEquipment().GetEquippedItemsAdvancedStigma().Count)
                {
                    AuditLogger.Log(player, "tried to equip advanced stigma, exceeding the socket limit");
                    return false;
                }
            }

            long kinahcount = 25000;
            // Sets the price for equipping stigma during mission in Space of Destiny [ID: 320070000] and Sliver of darkness [ID: 310070000]
            if ((player.GetRace() == Race.ASMODIANS && player.GetWorldId() == 320070000)
                || (player.GetRace() == Race.ELYOS && player.GetWorldId() == 310070000))
                kinahcount = 1000;
            else if (resultItem.GetItemTemplate().GetItemQuality() == ItemQuality.LEGEND)
                kinahcount = 50000;
            else if (resultItem.GetItemTemplate().GetItemQuality() == ItemQuality.UNIQUE)
                kinahcount = 100000;

            if (!player.GetInventory().TryDecreaseKinah(Aion.GameServer.Services.Trade.PricesService.GetPriceForService(kinahcount, player.GetRace())))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_STIGMA_NOT_ENOUGH_MONEY());
                return false;
            }
            AddStigmaSkills(player, stigmaInfo, stigmaLevel);
        }
        return true;
    }

    public static void OnPlayerLogin(Player player)
    {
        if (player.HasPermission(MembershipConfig.STIGMA_AUTOLEARN))
        {
            for (int level = 20; level <= player.GetLevel(); level++)
            {
                foreach (SkillLearnTemplate template in DataManager.SKILL_TREE_DATA.GetTemplatesFor(player.GetPlayerClass(), level, player.GetRace()))
                {
                    if (template.IsStigma())
                        SkillLearnService.LearnTemporarySkill(player, template.GetSkillId(), template.GetSkillLevel());
                }
            }
            return;
        }

        foreach (Item item in player.GetEquipment().GetEquippedItemsAllStigma())
        {
            if (!item.GetItemTemplate().IsStigma())
            {
                player.GetEquipment().UnEquipItem(item.GetObjectId(), false);
                continue;
            }

            if (!IsPossibleEquippedStigma(player, item))
            {
                player.GetEquipment().UnEquipItem(item.GetObjectId(), false);
                AuditLogger.Log(player, "had more equipped stigmas on login than allowed");
                continue;
            }

            if (!item.GetItemTemplate().IsClassSpecific(player.GetPlayerClass()))
            {
                player.GetEquipment().UnEquipItem(item.GetObjectId(), false);
                AuditLogger.Log(player, "had an equipped stigma on login which was not for his class");
                continue;
            }

            // check for double stigmas equipped into the same slot
            bool doubleSlot = false;
            foreach (Item checkStigma in player.GetEquipment().GetEquippedItemsAllStigma())
            {
                if (checkStigma.GetEquipmentSlot() == item.GetEquipmentSlot() && checkStigma.GetItemId() != item.GetItemId())
                {
                    player.GetEquipment().UnEquipItem(item.GetObjectId(), false);
                    AuditLogger.Log(player, "had two stigmas equipped in the same slot on login");
                    doubleSlot = true;
                    break;
                }
            }
            if (doubleSlot)
                continue;

            AddStigmaSkills(player, item.GetItemTemplate().GetStigma(), item.GetEnchantLevel());
        }

        AddLinkedStigmaSkills(player);
    }

    public static void RemoveLinkedStigmaSkills(Player player)
    {
        List<PlayerSkillEntry> linkedStigmaSkills = new List<PlayerSkillEntry>();
        while (true) // remove all linked stigma skills (can be more than one if stigma auto learning is enabled)
        {
            string stack = null;
            linkedStigmaSkills.Clear();
            foreach (PlayerSkillEntry skill in player.GetSkillList().GetAllSkills())
            {
                if (skill.IsLinkedStigmaSkill())
                {
                    SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId());
                    if (stack == null)
                        stack = skillTemplate.GetStack();
                    if (string.Equals(skillTemplate.GetStack(), stack, StringComparison.OrdinalIgnoreCase))
                        linkedStigmaSkills.Add(skill);
                    if (string.Equals(stack, "NONE", StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }
            if (linkedStigmaSkills.Count == 0)
                break;
            string firstSkillL10n = null, secondSkillL10n = null;
            int skillLevel = 0;
            for (int i = 0; i < linkedStigmaSkills.Count; i++)
            {
                PlayerSkillEntry skillEntry = linkedStigmaSkills[i];
                SkillLearnService.RemoveSkill(player, skillEntry.GetSkillId());
                if (i == 0)
                {
                    firstSkillL10n = DataManager.SKILL_DATA.GetSkillTemplate(skillEntry.GetSkillId()).GetL10n();
                    skillLevel = skillEntry.GetSkillLevel();
                }
                else if (i == 1)
                {
                    secondSkillL10n = DataManager.SKILL_DATA.GetSkillTemplate(skillEntry.GetSkillId()).GetL10n();
                }
            }
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_STIGMA_DELETE_HIDDEN_SKILL(firstSkillL10n, skillLevel, secondSkillL10n));
        }
    }

    public static void AddLinkedStigmaSkills(Player player)
    {
        List<Item> stigmas = player.GetEquipment().GetEquippedItemsAllStigma();
        if (stigmas.Count < 6)
            return;

        foreach (Item stigma in stigmas)
        {
            if (!stigma.IsStigmaChargeable())
                return;
        }

        int skillId = GetLinkedStigmaLearnSkill(player);
        if (skillId > 0)
        {
            // linked stigma level is the lowest enchant level of all equipped stigmas
            int linkedStigmaSkillLevel = stigmas.Min(s => s.GetEnchantLevel()) + 1;
            foreach (SkillLearnTemplate skill in DataManager.SKILL_TREE_DATA.GetSkillsForSkill(skillId, player.GetPlayerClass(), player.GetRace(), player.GetLevel()))
                SkillLearnService.LearnTemporarySkill(player, skill.GetSkillId(), linkedStigmaSkillLevel);
        }
    }

    private static int GetLinkedStigmaLearnSkill(Player player)
    {
        // references: http://aion.mouseclic.com/beta/stigma.php
        bool isEly = player.GetRace() == Race.ELYOS;
        switch (player.GetPlayerClass())
        {
            case PlayerClass.GLADIATOR:
                if (IsEquipped(player, 140001118) && IsEquipped(player, 2, 140001103, 140001104, 140001105))
                    return 731; // Wind Lance
                if (IsEquipped(player, 140001119) && IsEquipped(player, 2, 140001106, 140001107, 140001108))
                    return 643; // Unraveling Assault
                return !isEly ? 661 : 662; // Battle Banner
            case PlayerClass.TEMPLAR:
                if (IsEquipped(player, 140001134) && IsEquipped(player, 2, 140001120, 140001122, 140001125))
                    return 2921; // Invigorating Strike
                if (IsEquipped(player, 140001135) && IsEquipped(player, 2, 140001121, 140001123, 140001124))
                    return 2918; // Shield of Vengeance
                return 2917; // Eternal Denial
            case PlayerClass.ASSASSIN:
                if (IsEquipped(player, 140001151) && IsEquipped(player, 2, 140001136, 140001137, 140001140))
                    return 3241; // Fangdrop Stab
                if (IsEquipped(player, 140001152) && IsEquipped(player, 2, 140001138, 140001139, 140001141))
                    return 3238; // Shimmerbomb
                return 3244; // Explosive Rebranding
            case PlayerClass.RANGER:
                if (IsEquipped(player, 140001172) && IsEquipped(player, 2, 140001153, 140001155, 140001157))
                    return 1008; // Ripthread Shot
                if (IsEquipped(player, 140001173) && IsEquipped(player, 2, 140001154, 140001156, 140001158))
                    return 938; // Night Haze
                return isEly ? 1065 : 1064; // Staggering Trap
            case PlayerClass.SORCERER:
                if (IsEquipped(player, 140001191) && IsEquipped(player, 2, 140001174, 140001178, 140001181))
                    return 1342; // Slumberswept Wind
                if (IsEquipped(player, 140001192) && IsEquipped(player, 2, 140001176, 140001177, isEly ? 140001184 : 140001185))
                    return 1542; // Aetherblaze
                return 1420; // Repulsion Field
            case PlayerClass.SPIRIT_MASTER:
                if (IsEquipped(player, 140001209) && IsEquipped(player, 2, 140001193, 140001194, 140001195))
                    return 3543; // Spirit's Empowerment
                if (IsEquipped(player, 140001210) && IsEquipped(player, 2, 140001196, isEly ? 140001197 : 140001198, 140001199))
                    return 3549; // Command: Absorb Wounds
                return 3851; // Blood Funnel
            case PlayerClass.CLERIC:
                if (IsEquipped(player, 140001245) && IsEquipped(player, 2, 140001228, 140001229, isEly ? 140001230 : 140001231))
                    return 4169; // Judge's Edict
                if (IsEquipped(player, 140001246) && IsEquipped(player, 2, 140001232, 140001233, isEly ? 140001234 : 140001235))
                    return 3934; // Restoration Relief
                return isEly ? 3906 : 3911; // Summon Vexing Energy
            case PlayerClass.CHANTER:
                if (IsEquipped(player, 140001226) && IsEquipped(player, 2, 140001211, 140001212, 140001213))
                    return 1909; // Word of Instigation
                if (IsEquipped(player, 140001227) && IsEquipped(player, 2, 140001214, 140001215, 140001216))
                    return 1903; // Resonant Strike
                return 1906; // Debilitating Incantation
            case PlayerClass.RIDER:
                if (IsEquipped(player, 140001279) && IsEquipped(player, 2, 140001264, 140001265, 140001269))
                    return 2858; // Explosive Exhaust
                if (IsEquipped(player, 140001280) && IsEquipped(player, 2, 140001266, 140001267, 140001268))
                    return 2863; // Powerspike Trigger
                return 2851; // Nerve Pulse
            case PlayerClass.GUNNER:
                if (IsEquipped(player, 140001262) && IsEquipped(player, 2, 140001247, 140001248, 140001249))
                    return 2370; // Pursuit Stance
                if (IsEquipped(player, 140001263) && IsEquipped(player, 2, 140001250, 140001251, 140001252))
                    return 2377; // Sequential Fire
                return 2382; // Pulverizer Cannon
            case PlayerClass.BARD:
                if (IsEquipped(player, 140001296) && IsEquipped(player, 2, 140001281, 140001282, 140001284))
                    return 4480; // Blazing Requiem
                if (IsEquipped(player, 140001297) && IsEquipped(player, 2, 140001283, 140001285, 140001286))
                    return 4483; // Purging Paean
                return 4566; // Delusional Dirge
        }
        return 0;
    }

    public static bool IsEquipped(Player player, int itemId)
    {
        if (player.GetEquipment().GetEquippedItemsByItemId(itemId) != null)
            return player.GetEquipment().GetEquippedItemsByItemId(itemId).Count > 0;
        return false;
    }

    public static bool IsEquipped(Player player, int neededCount, params int[] itemIds)
    {
        int equippedCount = 0;
        foreach (int itemId in itemIds)
            if (IsEquipped(player, itemId))
                equippedCount += 1;
        return equippedCount == neededCount;
    }

    private static int GetPossibleStigmaCount(Player player)
    {
        if (player.HasPermission(MembershipConfig.STIGMA_SLOT_QUEST))
            return 3;
        int playerLevel = player.GetLevel();
        bool isCompleteQuest = IsCompleteQuest(player);
        if (isCompleteQuest)
        {
            if (playerLevel < 30)
                return 1;
            else if (playerLevel < 40)
                return 2;
            else
                return 3;
        }
        return 0;
    }

    private static bool IsCompleteQuest(Player player)
    {
        // Stigma Quest Elyos: 1929, Asmodians: 2900
        bool isCompleteQuest = false;

        if (player.GetRace() == Race.ELYOS)
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(1929);
            if (qs != null)
                isCompleteQuest = player.IsCompleteQuest(1929) || (qs.GetStatus() == QuestStatus.START && qs.GetQuestVars().GetQuestVars() == 98);
            else
                isCompleteQuest = player.IsCompleteQuest(1929);
        }
        else
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(2900);
            if (qs != null)
                isCompleteQuest = player.IsCompleteQuest(2900) || (qs.GetStatus() == QuestStatus.START && qs.GetQuestVars().GetQuestVars() == 99);
            else
                isCompleteQuest = player.IsCompleteQuest(2900);
        }
        return isCompleteQuest;
    }

    private static int GetPossibleAdvancedStigmaCount(Player player)
    {
        if (player.HasPermission(MembershipConfig.STIGMA_SLOT_QUEST))
            return 3;
        int playerLevel = player.GetLevel();
        bool isCompleteQuest = IsCompleteQuest(player);
        if (isCompleteQuest)
        {
            if (playerLevel >= 55)
                return 3;
            else if (playerLevel >= 50)
                return 2;
            else if (playerLevel >= 45)
                return 1;
        }
        return 0;
    }

    private static bool IsPossibleEquippedStigma(Player player, Item item)
    {
        if (!item.GetItemTemplate().IsStigma())
            return false;

        long itemSlotToEquip = item.GetEquipmentSlot();

        // Stigma
        if (ItemSlotExtensions.IsRegularStigma(itemSlotToEquip))
        {
            int stigmaCount = GetPossibleStigmaCount(player);
            if (stigmaCount > 0)
            {
                if (stigmaCount == 1)
                {
                    if (itemSlotToEquip == ItemSlot.STIGMA1.GetSlotIdMask())
                        return true;
                }
                else if (stigmaCount == 2)
                {
                    if (itemSlotToEquip == ItemSlot.STIGMA1.GetSlotIdMask() || itemSlotToEquip == ItemSlot.STIGMA2.GetSlotIdMask())
                        return true;
                }
                else if (stigmaCount == 3)
                {
                    return true;
                }
            }
        }
        // Advanced Stigma
        else if (ItemSlotExtensions.IsAdvancedStigma(itemSlotToEquip))
        {
            int advStigmaCount = GetPossibleAdvancedStigmaCount(player);
            if (advStigmaCount > 0)
            {
                if (advStigmaCount == 1)
                {
                    if (itemSlotToEquip == ItemSlot.ADV_STIGMA1.GetSlotIdMask())
                        return true;
                }
                else if (advStigmaCount == 2)
                {
                    if (itemSlotToEquip == ItemSlot.ADV_STIGMA1.GetSlotIdMask() || itemSlotToEquip == ItemSlot.ADV_STIGMA2.GetSlotIdMask())
                        return true;
                }
                else if (advStigmaCount == 3)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static void ChargeStigma(Player player, Item stigma, Item chargeStone)
    {
        Stigma stigmaInfo = stigma.GetItemTemplate().GetStigma();
        if (stigma.GetItemId() != chargeStone.GetItemId() || chargeStone.GetEnchantLevel() > 0 || stigma.GetEnchantLevel() >= 10)
            return;
        if (!stigma.IsStigmaChargeable())
            return;

        bool isSuccess = Rnd.Chance() < Math.Max(25, 100 - (stigma.GetEnchantLevel() * 10));

        int parentItemId = stigma.GetItemId();
        int parentObjectId = stigma.GetObjectId();
        PacketSendUtility.BroadcastPacket(player,
            new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentObjectId, chargeStone.GetObjectId(), parentItemId, 5000, 0, 0), true);
        ItemUseObserver observer = new ChargeStigmaObserver(player, stigma, chargeStone, parentObjectId, parentItemId);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(TaskId.ITEM_USE, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            PacketSendUtility.BroadcastPacket(player,
                new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentObjectId, parentItemId, 0, isSuccess ? 1 : 2, 1), true);
            if (!player.GetInventory().DecreaseByObjectId(chargeStone.GetObjectId(), 1, ItemPacketService.ItemUpdateType.DEC_STIGMA_USE))
                return ValueTask.CompletedTask;
            if (!isSuccess)
            {
                if (stigma.IsEquipped())
                    player.GetEquipment().UnEquipItem(stigma.GetObjectId());
                player.GetInventory().DecreaseByObjectId(stigma.GetObjectId(), 1, ItemPacketService.ItemUpdateType.DEC_STIGMA_USE);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_STIGMA_ENCHANT_FAIL(stigma.GetL10n()));
            }
            else
            {
                stigma.SetEnchantLevel(stigma.GetEnchantLevel() + 1);
                if (stigma.IsEquipped())
                {
                    RemoveStigmaSkills(player, stigmaInfo, stigma.GetEnchantLevel() - 1, false);
                    AddStigmaSkills(player, stigmaInfo, stigma.GetEnchantLevel());
                }
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_STIGMA_ENCHANT_SUCCESS(stigma.GetL10n()));
                PacketSendUtility.SendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, stigma));
                if (stigma.GetPersistentState() != IPersistable.PersistentState.DELETED)
                {
                    stigma.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                    if (stigma.IsEquipped())
                        player.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                    else
                        player.GetInventory().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    // Java parity: anonymous ItemUseObserver in chargeStigma().
    private sealed class ChargeStigmaObserver : ItemUseObserver
    {
        private readonly Player player;
        private readonly Item stigma;
        private readonly Item chargeStone;
        private readonly int parentObjectId;
        private readonly int parentItemId;

        public ChargeStigmaObserver(Player player, Item stigma, Item chargeStone, int parentObjectId, int parentItemId)
        {
            this.player = player;
            this.stigma = stigma;
            this.chargeStone = chargeStone;
            this.parentObjectId = parentObjectId;
            this.parentItemId = parentItemId;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ITEM_USE);
            player.RemoveItemCoolDown(stigma.GetItemTemplate().GetUseLimits().GetDelayId());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            PacketSendUtility.BroadcastPacket(player,
                new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentObjectId, chargeStone.GetObjectId(), parentItemId, 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    private static void AddStigmaSkills(Player player, Stigma stigma, int stigmaLevel)
    {
        foreach (string skillGroup in stigma.GetGainSkillGroups())
            foreach (SkillTemplate st in DataManager.SKILL_DATA.GetSkillTemplatesByGroup(skillGroup))
                foreach (SkillLearnTemplate skill in DataManager.SKILL_TREE_DATA.GetTemplatesForSkill(st.GetSkillId(), player.GetPlayerClass(), player.GetRace()))
                    if (player.GetLevel() >= skill.GetMinLevel())
                        SkillLearnService.LearnTemporarySkill(player, skill.GetSkillId(), stigmaLevel + 1);
    }

    public static void RemoveStigmaSkills(Player player, Stigma stigma, int stigmaLevel, bool notifyPlayer)
    {
        List<string> notifiedSkillL10ns = new List<string>();
        foreach (string skillGroup in stigma.GetGainSkillGroups())
        {
            foreach (SkillTemplate st in DataManager.SKILL_DATA.GetSkillTemplatesByGroup(skillGroup))
            {
                if (notifyPlayer && st.GetL10n() != null && !notifiedSkillL10ns.Contains(st.GetL10n()))
                {
                    notifiedSkillL10ns.Add(st.GetL10n());
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_STIGMA_YOU_CANNOT_USE_THIS_SKILL_AFTER_UNEQUIP_STIGMA_STONE(st.GetL10n()));
                }
                foreach (SkillLearnTemplate skill in DataManager.SKILL_TREE_DATA.GetTemplatesForSkill(st.GetSkillId(), player.GetPlayerClass(), player.GetRace()))
                    SkillLearnService.RemoveSkill(player, skill.GetSkillId());
            }
        }
        RemoveLinkedStigmaSkills(player);
    }
}
