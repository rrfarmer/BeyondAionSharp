using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ShieldEffect (ATracer, Wakizashi, Sippolo, kecimis) : EffectTemplate. @XmlAttribute fields→[XmlAttribute]; getType()→GetType_() (Object.GetType collision); base Hittype/HitTypeProb/CalculateBaseValue from EffectTemplate; new AttackShieldObserver(hit,total,percent,effect,hitType,getType(),hitTypeProb). AttackShieldObserver/ShieldType red-tolerated.</summary>
[XmlType("ShieldEffect")]
public class ShieldEffect : EffectTemplate
{
    [XmlAttribute]
    public int hitdelta;
    [XmlAttribute]
    public int hitvalue;
    [XmlAttribute]
    public bool percent;
    [XmlAttribute]
    public int radius = 0;
    [XmlAttribute]
    public int minradius = 0;

    public override void ApplyEffect(Effect effect)
    {
        // check for condition race, skillId: 10317,10318, implemented as RaceCondition
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int hitValueWithDelta = hitvalue + hitdelta * effect.GetSkillLevel();

        AttackShieldObserver asObserver = new AttackShieldObserver(hitValueWithDelta, valueWithDelta, percent, effect, Hittype, GetType_(), HitTypeProb);
        effect.AddObserver(effect.GetEffected(), asObserver);
    }

    public virtual ShieldType GetType_()
    {
        return ShieldType.NORMAL;
    }
}
