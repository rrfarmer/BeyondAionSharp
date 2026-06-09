using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerRecipeFilter.</summary>
public class BrokerRecipeFilter : BrokerFilter
{
    private int craftSkillId;
    private int[] masks;

    public BrokerRecipeFilter(int craftSkillId, params int[] masks)
    {
        this.craftSkillId = craftSkillId;
        this.masks = masks;
    }

    public override bool Accept(ItemTemplate template)
    {
        ItemActions actions = template.GetActions();
        if (actions != null)
        {
            CraftLearnAction craftAction = actions.GetCraftLearnAction();
            if (craftAction != null)
            {
                int id = craftAction.GetRecipeId();
                RecipeTemplate recipeTemplate = DataManager.RECIPE_DATA.GetRecipeTemplateById(id);
                if (recipeTemplate != null && recipeTemplate.GetSkillId() == craftSkillId)
                {
                    return masks.Contains(template.GetTemplateId() / 100000);
                }
            }
        }
        return false;
    }
}
