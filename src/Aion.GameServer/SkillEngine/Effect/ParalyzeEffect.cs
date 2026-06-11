using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ParalyzeEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; calculate→base.Calculate(effect, PARALYZE_RESISTANCE, null); startEffect: cancelCurrentSkill, Player→onStopGliding+abortMove, set AbnormalState.PARALYZE; endEffect→unset. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("ParalyzeEffect")]
public class ParalyzeEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.PARALYZE_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetMoveController().AbortMove();
        }
        effect.SetAbnormal(AbnormalState.PARALYZE);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.PARALYZE);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.PARALYZE);
    }
}
