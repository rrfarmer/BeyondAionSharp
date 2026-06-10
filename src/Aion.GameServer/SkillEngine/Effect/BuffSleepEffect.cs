using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/BuffSleepEffect (kecimis) : SleepEffect. calculate→addSuccessEffect(this); startEffect: cancelCurrentSkill(effector), setAbnormal(SLEEP) on effect + effectController. AbnormalState red-tolerated.</summary>
[XmlType("BuffSleepEffect")]
public class BuffSleepEffect : SleepEffect
{
    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        effect.SetAbnormal(AbnormalState.SLEEP);
        effected.GetEffectController().SetAbnormal(AbnormalState.SLEEP);
    }
}
