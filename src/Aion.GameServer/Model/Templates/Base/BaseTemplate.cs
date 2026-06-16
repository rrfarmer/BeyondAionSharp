using System.Xml.Serialization;
using Aion.GameServer.Model.Base;

namespace Aion.GameServer.Model.Templates.Base;

/// <summary>Java parity: model/templates/base/BaseTemplate (Source).</summary>
[XmlType("Base")]
public class BaseTemplate
{
    // Public so XmlSerializer can populate them (JAXB used protected fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("id")] public int id;
    [XmlAttribute("world")] public int world;
    [XmlAttribute("type")] public BaseType type;
    [XmlAttribute("color")] public BaseColorType color;
    [XmlAttribute("default_occupier")] public BaseOccupier defaultOccupier = BaseOccupier.BALAUR;

    public int GetId()
    {
        return this.id;
    }

    public int GetWorldId()
    {
        return this.world;
    }

    // Java parity: getType() — renamed GetType_ (GetType collides with object.GetType()).
    public BaseType GetType_()
    {
        return type;
    }

    public BaseColorType GetColor()
    {
        return color;
    }

    public BaseOccupier GetDefaultOccupier()
    {
        return defaultOccupier;
    }
}
