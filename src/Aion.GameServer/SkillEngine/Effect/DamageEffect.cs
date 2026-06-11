using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Change;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/DamageEffect (ATracer) abstract : EffectTemplate. @XmlAttribute fields→[XmlAttribute]; nested SM_ATTACK_STATUS.TYPE/LOG qualified; int*=float (lossy compound) preserved. Inherited position/hopType/element/change/CalculateBaseValue + EffectTemplate/AttackUtil red-tolerated.</summary>
[XmlType("DamageEffect")]
public abstract class DamageEffect : EffectTemplate
{
    [XmlAttribute]
    protected Func mode = Func.ADD;
    [XmlAttribute]
    protected bool shared;

    public override void ApplyEffect(Effect effect)
    {
        if (effect.GetSkillTemplate().GetActivationAttribute() == ActivationAttribute.PROVOKED)
        {
            OnAttack(effect, SM_ATTACK_STATUS.TYPE.DAMAGE, SM_ATTACK_STATUS.LOG.PROCATKINSTANT);
        }
        else
        {
            OnAttack(effect, SM_ATTACK_STATUS.TYPE.REGULAR, SM_ATTACK_STATUS.LOG.REGULAR);
            effect.GetEffector().GetObserveController().NotifyAttackObservers(effect.GetEffected(), effect.GetSkillId());
        }
    }

    private void OnAttack(Effect effect, SM_ATTACK_STATUS.TYPE type, SM_ATTACK_STATUS.LOG log)
    {
        effect.GetEffected().GetController().OnAttack(effect, type, effect.GetReserveds(this.position).GetValue(), true, log, hopType);
    }

    public override void CalculateDamage(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        if (element != SkillElement.NONE)
            valueWithDelta *= effect.GetEffector().GetGameStats().GetKnowledge().GetCurrent() / 100f;

        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, false);
    }

    public Func GetMode()
    {
        return mode;
    }

    /// <summary>the shared</summary>
    public bool IsShared()
    {
        return shared;
    }

    /// <summary>
    /// Determines whether movement-based modifiers should be applied to this damage effect during damage calculation.
    /// Specific DamageEffect implementations may override this to exclude themselves from movement-based damage adjustments.
    /// </summary>
    public virtual bool ShouldApplyAttackerMovementModifier()
    {
        return true;
    }
}
