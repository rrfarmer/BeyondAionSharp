using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MeshList.</summary>
[XmlType("MeshList")]
public class MeshList
{
    [XmlElement("mesh")] protected List<MeshMaterial> meshMaterials;

    [XmlAttribute("world_id")] protected int worldId;

    [XmlIgnore] internal Dictionary<string, int> materialIdsByPath = new Dictionary<string, int>();

    // Java parity: pathZones keyed by path.hashCode(). C# string.GetHashCode() is in-process consistent (put & get
    // both use it within the same run; map rebuilt each load), so functionally equivalent here.
    [XmlIgnore] internal Dictionary<int, string> pathZones = new Dictionary<int, string>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        if (meshMaterials == null)
            return;

        foreach (MeshMaterial meshMaterial in meshMaterials)
        {
            materialIdsByPath[meshMaterial.path] = meshMaterial.materialId;
            pathZones[meshMaterial.path.GetHashCode()] = meshMaterial.GetZoneName();
            meshMaterial.path = null;
        }

        meshMaterials = null;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    /// <summary>Find material ID for the specific mesh. Returns 0 if not found.</summary>
    public int GetMeshMaterialId(string meshPath)
    {
        return materialIdsByPath.TryGetValue(meshPath, out var materialId) ? materialId : 0;
    }

    public ICollection<string> GetMeshPaths()
    {
        return materialIdsByPath.Keys;
    }

    public string GetZoneName(string meshPath)
    {
        return pathZones.TryGetValue(meshPath.GetHashCode(), out var v) ? v : null;
    }

    public int Size()
    {
        return materialIdsByPath.Count;
    }
}
