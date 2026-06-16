using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Recipe;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/RecipeData (ATracer, MrPoke, KID). @XmlRootElement(recipe_templates); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("recipe_templates")]
public class RecipeData
{
    [XmlElement("recipe_template")] public List<RecipeTemplate> list;

    [XmlIgnore] private readonly Dictionary<int, RecipeTemplate> recipeData = new();
    [XmlIgnore] private readonly List<RecipeTemplate> autoLearnRecipes = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (RecipeTemplate it in list)
        {
            recipeData[it.GetId()] = it;
            if (it.GetAutoLearn() != 0)
                autoLearnRecipes.Add(it);
        }
        list = null;
    }

    public List<RecipeTemplate> GetAutolearnRecipes(Race race, int skillId, int maxLevel)
    {
        List<RecipeTemplate> list = new();
        foreach (RecipeTemplate recipe in autoLearnRecipes)
        {
            if (recipe.GetSkillId() != skillId || recipe.GetSkillpoint() > maxLevel)
                continue;
            if (recipe.GetRace() != Race.PC_ALL && recipe.GetRace() != race)
                continue;
            list.Add(recipe);
        }
        return list;
    }

    public RecipeTemplate GetRecipeTemplateById(int id)
    {
        return recipeData.TryGetValue(id, out var v) ? v : null;
    }

    public ICollection<RecipeTemplate> GetRecipeTemplates()
    {
        return recipeData.Values;
    }

    public int Size()
    {
        return recipeData.Count;
    }
}
