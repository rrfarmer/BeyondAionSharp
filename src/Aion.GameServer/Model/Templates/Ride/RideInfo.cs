using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Ride;

/// <summary>Java parity: model/templates/ride/RideInfo (Rolandas).</summary>
[XmlType("RideInfo")]
public class RideInfo
{
    [XmlElement("bounds")] protected Bounds bounds;

    // Java parity: nullable Integer cost_fp.
    [XmlAttribute("cost_fp")] protected int? costFp;

    [XmlAttribute("start_fp")] protected int startFp;
    [XmlAttribute("sprint_speed")] protected float sprintSpeed;
    [XmlAttribute("fly_speed")] protected float flySpeed;
    [XmlAttribute("move_speed")] protected float moveSpeed;

    // Java parity: nullable Integer type.
    [XmlAttribute("type")] protected int? type;

    [XmlAttribute("id")] protected int id;

    public Bounds GetBounds()
    {
        return bounds;
    }

    public int? GetCostFp()
    {
        return costFp;
    }

    public int GetStartFp()
    {
        return startFp;
    }

    /// <summary>reworked call sites use rideInfo.SprintSpeed/FlySpeed/MoveSpeed property-form over the faithful getters</summary>
    public float SprintSpeed => GetSprintSpeed();
    public float FlySpeed => GetFlySpeed();
    public float MoveSpeed => GetMoveSpeed();

    public float GetSprintSpeed()
    {
        return sprintSpeed;
    }

    public float GetFlySpeed()
    {
        return flySpeed;
    }

    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    public int? GetType_()
    {
        return type;
    }

    public int GetNpcId()
    {
        return id;
    }

    public bool CanSprint()
    {
        return sprintSpeed != 0;
    }
}
