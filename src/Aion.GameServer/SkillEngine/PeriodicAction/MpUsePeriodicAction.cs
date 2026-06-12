using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.PeriodicAction;

/// <summary>
/// Java parity: skillengine/periodicaction/MpUsePeriodicAction (antness). Periodically drains MP; ends effect when below required.
/// SmAttackStatus.TYPE/LOG faithful. Creature/Effect red-tolerated.
/// </summary>
public class MpUsePeriodicAction : PeriodicAction
{
    [XmlAttribute("value")]
    protected int value;

    [XmlAttribute("ratio")]
    protected bool ratio;

    public override void Act(Effect effect)
    {
        Creature effected = effect.GetEffected();
        int maxMp = effected.GetGameStats().GetMaxMp().GetCurrent();
        int requiredMp = ratio ? (int)(maxMp * (value / 100f)) : value;
        if (effected.GetLifeStats().GetCurrentMp() < requiredMp)
        {
            effect.EndEffect();
            return;
        }
        effected.GetLifeStats().ReduceMp(SmAttackStatus.TYPE.USED_MP, requiredMp, 0, SmAttackStatus.LOG.REGULAR);
    }
}
