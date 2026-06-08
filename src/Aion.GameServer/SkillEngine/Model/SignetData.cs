using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Per-level signet data (effect prob + damage multiplier). Java parity: skillengine/model/SignetData (@XmlType("signet_data")).</summary>
[XmlType("signet_data")]
public class SignetData
{
    [XmlAttribute("lvl")] public int Level { get; set; }
    [XmlAttribute("add_effect_prob")] public int AddEffectProb { get; set; } = 1;
    [XmlAttribute("dmg_multi")] public float DamageMultiplier { get; set; }

    public int GetLevel() => Level;
    public int GetAddEffectProb() => AddEffectProb;
    public float GetDamageMultiplier() => DamageMultiplier;
}
