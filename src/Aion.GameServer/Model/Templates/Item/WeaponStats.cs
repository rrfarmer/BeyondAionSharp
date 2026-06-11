using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Weapon combat stats block.
/// Java parity: model/templates/item/WeaponStats.
/// </summary>
public class WeaponStats
{
    [XmlAttribute("min_damage")] public int MinDamage { get; set; }
    [XmlAttribute("max_damage")] public int MaxDamage { get; set; }
    [XmlAttribute("attack_speed")] public int AttackSpeed { get; set; }
    [XmlAttribute("critical")] public int Critical { get; set; }
    [XmlAttribute("physical_accuracy")] public int PhysicalAccuracy { get; set; }
    [XmlAttribute("parry")] public int Parry { get; set; }
    [XmlAttribute("magical_accuracy")] public int MagicalAccuracy { get; set; }
    [XmlAttribute("boost_magical_skill")] public int BoostMagicalSkill { get; set; }
    [XmlAttribute("attack_range")] public int AttackRange { get; set; }
    [XmlAttribute("hit_count")] public int HitCount { get; set; }
    [XmlAttribute("reduce_max")] public int ReduceMax { get; set; }

    public int GetMinDamage() => MinDamage;
    public int GetMaxDamage() => MaxDamage;
    public float GetMeanDamage() => (MinDamage + MaxDamage) / 2f;
    public int GetAttackSpeed() => AttackSpeed;
    public int GetCritical() => Critical;
    public int GetPhysicalAccuracy() => PhysicalAccuracy;
    public int GetParry() => Parry;
    public int GetMagicalAccuracy() => MagicalAccuracy;
    public int GetBoostMagicalSkill() => BoostMagicalSkill;
    public int GetAttackRange() => AttackRange;
    public int GetHitCount() => HitCount;
    public int GetReduceMax() => ReduceMax;
}
