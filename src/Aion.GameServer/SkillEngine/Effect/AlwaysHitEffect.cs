using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/AlwaysHitEffect : EffectTemplate. @XmlType(name)→[XmlType]; empty applyEffect override (Java TODO stub). EffectTemplate/Effect red-tolerated.</summary>
[XmlType("AlwaysHitEffect")]
public class AlwaysHitEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        // TODO Auto-generated method stub
    }
}
