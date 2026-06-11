using Aion.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/BlindEffect (ATracer) : EffectTemplate. applyEffect: visualState &amp; ~BLINKING &lt; HIDE10→removeHideEffects, addToEffectedController; calculate→BLIND_RESISTANCE; startEffect: set BLIND, anonymous AttackStatusObserver(value, DODGE) overriding checkAttackerStatus→nested BlindObserver (Rnd.Chance&lt;Value); endEffect→unset. CreatureVisualState/AttackStatusObserver red-tolerated.</summary>
[XmlType("BlindEffect")]
public class BlindEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        int visualStateExcludingBlinking = effect.GetEffected().GetVisualState() & ~CreatureVisualState.BLINKING.GetId();
        if (visualStateExcludingBlinking < CreatureVisualState.HIDE10.GetId())
            effect.GetEffected().GetEffectController().RemoveHideEffects();
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.BLIND_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        effect.SetAbnormal(AbnormalState.BLIND);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.BLIND);
        effect.AddObserver(effect.GetEffected(), new BlindObserver(Value));
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.BLIND);
    }

    private sealed class BlindObserver : AttackStatusObserver
    {
        public BlindObserver(int value)
            : base(value, AttackStatus.DODGE)
        {
        }

        public override bool CheckAttackerStatus(AttackStatus status)
        {
            return Rnd.Chance() < Value;
        }
    }
}
