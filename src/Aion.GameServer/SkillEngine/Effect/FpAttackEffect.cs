using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/FpAttackEffect (Sippolo) : AbstractOverTimeEffect. instanceof Player→is Player; onPeriodicAction reduces FP. Inherited value/percent + SM_ATTACK_STATUS red-tolerated.</summary>
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
        int newValue = value;
        // Support for values in percentage
        if (percent)
            newValue = (maxFP * value) / 100;
        effected.GetLifeStats().ReduceFp(SM_ATTACK_STATUS.TYPE.FP_DAMAGE, newValue, effect.GetSkillId(), SM_ATTACK_STATUS.LOG.FPATTACK);
    }
}
