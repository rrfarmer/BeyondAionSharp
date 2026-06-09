using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MeshMaterial (Rolandas).</summary>
[XmlType("MeshMaterial")]
public class MeshMaterial
{
    [XmlAttribute("material_id")] protected int materialId;

    [XmlAttribute("path")] protected string path;

    [XmlAttribute("zone")] private string zoneName;

    public string GetZoneName()
    {
        return zoneName;
    }
}
