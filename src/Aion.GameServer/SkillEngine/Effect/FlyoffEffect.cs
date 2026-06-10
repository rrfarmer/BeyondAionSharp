using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

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
