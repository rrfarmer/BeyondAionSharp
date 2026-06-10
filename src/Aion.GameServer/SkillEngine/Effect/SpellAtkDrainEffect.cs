using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SpellAtkDrainEffect (Sippolo, kecimis) : AbstractOverTimeEffect. @XmlAttribute(name=)→[XmlAttribute(...)]; drains hp/mp percent of damage to effector. Inherited position/hopType/CalculateBaseValue + AttackUtil/SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("SpellAtkDrainEffect")]
public class SpellAtkDrainEffect : AbstractOverTimeEffect
{
    [XmlAttribute("hp_percent")]
    private int hpPercent;
    [XmlAttribute("mp_percent")]
    private int mpPercent;

    public override void OnPeriodicAction(Effect effect)
    {
        int valueWithDelta = CalculateBaseValue(effect);
        int damage = AttackUtil.CalculateMagicalOverTimeSkillResult(effect, valueWithDelta, this, true);
        effect.GetEffected().GetController().OnAttack(effect, SM_ATTACK_STATUS.TYPE.DAMAGE, damage, true, SM_ATTACK_STATUS.LOG.SPELLATKDRAIN, hopType);
        effect.GetEffector().GetObserveController().NotifyAttackObservers(effect.GetEffected(), effect.GetSkillId());

        // Drain (heal) portion of damage inflicted
        if (hpPercent != 0)
        {
            effect.GetEffector().GetLifeStats().IncreaseHp(SM_ATTACK_STATUS.TYPE.HP, damage * hpPercent / 100, effect, SM_ATTACK_STATUS.LOG.SPELLATKDRAIN);
        }
        if (mpPercent != 0)
        {
            effect.GetEffector().GetLifeStats().IncreaseMp(SM_ATTACK_STATUS.TYPE.MP, damage * mpPercent / 100, effect.GetSkillId(), SM_ATTACK_STATUS.LOG.SPELLATKDRAIN);
        }
    }
}
