using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Rift;

/// <summary>Java parity: model/templates/rift/RiftTemplate (Source).</summary>
[XmlType("Rift")]
public class RiftTemplate
{
    [XmlAttribute("id")] private int id;
    [XmlAttribute("world")] private int world;
    [XmlAttribute("has_spawns")] private bool hasSpawns;
    [XmlAttribute("auto_closeable")] private bool autoCloseable = true;

    public RiftTemplate()
    {
    }

    public RiftTemplate(int id, int world, bool hasSpawns, bool autoCloseable)
    {
        this.id = id;
        this.world = world;
        this.hasSpawns = hasSpawns;
        this.autoCloseable = autoCloseable;
    }

    public int GetId()
    {
        return id;
    }

    public int GetWorldId()
    {
        return world;
    }

    public bool HasSpawns()
    {
        return hasSpawns;
    }

    public bool IsAutoCloseable()
    {
        return autoCloseable;
    }
}
