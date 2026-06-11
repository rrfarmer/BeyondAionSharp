using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/FlyoffEffect (Rolandas) : EffectTemplate. @XmlType(name="FlyOffEffect") (differs from class name); @XmlAttribute int distance. EffectTemplate/Effect red-tolerated.</summary>
[XmlType("FlyOffEffect")]
public class FlyoffEffect : EffectTemplate
{
    [XmlAttribute]
    protected int distance;

    public override void ApplyEffect(Effect effect)
    {
        // TODO Distance is Z, value probably contains angle or width
    }
}
