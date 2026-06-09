using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MeshMaterial (Rolandas).</summary>
[XmlType("MeshMaterial")]
public class MeshMaterial
{
    // Java parity: package-private fields accessed by sibling MeshList → internal (closest C# analog).
    [XmlAttribute("material_id")] internal int materialId;

    [XmlAttribute("path")] internal string path;

    [XmlAttribute("zone")] private string zoneName;

    public string GetZoneName()
    {
        return zoneName;
    }
}
