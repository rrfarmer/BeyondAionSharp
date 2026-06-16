using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.PeriodicAction;

/// <summary>
/// Java parity: skillengine/periodicaction/HpUsePeriodicAction (antness). Periodically drains HP; ends effect when below required.
/// SmAttackStatus.TYPE/LOG faithful. Creature/Effect red-tolerated.
/// </summary>
public class HpUsePeriodicAction : PeriodicAction
{
    [XmlAttribute("value")]
    public int value;

    [XmlAttribute("delta")]
    public int delta;

    [XmlAttribute("ratio")]
    public bool ratio;

    public override void Act(Effect effect)
    {
        Creature effected = effect.GetEffected();
        int maxHp = effected.GetGameStats().GetMaxHp().GetCurrent();
        int requiredHp = ratio ? (int)(maxHp * (value / 100f)) : value;
        if (effected.GetLifeStats().GetCurrentHp() < requiredHp)
        {
            effect.EndEffect();
            return;
        }
        effected.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.USED_HP, requiredHp, 0, SmAttackStatus.LOG.REGULAR, effected);
    }
}
