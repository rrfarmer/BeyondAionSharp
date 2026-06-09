using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/InstanceExit (xTz).</summary>
[XmlType("InstanceExit")]
public class InstanceExit
{
    [XmlAttribute("instance_id")] protected int instanceId;
    [XmlAttribute("exit_world")] protected int exitWorld;
    [XmlAttribute("race")] protected Race race = Race.PC_ALL;
    [XmlAttribute("x")] protected float x;
    [XmlAttribute("y")] protected float y;
    [XmlAttribute("z")] protected float z;
    [XmlAttribute("h")] protected sbyte h;

    // Java parity: declared return type Integer (field is primitive int, never null).
    public int? GetInstanceId()
    {
        return instanceId;
    }

    public void SetInstanceId(int value)
    {
        this.instanceId = value;
    }

    public int GetExitWorld()
    {
        return exitWorld;
    }

    public void SetExitWorld(int value)
    {
        this.exitWorld = value;
    }

    public Race GetRace()
    {
        return race;
    }

    public void SetRace(Race value)
    {
        this.race = value;
    }

    public float GetX()
    {
        return x;
    }

    public void SetX(float value)
    {
        this.x = value;
    }

    public float GetY()
    {
        return y;
    }

    public void SetY(float value)
    {
        this.y = value;
    }

    public float GetZ()
    {
        return z;
    }

    public void SetZ(float value)
    {
        this.z = value;
    }

    public sbyte GetH()
    {
        return h;
    }

    public void SetH(sbyte value)
    {
        this.h = value;
    }
}
