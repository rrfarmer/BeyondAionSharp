using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Recipe;

/// <summary>Java parity: model/templates/recipe/RecipeTemplate (ATracer).</summary>
[XmlType("RecipeTemplate")]
public class RecipeTemplate : IL10n
{
    [XmlElement("components_data")] protected List<ComponentsData> componentsData;
    [XmlElement("comboproduct")] protected List<ComboProduct> comboproduct;
    [XmlAttribute("max_production_count")] protected int? maxProductionCount;
    [XmlAttribute("craft_delay_time")] protected int? craftDelayTime;
    [XmlAttribute("craft_delay_id")] protected int? craftDelayId;
    [XmlAttribute("quantity")] protected int quantity;
    [XmlAttribute("productid")] protected int productid;
    [XmlAttribute("autolearn")] protected int autolearn;
    [XmlAttribute("dp")] protected int dp;
    [XmlAttribute("skillpoint")] protected int skillpoint;
    [XmlAttribute("race")] protected Race race;
    [XmlAttribute("skillid")] protected int skillid;
    [XmlAttribute("itemid")] protected int itemid;
    [XmlAttribute("nameid")] protected int nameid;
    [XmlAttribute("id")] protected int id;

    /// <summary>
    /// Gets the value of the component property. This accessor method returns a reference to the live list, not a snapshot.
    /// </summary>
    public List<ComponentsData> GetComponents()
    {
        return componentsData == null ? new List<ComponentsData>() : componentsData;
    }

    public int? GetComboProduct(int num)
    {
        if (comboproduct == null || comboproduct[num - 1] == null)
        {
            return null;
        }
        return comboproduct[num - 1].GetItemId();
    }

    public int GetComboProductSize()
    {
        if (comboproduct == null)
        {
            return 0;
        }
        return comboproduct.Count;
    }

    public int GetQuantity()
    {
        return quantity;
    }

    public int GetProductId()
    {
        return productid;
    }

    public int GetAutoLearn()
    {
        return autolearn;
    }

    public int GetDp()
    {
        return dp;
    }

    public int GetSkillpoint()
    {
        return skillpoint;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetSkillId()
    {
        return skillid;
    }

    public int GetItemId()
    {
        return itemid;
    }

    /// <returns>the nameid</returns>
    public int GetL10nId()
    {
        return nameid;
    }

    public int GetId()
    {
        return id;
    }

    /// <returns>Returns the maxProductionCount.</returns>
    public int? GetMaxProductionCount()
    {
        return maxProductionCount;
    }

    /// <returns>Returns the craftDelayTime.</returns>
    public int? GetCraftDelayTime()
    {
        return craftDelayTime;
    }

    /// <returns>Returns the craftDelayId.</returns>
    public int? GetCraftDelayId()
    {
        return craftDelayId;
    }
}
