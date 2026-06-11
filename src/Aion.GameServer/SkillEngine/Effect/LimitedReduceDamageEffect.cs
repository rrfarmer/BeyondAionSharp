using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/LimitedReduceDamageEffect : EffectTemplate. @XmlType(name)→[XmlType]; empty applyEffect override (Java TODO stub). EffectTemplate/Effect red-tolerated.</summary>
[XmlType("LimitedReduceDamageEffect")]
public class LimitedReduceDamageEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        // TODO Auto-generated method stub
    }
}
