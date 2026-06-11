using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/MPShieldEffect (Cheatkiller) : ShieldEffect. @XmlAttribute(name="mp_value")→[XmlAttribute("mp_value")]; AttackShieldObserver 8-arg ctor (mpValue overload); empty endEffect; getType()→MPSHIELD. base fields red-tolerated.</summary>
[XmlType("MPShieldEffect")]
public class MPShieldEffect : ShieldEffect
{
    [XmlAttribute("mp_value")]
    protected int mpValue;

    public override void StartEffect(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int hitValueWithDelta = hitvalue + hitdelta * effect.GetSkillLevel();
        AttackShieldObserver asObserver = new AttackShieldObserver(hitValueWithDelta, valueWithDelta, percent, effect, Hittype, GetType_(), HitTypeProb,
            mpValue);
        effect.AddObserver(effect.GetEffected(), asObserver);
    }

    public override void EndEffect(Effect effect)
    {
    }

    public override ShieldType GetType_()
    {
        return ShieldType.MPSHIELD;
    }
}
