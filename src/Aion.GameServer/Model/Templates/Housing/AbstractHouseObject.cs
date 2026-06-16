using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/AbstractHouseObject (Rolandas).</summary>
[XmlType("AbstractHouseObject")]
[XmlInclude(typeof(PlaceableHouseObject))]
public abstract class AbstractHouseObject : VisibleObjectTemplate
{
    [XmlAttribute("talking_distance")] public float talkingDistance;

    [XmlAttribute("quality")] public ItemQuality quality;

    [XmlAttribute("category")] public HousingCategory category;

    [XmlAttribute("name_id")] public int nameId;

    [XmlAttribute("id")] public int id;

    [XmlAttribute("can_dye")] public bool canDye;

    public override int GetTemplateId()
    {
        return id;
    }

    public float GetTalkingDistance()
    {
        return talkingDistance;
    }

    public ItemQuality GetQuality()
    {
        return quality;
    }

    public HousingCategory GetCategory()
    {
        return category;
    }

    public bool GetCanDye()
    {
        return canDye;
    }

    public override int GetL10nId()
    {
        return nameId;
    }

    public override string GetName()
    {
        return null;
    }
}
