using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ConvertHealEffect (kecimis) : ShieldEffect. No @XmlType in Java (defaults to class name); @XmlAttribute type (HealType); @XmlAttribute(name="hitpercent")→[XmlAttribute("hitpercent")]; AttackShieldObserver 12-arg ctor (0,0,type,0); empty endEffect; getType()→CONVERT. HealType/base fields red-tolerated.</summary>
[XmlType("ConvertHealEffect")]
public class ConvertHealEffect : ShieldEffect
{
    [XmlAttribute]
    public HealType type;
    [XmlAttribute("hitpercent")]
    public bool hitPercent;

    public override void StartEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int hitValueWithDelta = hitvalue + hitdelta * effect.GetSkillLevel();

        AttackShieldObserver asObserver = new AttackShieldObserver(hitValueWithDelta, valueWithDelta, percent, hitPercent, effect, Hittype, GetType_(),
            HitTypeProb, 0, 0, type, 0);

        effect.AddObserver(effect.GetEffected(), asObserver);
    }

    public override void EndEffect(Effect effect)
    {
    }

    public override ShieldType GetType_()
    {
        return ShieldType.CONVERT;
    }
}
