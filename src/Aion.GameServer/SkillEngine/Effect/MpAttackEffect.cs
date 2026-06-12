using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/MpAttackEffect (Sippolo) : AbstractOverTimeEffect. onPeriodicAction reduces MP (percent-aware). Inherited Value/percent + SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("MpAttackEffect")]
public class MpAttackEffect : AbstractOverTimeEffect
{
    // TODO bosses are resistent to this?

    public override void OnPeriodicAction(Effect effect)
    {
        int maxMP = effect.GetEffected().GetLifeStats().GetMaxMp();
        int newValue = Value;
        // Support for values in percentage
        if (percent)
            newValue = (maxMP * Value) / 100;
        // sm_attack_status for type and log - 4.5 checked
        effect.GetEffected().GetLifeStats().ReduceMp(SmAttackStatus.TYPE.DAMAGE_MP, newValue, effect.GetSkillId(), SmAttackStatus.LOG.MPATTACK);
    }
}
