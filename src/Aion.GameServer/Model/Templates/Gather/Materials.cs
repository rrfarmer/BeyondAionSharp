using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Gather;

/// <summary>Java parity: model/templates/gather/Materials (ATracer).</summary>
[XmlType("Materials")]
public class Materials
{
    [XmlElement("material")] protected List<Material> material;

    /// <summary>Gets the value of the material property.</summary>
    public List<Material> GetMaterial()
    {
        if (material == null)
        {
            material = new List<Material>();
        }
        return this.material;
    }
}
