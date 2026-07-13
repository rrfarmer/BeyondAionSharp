using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/SkillLearnService (ATracer, xTz, Neon). Faithful 1:1 re-port (replaces the
/// reworked plan-based slop). onLearnSkill/learnNewSkills/learnTemporarySkill/autoLearnSkills/learnSkillBook/removeSkill
/// over the faithful PlayerSkillList + DataManager.SKILL_TREE_DATA(SkillTreeData enum API) + SKILL_DATA. Java
/// switch(skillLevel) case 1,100,...,500 -> C# pattern; SkillEngine.applyEffectDirectly(skillTemplate,...) maps to the
/// faithful (skillId,...) overload.</summary>
public static class SkillLearnService
{
    public static void OnLearnSkill(Player player, int skillId, int skillLevel, bool isNew)
    {
        PlayerSkillEntry skill = player.GetSkillList().GetSkillEntry(skillId);
        if (skill.IsProfessionSkill() && skillLevel is 1 or 100 or 200 or 300 or 400 or 450 or 500)
        {
            if (skillLevel != 1 || skill.IsCraftingSkill()) // exclude lvl 1 tapping skills
                PacketSendUtility.BroadcastPacket(player, new SM_ACTION_ANIMATION(player.GetObjectId(), ActionAnimation.CRAFT_LEVEL_UP), true);
        }
        if (player.GetEffectController() != null) // null on character creation
        {
            if (player.IsSpawned())
                SendPacket(player, skill, isNew);
            SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
            if (skillTemplate.IsPassive())
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player);
            if (skill.IsProfessionSkill() && (skill.GetSkillLevel() == 399 || skill.GetSkillLevel() == 499))
                player.GetController().UpdateNearbyQuests();
        }
        if (skill.IsCraftingSkill() || skill.IsMorphSkill())
            RecipeService.AutoLearnRecipes(player, skillId, skillLevel);
    }

    private static void SendPacket(Player player, PlayerSkillEntry skill, bool isNew)
    {
        if (skill.IsProfessionSkill())
        {
            if (skill.IsTappingSkill())
                PacketSendUtility.SendPacket(player, new SM_SKILL_LIST(skill, isNew ? 1330004 : 1330005));
            else
                PacketSendUtility.SendPacket(player, new SM_SKILL_LIST(skill, isNew ? 1330061 : 1330064));
        }
        else if (isNew)
            PacketSendUtility.SendPacket(player,
                new SM_SKILL_LIST(skill, skill.IsStigmaSkill() ? skill.IsLinkedStigmaSkill() ? 1402891 : 1300401 : 1300050));
        else
            PacketSendUtility.SendPacket(player, new SM_SKILL_LIST(skill, 0));
    }

    /// <summary>
    /// Adds all missing skills and recipes that can be auto-learned for the given level range.
    /// </summary>
    public static void LearnNewSkills(Player player, int fromLevel, int toLevel)
    {
        PlayerClass playerClass = player.GetCommonData().GetPlayerClass();
        PlayerClass? playerStartClass = playerClass.IsStartingClass() ? (PlayerClass?)null : playerClass.GetStartingClass();
        for (int level = toLevel; level >= fromLevel; level--) // reversed order to add only the highest of each skill (more efficient)
        {
            if (level < 10 && playerStartClass != null) // add missing start class skills if already switched class
                AutoLearnSkills(player, level, playerStartClass.Value, player.GetRace());
            AutoLearnSkills(player, level, playerClass, player.GetRace());
        }

        // upgrade human gathering to daeva essence tapping
        if (toLevel >= 10 && player.GetCommonData().IsDaeva() && player.GetSkillList().IsSkillPresent(30001))
        {
            if (!player.GetSkillList().IsSkillPresent(30002))
                player.GetSkillList().AddSkill(player, 30002, player.GetSkillList().GetSkillLevel(30001));
            RemoveSkill(player, 30001);
        }
    }

    public static void LearnTemporarySkill(Player player, int skillId, int skillLevel)
    {
        player.GetSkillList().AddTemporarySkill(player, skillId, skillLevel);
    }

    /// <summary>
    /// Adds auto-learned skills to the player, according to the specified level, class and race.
    /// </summary>
    private static void AutoLearnSkills(Player player, int level, PlayerClass playerClass, Race playerRace)
    {
        foreach (SkillLearnTemplate template in DataManager.SKILL_TREE_DATA.GetTemplatesFor(playerClass, level, playerRace))
        {
            if (!template.IsAutolearn())
                continue;
            if (template.GetSkillId() == 30001 && !playerClass.IsStartingClass()) // no human gathering for main classes
                continue;

            player.GetSkillList().AddSkill(player, template.GetSkillId(), template.GetSkillLevel());
        }
    }

    public static void LearnSkillBook(Player player, int skillId)
    {
        foreach (SkillLearnTemplate skill in DataManager.SKILL_TREE_DATA.GetSkillsForSkill(skillId, player.GetPlayerClass(), player.GetRace(),
            player.GetLevel()))
            player.GetSkillList().AddSkill(player, skillId, skill.GetSkillLevel());
    }

    public static bool RemoveSkill(Player player, int skillId)
    {
        PlayerSkillEntry skill = player.GetSkillList().GetSkillEntry(skillId);
        if (skill != null)
        {
            SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
            if (ShouldRemoveEffectOnSkillRemoval(skillTemplate))
                player.GetEffectController().RemoveEffect(skillId);
            player.GetSkillList().RemoveSkill(skillId);
            PacketSendUtility.SendPacket(player, new SM_SKILL_REMOVE(skill));
            return true;
        }
        return false;
    }

    internal static bool ShouldRemoveEffectOnSkillRemoval(SkillTemplate skillTemplate)
    {
        return skillTemplate.IsPassive() || skillTemplate.IsToggle() || skillTemplate.IsDeityAvatar()
            || skillTemplate.GetStack()!.StartsWith("WS_", StringComparison.Ordinal);
    }
}
