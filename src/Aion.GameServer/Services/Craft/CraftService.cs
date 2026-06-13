using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.SkillEngine.Task;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using ItemAddType = Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;
using ItemUpdatePredicate = Aion.GameServer.Services.Items.ItemService.ItemUpdatePredicate;

namespace Aion.GameServer.Services.Craft;

/// <summary>Java parity: services/craft/CraftService (MrPoke, sphinx, synchro2, Evil_dnk). "CRAFT_LOG" logger; finishCrafting (xp/crit/combo product, anonymous ItemUpdatePredicate->nested CraftedItemPredicate setting weapon/armor creator, craft cooldown), startCrafting (quality interval cap, CraftingTask), checkCraft (target/DP/mode/inventory/recipe/cooldown/skill/component gates), sendCancelCraft, getBonusReqItem. Map<Integer,Long>->Dictionary; int? nullables .Value; int*=float lossy->cast; currentTimeMillis->UtcNow.ToUnixTimeMilliseconds; switch quality/skillId; ItemAddType/ItemUpdateType aliases. CraftingTask/RecipeTemplate/DAO red-tolerated.</summary>
public class CraftService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("CRAFT_LOG");

    public static void FinishCrafting(Player player, RecipeTemplate recipetemplate, int critCount, int bonus)
    {
        if (recipetemplate.GetMaxProductionCount() != null)
        {
            player.GetRecipeList().DeleteRecipe(player, recipetemplate.GetId());
            if (critCount == 0)
            {
                QuestEngine.QuestEngine.GetInstance().OnFailCraft(new QuestEnv(null, player, 0),
                    recipetemplate.GetComboProduct(1) == null ? 0 : recipetemplate.GetComboProduct(1).Value);
            }
        }

        int skillId = recipetemplate.GetSkillId();
        int skillLvl = recipetemplate.GetSkillpoint();
        int xpReward = (int)((0.008 * (skillLvl + 100) * (skillLvl + 100) + 60));
        xpReward = xpReward + (xpReward * bonus / 100); // bonus
        int gainedCraftXp = Rates.SKILL_XP_CRAFTING.CalcResult(player, xpReward);
        StatEnum? boostStat = StatEnumExtensions.GetModifier(skillId);
        if (boostStat != null) // there is no boost for morphing (40009)
            gainedCraftXp = (int)(gainedCraftXp * (player.GetGameStats().GetStat(boostStat.Value, 100).GetCurrent() / 100f));
        gainedCraftXp = Math.Max(1, gainedCraftXp);

        if (player.GetSkillList().AddSkillXp(player, skillId, gainedCraftXp, skillLvl))
        {
            player.GetCommonData().AddExp(xpReward, Rates.XP_CRAFTING);
        }
        else
        {
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_MSG_DONT_GET_PRODUCTION_EXP(DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetL10n()));
        }

        int productItemId = critCount > 0 ? recipetemplate.GetComboProduct(critCount).Value : recipetemplate.GetProductId();

        ItemService.AddItem(player, productItemId, recipetemplate.GetQuantity(), true,
            new CraftedItemPredicate(player));

        if (LoggingConfig.LOG_CRAFT)
        {
            ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(productItemId);
            log.LogInformation("Player " + player.GetName() + " crafted item " + productItemId + " [" + itemTemplate.GetName() + "] (count: "
                + recipetemplate.GetQuantity() + ")" + (critCount > 0 ? " - critical" : ""));
        }

        if (recipetemplate.GetCraftDelayId() != null)
        {
            long reuseTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + recipetemplate.GetCraftDelayTime().Value * 1000;
            player.GetCraftCooldowns()[recipetemplate.GetCraftDelayId().Value] = reuseTimeMillis;
        }
    }

    public static void StartCrafting(Player player, int recipeId, int targetObjId, int craftType, Dictionary<int, long> sendMaterialsData)
    {
        RecipeTemplate recipeTemplate = DataManager.RECIPE_DATA.GetRecipeTemplateById(recipeId);
        int skillId = recipeTemplate.GetSkillId();
        VisibleObject target = player.GetKnownList().GetObject(targetObjId);
        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(recipeTemplate.GetProductId());

        if (!CheckCraft(player, recipeTemplate, skillId, target, itemTemplate, craftType, sendMaterialsData))
        {
            SendCancelCraft(player, skillId, targetObjId, itemTemplate);
            return;
        }

        if (recipeTemplate.GetDp() != 0)
            player.GetCommonData().AddDp(-recipeTemplate.GetDp());

        int intervalCap = 1200;
        switch (itemTemplate.GetItemQuality())
        {
            case ItemQuality.UNIQUE:
            case ItemQuality.EPIC:
                intervalCap = 1500;
                break;
            case ItemQuality.MYTHIC:
                intervalCap = 1700;
                break;
        }
        int skillLvlDiff = player.GetSkillList().GetSkillLevel(skillId) - recipeTemplate.GetSkillpoint();
        player.SetCraftingTask(new CraftingTask(player, (StaticObject)target, recipeTemplate, skillLvlDiff, craftType == 1 ? 15 : 0));

        if (skillId == 40009)
        {
            player.GetCraftingTask().SetInterval(200);
        }
        else
        {
            int interval = 2500 - (skillLvlDiff * 60);
            player.GetCraftingTask().SetInterval(interval < intervalCap ? intervalCap : interval);
        }
        player.GetCraftingTask().Start();
    }

    private static bool CheckCraft(Player player, RecipeTemplate recipeTemplate, int skillId, VisibleObject target, ItemTemplate itemTemplate,
        int craftType, Dictionary<int, long> sendMaterialsData)
    {
        if (recipeTemplate == null)
        {
            return false;
        }

        if (itemTemplate == null)
        {
            return false;
        }

        if (player.GetCraftingTask() != null && player.GetCraftingTask().IsInProgress())
        {
            return false;
        }

        // morphing dont need static object/npc to use
        if ((skillId != 40009))
        {
            if (target == null || !(target is StaticObject))
            {
                AuditLogger.Log(player, "tried to craft with incorrect target");
                return false;
            }
            else if (!PositionUtil.IsInRange(player, target, 5, false))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_TOO_FAR_FROM_TOOL(target.GetObjectTemplate().GetL10n()));
                return false;
            }
        }

        if (recipeTemplate.GetDp() != null && (player.GetCommonData().GetDp() < recipeTemplate.GetDp()))
        {
            AuditLogger.Log(player, "tried to craft without required DP count");
            return false;
        }

        if (player.IsInPlayerMode(PlayerMode.RIDE) || player.IsInAnyHide())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_COMBINE_WHILE_IN_CURRENT_STANCE());
            return false;
        }

        if (player.GetInventory().IsFull())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_INVENTORY_IS_FULL());
            return false;
        }

        if (!player.GetRecipeList().IsRecipePresent(recipeTemplate.GetId()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_CAN_NOT_FIND_RECIPE());
            return false;
        }

        if (recipeTemplate.GetCraftDelayId() != null && player.GetCraftCooldowns().HasCooldown(recipeTemplate.GetCraftDelayId().Value))
        {
            // since there's no SM_CRAFT_COOLDOWN (at least we didn't find it yet), we must send some sys message to the player instead of audit logging
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_CANT_USE_UNTIL_DELAY_TIME());
            // AuditLogger.log(player, "tried to craft before cooldown expired");
            return false;
        }

        if (!player.GetSkillList().IsSkillPresent(skillId))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_CANT_USE(DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetL10n()));
            return false;
        }

        if (player.GetSkillList().GetSkillLevel(skillId) < recipeTemplate.GetSkillpoint())
        {
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_COMBINE_OUT_OF_SKILL_POINT(DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetL10n()));
            return false;
        }

        foreach (ComponentsData componentsData in recipeTemplate.GetComponents())
        {
            Component firstComponent = componentsData.GetComponent()[0];
            if (!sendMaterialsData.ContainsKey(firstComponent.GetItemId()))
                continue;
            foreach (Component component in componentsData.GetComponent())
            {
                long availableComponentCount = player.GetInventory().GetItemCountByItemId(component.GetItemId());
                if (availableComponentCount < component.GetQuantity())
                {
                    string itemL10n = DataManager.ITEM_DATA.GetItemTemplate(component.GetItemId()).GetL10n();
                    if (component.GetQuantity() == 1)
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_NO_COMPONENT_ITEM_SINGLE(itemL10n));
                    else
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_COMBINE_NO_COMPONENT_ITEM_MULTIPLE(component.GetQuantity(), itemL10n));
                    return false;
                }
            }
            break;
        }

        if (craftType == 1 && !player.GetInventory().DecreaseByItemId(GetBonusReqItem(skillId), 1))
        {
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_COMBINE_NO_COMPONENT_ITEM_SINGLE(DataManager.ITEM_DATA.GetItemTemplate(GetBonusReqItem(skillId)).GetL10n()));
            return false;
        }

        foreach (ComponentsData componentsData in recipeTemplate.GetComponents())
        {
            Component firstComponent = componentsData.GetComponent()[0];
            if (!sendMaterialsData.ContainsKey(firstComponent.GetItemId()))
                continue;

            foreach (Component component in componentsData.GetComponent())
                player.GetInventory().DecreaseByItemId(component.GetItemId(), component.GetQuantity());
            break;
        }

        return true;
    }

    private static void SendCancelCraft(Player player, int skillId, int targetObjId, ItemTemplate itemTemplate)
    {
        PacketSendUtility.SendPacket(player, new SM_CRAFT_UPDATE(skillId, itemTemplate, 0, 0, 4, 0, 0));
        PacketSendUtility.BroadcastPacket(player, new SM_CRAFT_ANIMATION(player.GetObjectId(), targetObjId, 0, 2), true);
    }

    private static int GetBonusReqItem(int skillId)
    {
        switch (skillId)
        {
            case 40001: // Cooking
                return 169401081;
            case 40002: // Weaponsmithing
                return 169401076;
            case 40003: // Armorsmithing
                return 169401077;
            case 40004: // Tailoring
                return 169401078;
            case 40007: // Alchemy
                return 169401080;
            case 40008: // Handicrafting
                return 169401079;
            case 40010: // Menusier
                return 169401082;
        }
        return 0;
    }

    private sealed class CraftedItemPredicate : ItemUpdatePredicate
    {
        private readonly Player player;

        public CraftedItemPredicate(Player player)
            : base(ItemAddType.CRAFTED_ITEM, ItemUpdateType.INC_ITEM_COLLECT)
        {
            this.player = player;
        }

        public override bool ChangeItem(Item item)
        {
            if (item.GetItemTemplate().IsWeapon() || item.GetItemTemplate().IsArmor())
            {
                item.SetItemCreator(player.GetName());
                return true;
            }
            return false;
        }
    }
}
