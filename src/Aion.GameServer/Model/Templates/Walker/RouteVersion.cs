using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Walker;

/// <summary>Java parity: model/templates/walker/RouteVersion.</summary>
[XmlType("RouteVersion")]
public class RouteVersion
{
    [XmlAttribute("id")] protected string id;

    public string GetId()
    {
        return id;
    }
}
