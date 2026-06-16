using System.Globalization;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Ride;

/// <summary>Java parity: model/templates/ride/RideInfo (Rolandas).</summary>
[XmlType("RideInfo")]
public class RideInfo
{
    [XmlElement("bounds")] public Bounds bounds;

    // Java parity: nullable Integer cost_fp.
    // String-proxy: XmlSerializer cannot bind a nullable value type to an [XmlAttribute] (it emits the
    // "cannot encode complex types" error). Back the attribute with a string and parse it, mirroring
    // JAXB's native nullable-attribute handling: missing attribute -> null, present -> Integer.parseInt.
    [XmlIgnore] protected int? costFp;

    [XmlAttribute("cost_fp")]
    public string CostFpRaw
    {
        get => costFp?.ToString(CultureInfo.InvariantCulture);
        set => costFp = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("start_fp")] public int startFp;
    [XmlAttribute("sprint_speed")] public float sprintSpeed;
    [XmlAttribute("fly_speed")] public float flySpeed;
    [XmlAttribute("move_speed")] public float moveSpeed;

    // Java parity: nullable Integer type. (string-proxy — see cost_fp above)
    [XmlIgnore] protected int? type;

    [XmlAttribute("type")]
    public string TypeRaw
    {
        get => type?.ToString(CultureInfo.InvariantCulture);
        set => type = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("id")] public int id;

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
