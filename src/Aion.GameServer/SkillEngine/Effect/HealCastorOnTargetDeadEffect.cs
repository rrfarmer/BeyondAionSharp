using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Network.Aion.ServerPackets.SM_ATTACK_STATUS;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HealCastorOnTargetDeadEffect (Sippolo) : EffectTemplate. @XmlAttribute type(useless)/range/healparty; applyEffect→addToEffectedController; endEffect: effected.isDead→healValue=base; group=healparty&&effector is Player p?currentGroup:null; no-group→isInRange heal effector HP, group→per online member isInRange heal effector HP. HealType/PositionUtil red-tolerated.</summary>
[XmlType("HealCastorOnTargetDeadEffect")]
public class HealCastorOnTargetDeadEffect : EffectTemplate
{
    [XmlAttribute]
    protected HealType type; // useless
    [XmlAttribute]
    protected float range;
    [XmlAttribute]
    protected bool healparty;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void EndEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        Creature effector = effect.GetEffector();
        if (effected.IsDead())
        {
            int healValue = CalculateBaseValue(effect);
            var group = healparty && effector is Player p ? p.GetCurrentGroup() : null;
            if (group == null)
            {
                if (PositionUtil.IsInRange(effected, effector, range, false))
                    effector.GetLifeStats().IncreaseHp(TYPE.HP, healValue, effect, LOG.REGULAR);
            }
            else
            {
                foreach (Player p in group.GetOnlineMembers())
                {
                    if (PositionUtil.IsInRange(effected, p, range, false))
                        effector.GetLifeStats().IncreaseHp(TYPE.HP, healValue, effect, LOG.REGULAR);
                }
            }
        }
    }
}
