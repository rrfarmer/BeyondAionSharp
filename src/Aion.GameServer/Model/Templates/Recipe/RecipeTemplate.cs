using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Recipe;

/// <summary>Java parity: model/templates/recipe/RecipeTemplate (ATracer).</summary>
[XmlType("RecipeTemplate")]
public class RecipeTemplate : IL10n
{
    [XmlElement("components_data")] public List<ComponentsData> componentsData;
    [XmlElement("comboproduct")] public List<ComboProduct> comboproduct;

    // JAXB @XmlAttribute Integer (nullable) — XmlSerializer cannot bind int?; round-trip through a string proxy
    // (absent attribute -> null, 1:1 with JAXB).
    [XmlIgnore] protected int? maxProductionCount;
    [XmlIgnore] protected int? craftDelayTime;
    [XmlIgnore] protected int? craftDelayId;

    [XmlAttribute("max_production_count")]
    public string MaxProductionCountRaw
    {
        get => maxProductionCount?.ToString();
        set => maxProductionCount = value == null ? null : int.Parse(value);
    }

    [XmlAttribute("craft_delay_time")]
    public string CraftDelayTimeRaw
    {
        get => craftDelayTime?.ToString();
        set => craftDelayTime = value == null ? null : int.Parse(value);
    }

    [XmlAttribute("craft_delay_id")]
    public string CraftDelayIdRaw
    {
        get => craftDelayId?.ToString();
        set => craftDelayId = value == null ? null : int.Parse(value);
    }

    [XmlAttribute("quantity")] public int quantity;
    [XmlAttribute("productid")] public int productid;
    [XmlAttribute("autolearn")] public int autolearn;
    [XmlAttribute("dp")] public int dp;
    [XmlAttribute("skillpoint")] public int skillpoint;
    [XmlAttribute("race")] public Race race;
    [XmlAttribute("skillid")] public int skillid;
    [XmlAttribute("itemid")] public int itemid;
    [XmlAttribute("nameid")] public int nameid;
    [XmlAttribute("id")] public int id;

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
