using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Recipe;

/// <summary>Java parity: model/templates/recipe/ComponentsData (xTz).</summary>
[XmlType("ComponentsData")]
public class ComponentsData
{
    [XmlElement("component")] protected List<Component> component;

    public List<Component> GetComponent()
    {
        return component;
    }
}
