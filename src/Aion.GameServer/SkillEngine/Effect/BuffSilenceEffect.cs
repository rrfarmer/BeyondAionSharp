using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BuffSilenceEffect (kecimis). @XmlType(name)→[XmlType]; calculate override→addSuccessEffect. SilenceEffect/Effect red-tolerated.</summary>
[XmlType("BuffSilenceEffect")]
public class BuffSilenceEffect : SilenceEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
