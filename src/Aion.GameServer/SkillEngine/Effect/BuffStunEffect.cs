using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BuffStunEffect (kecimis). @XmlType(name)→[XmlType]; calculate override→addSuccessEffect. StunEffect/Effect red-tolerated.</summary>
[XmlType("BuffStunEffect")]
public class BuffStunEffect : StunEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
