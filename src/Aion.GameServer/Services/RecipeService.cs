using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/RecipeService (KID, Neon).</summary>
public class RecipeService
{
    public static RecipeTemplate ValidateNewRecipe(Player player, int recipeId)
    {
        if (player.GetRecipeList().Size() >= 1600)
        {
            PacketSendUtility.SendMessage(player, "You are unable to have more than 1600 recipes at the same time.");
            return null;
        }

        RecipeTemplate template = DataManager.RECIPE_DATA.GetRecipeTemplateById(recipeId);
        if (template == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_RECIPEITEM_CANT_USE_NO_RECIPE());
            return null;
        }

        if (template.GetRace() != Race.PC_ALL)
        {
            if (template.GetRace() != player.GetRace())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CRAFTRECIPE_RACE_CHECK());
                return null;
            }
        }

        if (player.GetRecipeList().IsRecipePresent(recipeId))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_LEARNED_ALREADY());
            return null;
        }

        if (!player.GetSkillList().IsSkillPresent(template.GetSkillId()))
        {
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_CANT_LEARN_SKILL(DataManager.SKILL_DATA.GetSkillTemplate(template.GetSkillId()).GetL10n()));
            return null;
        }

        if (template.GetSkillpoint() > player.GetSkillList().GetSkillLevel(template.GetSkillId()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_CANT_LEARN_SKILLPOINT());
            return null;
        }

        return template;
    }

    public static bool AddRecipe(Player player, int recipeId, bool useValidation)
    {
        RecipeTemplate template = null;
        if (useValidation)
            template = ValidateNewRecipe(player, recipeId);
        else
            template = DataManager.RECIPE_DATA.GetRecipeTemplateById(recipeId);

        if (template == null)
            return false;

        if (player.GetRecipeList().AddRecipe(player, recipeId))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_LEARN(recipeId, player.GetName()));
            return true;
        }
        return false;
    }

    public static void AutoLearnRecipes(Player player, int skillId, int skillLvl)
    {
        foreach (RecipeTemplate recipe in DataManager.RECIPE_DATA.GetAutolearnRecipes(player.GetRace(), skillId, skillLvl))
            player.GetRecipeList().AddRecipe(player, recipe.GetId());
    }
}
