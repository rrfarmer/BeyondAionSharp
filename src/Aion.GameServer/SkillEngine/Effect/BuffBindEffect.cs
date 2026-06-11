using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BuffBindEffect (kecimis). @XmlType(name)→[XmlType]; calculate override→addSuccessEffect. BindEffect/Effect red-tolerated.</summary>
[XmlType("BuffBindEffect")]
public class BuffBindEffect : BindEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
