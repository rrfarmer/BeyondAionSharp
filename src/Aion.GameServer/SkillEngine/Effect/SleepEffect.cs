using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SleepEffect (ATracer) : EffectTemplate. **Converges BuffSleepEffect.** super.calculate→base.Calculate; instanceof Player→is Player; sets/unsets AbnormalState.SLEEP, setCancelOnDmg(true). EffectTemplate/Effect/StatEnum red-tolerated.</summary>
[XmlType("SleepEffect")]
public class SleepEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.SLEEP_RESISTANCE, null);
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
        effect.SetAbnormal(AbnormalState.SLEEP);
        effected.GetEffectController().SetAbnormal(AbnormalState.SLEEP);
        effect.SetCancelOnDmg(true);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SLEEP);
    }
}
