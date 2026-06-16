using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HealCastorOnTargetDeadEffect (Sippolo) : EffectTemplate. @XmlAttribute type(useless)/range/healparty; applyEffect→addToEffectedController; endEffect: effected.isDead→healValue=base; group=healparty&&effector is Player p?currentGroup:null; no-group→isInRange heal effector HP, group→per online member isInRange heal effector HP. HealType/PositionUtil red-tolerated.</summary>
[XmlType("HealCastorOnTargetDeadEffect")]
public class HealCastorOnTargetDeadEffect : EffectTemplate
{
    [XmlAttribute]
    public HealType type; // useless
    [XmlAttribute]
    public float range;
    [XmlAttribute]
    public bool healparty;

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
                foreach (Player member in group.GetOnlineMembers())
                {
                    if (PositionUtil.IsInRange(effected, member, range, false))
                        effector.GetLifeStats().IncreaseHp(TYPE.HP, healValue, effect, LOG.REGULAR);
                }
            }
        }
    }
}
