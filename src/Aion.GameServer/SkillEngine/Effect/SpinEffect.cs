using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SpinEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; calculate: guard if any of PULLED/SPIN/OPENAERIAL/STAGGER/STUMBLE set→return, else SPIN_RESISTANCE + SpellStatus.SPIN; startEffect: cancelCurrentSkill, Player glide/move abort, removeParalyzeEffects, set SPIN; endEffect→unset. StatEnum/AbnormalState/SpellStatus red-tolerated.</summary>
[XmlType("SpinEffect")]
public class SpinEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.PULLED)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.SPIN)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STAGGER)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STUMBLE))
            return;
        base.Calculate(effect, StatEnum.SPIN_RESISTANCE, SpellStatus.SPIN);
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
        effect.GetEffected().GetEffectController().RemoveParalyzeEffects();
        effected.GetEffectController().SetAbnormal(AbnormalState.SPIN);
        effect.SetAbnormal(AbnormalState.SPIN);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SPIN);
    }
}
