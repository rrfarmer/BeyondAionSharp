using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/BuffStunEffect (kecimis). @XmlType(name)→[XmlType]; calculate override→addSuccessEffect. StunEffect/Effect red-tolerated.</summary>
[XmlType("BuffStunEffect")]
public class BuffStunEffect : StunEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
