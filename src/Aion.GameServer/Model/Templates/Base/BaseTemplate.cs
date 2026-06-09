using System.Xml.Serialization;
using Aion.GameServer.Model.Base;

namespace Aion.GameServer.Model.Templates.Base;

/// <summary>Java parity: model/templates/base/BaseTemplate (Source).</summary>
[XmlType("Base")]
public class BaseTemplate
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("world")] protected int world;
    [XmlAttribute("type")] protected BaseType type;
    [XmlAttribute("color")] protected BaseColorType color;
    [XmlAttribute("default_occupier")] protected BaseOccupier defaultOccupier = BaseOccupier.BALAUR;

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
