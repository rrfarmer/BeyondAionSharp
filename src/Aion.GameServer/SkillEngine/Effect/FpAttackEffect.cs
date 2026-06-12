using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/FpAttackEffect (Sippolo) : AbstractOverTimeEffect. instanceof Player→is Player; onPeriodicAction reduces FP. Inherited Value/percent + SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("FpAttackEffect")]
public class FpAttackEffect : AbstractOverTimeEffect
{
    public override void Calculate(Effect effect)
    {
        // Only players have FP
        if (effect.GetEffected() is Player)
            base.Calculate(effect, null, null);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        Player effected = (Player)effect.GetEffected();
        int maxFP = effected.GetLifeStats().GetMaxFp();
        int newValue = Value;
        // Support for values in percentage
        if (percent)
            newValue = (maxFP * Value) / 100;
        effected.GetLifeStats().ReduceFp(SmAttackStatus.TYPE.FP_DAMAGE, newValue, effect.GetSkillId(), SmAttackStatus.LOG.FPATTACK);
    }
}
