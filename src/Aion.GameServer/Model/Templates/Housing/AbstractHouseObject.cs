using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/AbstractHouseObject (Rolandas).</summary>
[XmlType("AbstractHouseObject")]
[XmlInclude(typeof(PlaceableHouseObject))]
public abstract class AbstractHouseObject : VisibleObjectTemplate
{
    [XmlAttribute("talking_distance")] protected float talkingDistance;

    [XmlAttribute("quality")] protected ItemQuality quality;

    [XmlAttribute("category")] protected HousingCategory category;

    [XmlAttribute("name_id")] protected int nameId;

    [XmlAttribute("id")] protected int id;

    [XmlAttribute("can_dye")] protected bool canDye;

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
