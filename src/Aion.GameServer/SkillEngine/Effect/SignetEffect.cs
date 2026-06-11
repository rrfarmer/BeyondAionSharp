using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SignetEffect : EffectTemplate. applyEffect→AddToEffectedController; calculate→AddSuccessEffect(this). EffectTemplate/Effect red-tolerated.</summary>
[XmlType("SignetEffect")]
public class SignetEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
