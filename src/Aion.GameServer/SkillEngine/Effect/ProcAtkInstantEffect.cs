using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;
using static Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ProcAtkInstantEffect (Wakizashi) : DamageEffect. applyEffect→onAttack(effect, TYPE.DAMAGE, reserveds(Position).Value, true, LOG.PROCATKINSTANT, Hoptype); shouldApplyAttackerMovementModifier→false; calculateBaseValue override: delta==1 && skillTemplate.isProvoked()→Value else base. SmAttackStatus.LOG/TYPE via using static. Effect red-tolerated.</summary>
[XmlType("ProcAtkInstantEffect")]
public class ProcAtkInstantEffect : DamageEffect
{
    public override void ApplyEffect(Effect effect)
    {
        int damage = effect.GetReserveds(Position).GetValue();
        effect.GetEffected().GetController().OnAttack(effect, TYPE.DAMAGE, damage, true, LOG.PROCATKINSTANT, Hoptype);
    }

    public override bool ShouldApplyAttackerMovementModifier()
    {
        return false;
    }

    protected override int CalculateBaseValue(Effect effect)
    {
        if (Delta == 1 && effect.GetSkillTemplate().IsProvoked())
            return Value;
        else
            return base.CalculateBaseValue(effect);
    }
}
