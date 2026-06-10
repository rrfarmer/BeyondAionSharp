using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/MpAttackEffect (Sippolo) : AbstractOverTimeEffect. onPeriodicAction reduces MP (percent-aware). Inherited value/percent + SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("MpAttackEffect")]
public class MpAttackEffect : AbstractOverTimeEffect
{
    // TODO bosses are resistent to this?

    public override void OnPeriodicAction(Effect effect)
    {
        int maxMP = effect.GetEffected().GetLifeStats().GetMaxMp();
        int newValue = value;
        // Support for values in percentage
        if (percent)
            newValue = (maxMP * value) / 100;
        // sm_attack_status for type and log - 4.5 checked
        effect.GetEffected().GetLifeStats().ReduceMp(SM_ATTACK_STATUS.TYPE.DAMAGE_MP, newValue, effect.GetSkillId(), SM_ATTACK_STATUS.LOG.MPATTACK);
    }
}
