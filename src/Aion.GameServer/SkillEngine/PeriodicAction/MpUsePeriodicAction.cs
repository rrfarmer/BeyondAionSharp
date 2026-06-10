using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.periodicaction;

/// <summary>
/// Java parity: skillengine/periodicaction/MpUsePeriodicAction (antness). Periodically drains MP; ends effect when below required.
/// SM_ATTACK_STATUS.TYPE/LOG faithful. Creature/Effect red-tolerated.
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
        effected.GetLifeStats().ReduceMp(SM_ATTACK_STATUS.TYPE.USED_MP, requiredMp, 0, SM_ATTACK_STATUS.LOG.REGULAR);
    }
}
