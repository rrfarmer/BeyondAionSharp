using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Network.Aion.ServerPackets.SM_ATTACK_STATUS;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HealCastorOnAttackedEffect : EffectTemplate. @XmlAttribute type(useless)/range; applyEffect→addToEffectedController; startEffect: anonymous ActionObserver(ATTACKED).attacked→nested HealObserver capturing outer+effect: group=effector is Player p?currentGroup:null; healValue=base; no-group→isInRange heal HP, group→per online member isInRange heal HP. HealType/PositionUtil red-tolerated.</summary>
[XmlType("HealCastorOnAttackedEffect")]
public class HealCastorOnAttackedEffect : EffectTemplate
{
    [XmlAttribute]
    protected HealType type; // useless
    [XmlAttribute]
    protected float range;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(), new HealObserver(this, effect));
    }

    private sealed class HealObserver : ActionObserver
    {
        private readonly HealCastorOnAttackedEffect outer;
        private readonly Effect effect;

        public HealObserver(HealCastorOnAttackedEffect outer, Effect effect)
            : base(ObserverType.ATTACKED)
        {
            this.outer = outer;
            this.effect = effect;
        }

        public override void Attacked(Creature creature, int skillId)
        {
            Creature effector = effect.GetEffector();
            var group = effector is Player p ? p.GetCurrentGroup() : null;
            int healValue = outer.CalculateBaseValue(effect);
            if (group == null)
            {
                if (PositionUtil.IsInRange(effect.GetEffected(), effector, outer.range, false))
                    effector.GetLifeStats().IncreaseHp(TYPE.HP, healValue, effect, LOG.REGULAR);
            }
            else
            {
                foreach (Player p in group.GetOnlineMembers())
                {
                    if (PositionUtil.IsInRange(effect.GetEffected(), p, outer.range, false))
                        p.GetLifeStats().IncreaseHp(TYPE.HP, healValue, effect, LOG.REGULAR);
                }
            }
        }
    }
}
