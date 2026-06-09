using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Gather;

/// <summary>Java parity: model/templates/gather/ExMaterials (KID).</summary>
[XmlType("Exmaterials")]
public class ExMaterials
{
    [XmlElement("material")] protected List<Material> material;

    public List<Material> GetMaterial()
    {
        if (material == null)
        {
            material = new List<Material>();
        }
        return this.material;
    }
}
