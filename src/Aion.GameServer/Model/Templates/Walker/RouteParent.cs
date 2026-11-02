using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Walker;

/// <summary>Java parity: model/templates/walker/RouteParent.</summary>
[XmlType("RouteParent")]
public class RouteParent
{
    [XmlElement("version")] protected List<RouteVersion> versions;

    [XmlAttribute("id")] protected string id;

    public List<RouteVersion> GetRouteVersion()
    {
        return versions == null ? new List<RouteVersion>() : versions;
    }

    public string GetId()
    {
        return id;
    }
}
