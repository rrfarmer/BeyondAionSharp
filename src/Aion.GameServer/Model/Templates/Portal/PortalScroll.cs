using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalScroll (JAXB-generated).</summary>
[XmlType("PortalScroll")]
public class PortalScroll
{
    [XmlElement("portal_path")] public PortalPath portalPath;
    [XmlAttribute("name")] public string name;

    public PortalPath GetPortalPath()
    {
        return portalPath;
    }

    /// <summary>Gets the value of the name property.</summary>
    public string GetName()
    {
        return name;
    }

    /// <summary>Sets the value of the name property.</summary>
    public void SetName(string value)
    {
        this.name = value;
    }
}
