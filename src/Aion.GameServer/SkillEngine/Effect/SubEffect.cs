using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>A secondary skill effect triggered by a parent effect. Java parity: skillengine/effect/SubEffect (@XmlType("SubEffect")).</summary>
[XmlType("SubEffect")]
public class SubEffect
{
    [XmlAttribute("skill_id")] public int SkillId { get; set; }
    [XmlAttribute("chance")] public int Chance { get; set; } = 100;
    [XmlAttribute("addeffect")] public bool AddEffect { get; set; }

    public int GetSkillId() => SkillId;
    public int GetChance() => Chance;
    public bool IsAddEffect() => AddEffect;
}
