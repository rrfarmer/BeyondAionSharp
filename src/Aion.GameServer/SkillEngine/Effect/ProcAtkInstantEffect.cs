using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;
using static Aion.GameServer.Network.Aion.Serverpackets.SM_ATTACK_STATUS;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ProcAtkInstantEffect (Wakizashi) : DamageEffect. applyEffect→onAttack(effect, TYPE.DAMAGE, reserveds(position).value, true, LOG.PROCATKINSTANT, hopType); shouldApplyAttackerMovementModifier→false; calculateBaseValue override: delta==1 && skillTemplate.isProvoked()→value else base. SM_ATTACK_STATUS.LOG/TYPE via using static. Effect red-tolerated.</summary>
[XmlType("ProcAtkInstantEffect")]
public class ProcAtkInstantEffect : DamageEffect
{
    public override void ApplyEffect(Effect effect)
    {
        int damage = effect.GetReserveds(position).GetValue();
        effect.GetEffected().GetController().OnAttack(effect, TYPE.DAMAGE, damage, true, LOG.PROCATKINSTANT, hopType);
    }

    public override bool ShouldApplyAttackerMovementModifier()
    {
        return false;
    }

    protected override int CalculateBaseValue(Effect effect)
    {
        if (delta == 1 && effect.GetSkillTemplate().IsProvoked())
            return value;
        else
            return base.CalculateBaseValue(effect);
    }
}
