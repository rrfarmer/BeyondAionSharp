using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpellAttackInstantEffect (ATracer). @XmlType(name)→[XmlType]; @XmlAccessorType(FIELD) dropped. DamageEffect red-tolerated.</summary>
[XmlType("SpellAttackInstantEffect")]
public class SpellAttackInstantEffect : DamageEffect
{
}
