using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.PeriodicAction;

/// <summary>
/// Java parity: skillengine/periodicaction/HpUsePeriodicAction (antness). Periodically drains HP; ends effect when below required.
/// SM_ATTACK_STATUS.TYPE/LOG faithful. Creature/Effect red-tolerated.
/// </summary>
public class HpUsePeriodicAction : PeriodicAction
{
    [XmlAttribute("value")]
    protected int value;

    [XmlAttribute("delta")]
    protected int delta;

    [XmlAttribute("ratio")]
    protected bool ratio;

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
        effected.GetLifeStats().ReduceHp(SM_ATTACK_STATUS.TYPE.USED_HP, requiredHp, 0, SM_ATTACK_STATUS.LOG.REGULAR, effected);
    }
}
