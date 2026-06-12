using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Change;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DamageEffect (ATracer) abstract : EffectTemplate. @XmlAttribute fields→[XmlAttribute]; nested SmAttackStatus.TYPE/LOG qualified; int*=float (lossy compound) preserved. Inherited position/Hoptype/Element/change/CalculateBaseValue + EffectTemplate/AttackUtil red-tolerated.</summary>
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
            OnAttack(effect, SmAttackStatus.TYPE.DAMAGE, SmAttackStatus.LOG.PROCATKINSTANT);
        }
        else
        {
            OnAttack(effect, SmAttackStatus.TYPE.REGULAR, SmAttackStatus.LOG.REGULAR);
            effect.GetEffector().GetObserveController().NotifyAttackObservers(effect.GetEffected(), effect.GetSkillId());
        }
    }

    private void OnAttack(Effect effect, SmAttackStatus.TYPE type, SmAttackStatus.LOG log)
    {
        effect.GetEffected().GetController().OnAttack(effect, type, effect.GetReserveds(this.Position).GetValue(), true, log, Hoptype);
    }

    public override void CalculateDamage(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        if (Element != SkillElement.NONE)
            valueWithDelta = (int)(valueWithDelta * (effect.GetEffector().GetGameStats().GetKnowledge().GetCurrent() / 100f));

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
