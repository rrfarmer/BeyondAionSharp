using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Craft;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Craft;

/// <summary>Java parity: services/craft/RelinquishCraftStatus (synchro2). Static expert/master craft-status relinquish: skill-level range + kinah check, downgrade skill, remove recipes above, delete craft-status quests; removeExcessCraftStatus recursion (master then expert). PricesService/MasterQuestsList/ExpertQuestsList/RecipeTemplate red-tolerated.</summary>
public class RelinquishCraftStatus
{
    private const int expertMinValue = 400;
    private const int expertMaxValue = 499;
    private const int masterMinValue = 500;
    private const int masterMaxValue = 549;
    private const int expertPrice = 120895;
    private const int masterPrice = 3497448;
    private const int skillMessageId = 1401127;

    public static bool RelinquishExpertStatus(Player player, Profession? profession)
    {
        return RelinquishExpertStatus(player, profession, expertPrice);
    }

    public static bool RelinquishExpertStatus(Player player, Profession? profession, int price)
    {
        return RelinquishCraftStatusInternal(player, profession, expertMinValue, expertMaxValue, price);
    }

    public static bool RelinquishMasterStatus(Player player, Profession? profession)
    {
        return RelinquishMasterStatus(player, profession, masterPrice);
    }

    public static bool RelinquishMasterStatus(Player player, Profession? profession, int price)
    {
        return RelinquishCraftStatusInternal(player, profession, masterMinValue, masterMaxValue, price);
    }

    private static bool RelinquishCraftStatusInternal(Player player, Profession? profession, int minSkillLevel, int maxSkillLevel, int price)
    {
        if (!profession.HasValue || !profession.Value.IsCrafting())
            return false;
        Profession resolvedProfession = profession.Value;
        PlayerSkillEntry skill = player.GetSkillList().GetSkillEntry(resolvedProfession.GetSkillId());
        if (skill == null || skill.GetSkillLevel() < minSkillLevel || skill.GetSkillLevel() > maxSkillLevel)
            return false;
        if (!DecreaseKinah(player, price))
            return false;
        skill.SetSkillLvl(minSkillLevel - 1);
        PacketSendUtility.SendPacket(player, new SM_SKILL_LIST(skill, skillMessageId));
        RemoveRecipesAbove(player, skill.GetSkillId(), minSkillLevel);
        DeleteCraftStatusQuests(skill.GetSkillId(), player, maxSkillLevel < masterMinValue);
        return true;
    }

    private static bool DecreaseKinah(Player player, int basePrice)
    {
        if (basePrice > 0 && !player.GetInventory().TryDecreaseKinah(Aion.GameServer.Services.Trade.PricesService.GetPriceForService(basePrice, player.GetRace())))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            return false;
        }
        return true;
    }

    public static void RemoveRecipesAbove(Player player, int skillId, int level)
    {
        foreach (RecipeTemplate recipe in DataManager.RECIPE_DATA.GetRecipeTemplates())
        {
            if (recipe.GetSkillId() != skillId || recipe.GetSkillpoint() < level)
            {
                continue;
            }
            player.GetRecipeList().DeleteRecipe(player, recipe.GetId());
        }
    }

    public static void DeleteCraftStatusQuests(int skillId, Player player, bool isExpert)
    {
        foreach (int questId in MasterQuestsListExtensions.GetQuestIds(skillId, player.GetRace()))
        {
            player.GetQuestStateList().DeleteQuest(questId);
        }
        if (isExpert)
        {
            foreach (int questId in ExpertQuestsListExtensions.GetQuestIds(skillId, player.GetRace()))
            {
                player.GetQuestStateList().DeleteQuest(questId);
            }
        }
        QuestEngine.QuestEngine.GetInstance().SendCompletedQuests(player);
        player.GetController().UpdateNearbyQuests();
    }

    public static void RemoveExcessCraftStatus(Player player, bool isExpert)
    {
        int minValue = isExpert ? expertMinValue : masterMinValue;
        int maxValue = isExpert ? expertMaxValue : masterMaxValue;
        int skillId;
        int skillLevel;
        int maxCraftStatus = isExpert ? CraftConfig.MAX_EXPERT_CRAFTING_SKILLS : CraftConfig.MAX_MASTER_CRAFTING_SKILLS;
        int countCraftStatus;
        foreach (PlayerSkillEntry skill in player.GetSkillList().GetAllSkills())
        {
            countCraftStatus = isExpert
                ? CraftSkillUpdateService.GetInstance().GetTotalMasterCraftingSkills(player)
                    + CraftSkillUpdateService.GetInstance().GetTotalExpertCraftingSkills(player)
                : CraftSkillUpdateService.GetInstance().GetTotalMasterCraftingSkills(player);
            if (countCraftStatus > maxCraftStatus)
            {
                skillId = skill.GetSkillId();
                skillLevel = skill.GetSkillLevel();
                if (skill.IsCraftingSkill() && skillLevel > minValue && skillLevel <= maxValue)
                {
                    skill.SetSkillLvl(minValue - 1);
                    PacketSendUtility.SendPacket(player, new SM_SKILL_LIST(skill, skillMessageId));
                    RemoveRecipesAbove(player, skillId, minValue);
                    DeleteCraftStatusQuests(skillId, player, isExpert);
                }
                continue;
            }
            break;
        }
        if (!isExpert)
        {
            RemoveExcessCraftStatus(player, true);
        }
    }
}
