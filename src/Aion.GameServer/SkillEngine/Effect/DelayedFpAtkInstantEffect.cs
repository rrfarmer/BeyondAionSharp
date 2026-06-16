using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DelayedFpAtkInstantEffect (kecimis) : EffectTemplate (no @XmlType→defaults to class name). @XmlAttribute delay/percent; calculate: Player-only→base.Calculate(effect,null,null); applyEffect: anonymous Runnable→async delegate at delay ms→calculateAndApplyDamage; calculateAndApplyDamage: isEnemy guard, base value, maxFP, percent→(maxFP*v)/100, reduceFp(FP_DAMAGE, newValue, skillId, LOG.FPATTACK). Effect/Player red-tolerated.</summary>
[XmlType("DelayedFpAtkInstantEffect")]
public class DelayedFpAtkInstantEffect : EffectTemplate
{
    [XmlAttribute]
    public int delay;
    [XmlAttribute]
    public bool percent;

    public override void Calculate(Effect effect)
    {
        // Only players have FP
        if (effect.GetEffected() is Player)
            base.Calculate(effect, null, null);
    }

    public override void ApplyEffect(Effect effect)
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            CalculateAndApplyDamage(effect);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay));
    }

    private void CalculateAndApplyDamage(Effect effect)
    {
        if (!effect.GetEffector().IsEnemy(effect.GetEffected()))
            return;
        int valueWithDelta = CalculateBaseValue(effect);
        Player player = (Player)effect.GetEffected();
        int maxFP = player.GetLifeStats().GetMaxFp();

        int newValue = valueWithDelta;
        // Support for values in percentage
        if (percent)
            newValue = (maxFP * valueWithDelta) / 100;

        player.GetLifeStats().ReduceFp(TYPE.FP_DAMAGE, newValue, effect.GetSkillId(), LOG.FPATTACK);
    }
}
