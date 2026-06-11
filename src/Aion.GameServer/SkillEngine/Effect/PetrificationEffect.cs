using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/PetrificationEffect (ATracer, kecimis) : EffectTemplate. applyEffect→addToEffectedController; calculate→base.Calculate(effect, PERIFICATION_RESISTANCE, null) [Java enum typo preserved]; startEffect: abortMove, cancelCurrentSkill, Player&&isInGlidingState→onStopGliding, set AbnormalState.PETRIFICATION; endEffect→unset. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("PetrificationEffect")]
public class PetrificationEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.PERIFICATION_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetMoveController().AbortMove();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        // removes glide
        if (effected is Player && ((Player)effected).IsInGlidingState())
        {
            ((Player)effected).GetFlyController().OnStopGliding();
        }
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.PETRIFICATION);
        effect.SetAbnormal(AbnormalState.PETRIFICATION);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.PETRIFICATION);
    }
}
