using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpellAtkDrainEffect (Sippolo, kecimis) : AbstractOverTimeEffect. @XmlAttribute(name=)→[XmlAttribute(...)]; drains hp/mp percent of damage to effector. Inherited position/Hoptype/CalculateBaseValue + AttackUtil/SM_ATTACK_STATUS red-tolerated.</summary>
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
        effect.GetEffected().GetController().OnAttack(effect, SmAttackStatus.TYPE.DAMAGE, damage, true, SmAttackStatus.LOG.SPELLATKDRAIN, Hoptype);
        effect.GetEffector().GetObserveController().NotifyAttackObservers(effect.GetEffected(), effect.GetSkillId());

        // Drain (heal) portion of damage inflicted
        if (hpPercent != 0)
        {
            effect.GetEffector().GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.HP, damage * hpPercent / 100, effect, SmAttackStatus.LOG.SPELLATKDRAIN);
        }
        if (mpPercent != 0)
        {
            effect.GetEffector().GetLifeStats().IncreaseMp(SmAttackStatus.TYPE.MP, damage * mpPercent / 100, effect.GetSkillId(), SmAttackStatus.LOG.SPELLATKDRAIN);
        }
    }
}
