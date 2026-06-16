using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/FpAttackInstantEffect (Sippolo) : EffectTemplate. @XmlAttribute percent; calculate: Player-only, maxFP, percent→(maxFP*Value)/100, setReserveds(new EffectReserved(Position, newValue, FP, true), false), base.Calculate(effect,null,null); applyEffect: Player-only, reduceFp(TYPE.FP_DAMAGE, reserveds(Position), skillId, LOG.FPATTACK). EffectReserved/ResourceType red-tolerated.</summary>
[XmlType("FpAttackInstantEffect")]
public class FpAttackInstantEffect : EffectTemplate
{
    [XmlAttribute]
    public bool percent;

    public override void Calculate(Effect effect)
    {
        // Only players have FP
        if (effect.GetEffected() is Player)
        {
            Player player = (Player)effect.GetEffected();
            int maxFP = player.GetLifeStats().GetMaxFp();
            int newValue = Value;
            // Support for values in percentage
            if (percent)
                newValue = (maxFP * Value) / 100;

            effect.SetReserveds(new EffectReserved(Position, newValue, EffectReserved.ResourceType.FP, true), false);

            base.Calculate(effect, null, null);
        }
    }

    public override void ApplyEffect(Effect effect)
    {
        // Restriction to players because lack of FP on other Creatures
        if (!(effect.GetEffected() is Player))
            return;
        Player player = (Player)effect.GetEffected();
        player.GetLifeStats().ReduceFp(SmAttackStatus.TYPE.FP_DAMAGE, effect.GetReserveds(Position).GetValue(), effect.GetSkillId(), SmAttackStatus.LOG.FPATTACK);
    }
}
