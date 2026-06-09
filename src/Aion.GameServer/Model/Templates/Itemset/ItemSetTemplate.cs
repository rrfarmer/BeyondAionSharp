using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemset;

/// <summary>Java parity: model/templates/itemset/ItemSetTemplate implements StatOwner.</summary>
[XmlRoot("itemset")]
public class ItemSetTemplate : Aion.GameServer.Model.Stats.Calc.IStatOwner
{
    [XmlElement("itempart")] protected List<ItemPart> itempart;
    [XmlElement("partbonus")] protected List<PartBonus> partbonus;
    [XmlElement("fullbonus")] protected FullBonus fullbonus;
    [XmlAttribute("name")] protected string name;
    [XmlAttribute("id")] protected int id;

    // Java parity: afterUnmarshal(Unmarshaller, Object) — invoked by the loader after deserialization.
    public void AfterUnmarshal()
    {
        if (fullbonus != null)
        {
            // Set number of items to apply the full bonus
            fullbonus.SetNumberOfItems(itempart.Count);
        }
    }

    public List<ItemPart> GetItempart()
    {
        return itempart;
    }

    public List<PartBonus> GetPartbonus()
    {
        return partbonus;
    }

    public FullBonus GetFullbonus()
    {
        return fullbonus;
    }

    public string GetName()
    {
        return name;
    }

    public int GetId()
    {
        return id;
    }
}
