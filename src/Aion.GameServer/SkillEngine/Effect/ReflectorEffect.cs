using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ReflectorEffect (ginho1, Wakizashi, kecimis, Neon) : ShieldEffect. @XmlAttribute reflectType; hit=hitvalue+hitdelta*skillLevel; AttackShieldObserver 12-arg ctor (minradius,radius,null,0); empty endEffect; getType()→reflectType==1?SKILL_REFLECTOR:REFLECTOR. base fields red-tolerated.</summary>
[XmlType("ReflectorEffect")]
public class ReflectorEffect : ShieldEffect
{
    [XmlAttribute]
    protected int reflectType;

    public override void StartEffect(Effect effect)
    {
        int hit = hitvalue + hitdelta * effect.GetSkillLevel();

        AttackShieldObserver asObserver = new AttackShieldObserver(hit, Value, percent, false, effect, Hittype, GetType_(), HitTypeProb, minradius, radius,
            null, 0);

        effect.AddObserver(effect.GetEffected(), asObserver);
    }

    public override void EndEffect(Effect effect)
    {
    }

    public override ShieldType GetType_()
    {
        return reflectType == 1 ? ShieldType.SKILL_REFLECTOR : ShieldType.REFLECTOR;
    }
}
