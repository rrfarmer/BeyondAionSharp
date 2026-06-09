using System.Xml.Serialization;
using Aion.GameServer.Model.Flypath;

namespace Aion.GameServer.Model.Templates.Windstreams;

/// <summary>Java parity: model/templates/windstreams/Location2D (LokiReborn).</summary>
[XmlType("Location2D")]
public class Location2D
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("state")] protected int state;
    [XmlAttribute("fly_path")] protected FlyPathType flyPath;

    /// <returns>the id</returns>
    public int GetId()
    {
        return id;
    }

    public int GetState()
    {
        return state;
    }

    public void SetState(int state)
    {
        this.state = state;
    }

    /// <returns>the bidirectional</returns>
    public FlyPathType GetFlyPathType()
    {
        return flyPath;
    }
}
