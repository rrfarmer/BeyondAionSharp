using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/BuffSilenceEffect (kecimis). @XmlType(name)→[XmlType]; calculate override→addSuccessEffect. SilenceEffect/Effect red-tolerated.</summary>
[XmlType("BuffSilenceEffect")]
public class BuffSilenceEffect : SilenceEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
