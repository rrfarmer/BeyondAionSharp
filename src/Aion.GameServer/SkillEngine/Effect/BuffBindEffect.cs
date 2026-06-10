using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/BuffBindEffect (kecimis). @XmlType(name)→[XmlType]; calculate override→addSuccessEffect. BindEffect/Effect red-tolerated.</summary>
[XmlType("BuffBindEffect")]
public class BuffBindEffect : BindEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
