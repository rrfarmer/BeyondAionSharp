using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Stats;

/// <summary>
/// Movement speeds (walk/run/fly, plus group/fight variants) for a creature template.
/// Java parity: model/templates/stats/CreatureSpeeds (@XmlType("CreatureSpeeds")).
/// </summary>
[XmlType("CreatureSpeeds")]
public class CreatureSpeeds
{
    [XmlAttribute("walk")] public float WalkSpeed { get; set; }
    [XmlAttribute("run")] public float RunSpeed { get; set; }
    [XmlAttribute("group_walk")] public float GroupWalkSpeed { get; set; }
    [XmlAttribute("run_fight")] public float RunSpeedFight { get; set; }
    [XmlAttribute("group_run_fight")] public float GroupRunSpeedFight { get; set; }
    [XmlAttribute("fly")] public float FlySpeed { get; set; }

    public float GetWalkSpeed() => WalkSpeed;
    public float GetRunSpeed() => RunSpeed;
    public float GetFlySpeed() => FlySpeed;
    public float GetGroupWalkSpeed() => GroupWalkSpeed;
    public float GetRunSpeedFight() => RunSpeedFight;
    public float GetGroupRunSpeedFight() => GroupRunSpeedFight;
}
