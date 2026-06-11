using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/SupportEventEffect : EffectTemplate. @XmlType(name)→[XmlType]; empty applyEffect override (Java TODO stub). EffectTemplate/Effect red-tolerated.</summary>
[XmlType("SupportEventEffect")]
public class SupportEventEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        // TODO Auto-generated method stub
    }
}
