using Aion.GameServer.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/RootEffect (ATracer) : EffectTemplate. @XmlAttribute resistchance=100; calculate→ROOT_RESISTANCE; startEffect: set ROOT, Player glide/move abort, anonymous ActionObserver(ATTACKED)→nested RootResistObserver capturing outer+effect+effected: Rnd.Chance>=resistchance→removeEffect; endEffect→unset. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("RootEffect")]
public class RootEffect : EffectTemplate
{
    [XmlAttribute]
    protected int resistchance = 100;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.ROOT_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetEffectController().SetAbnormal(AbnormalState.ROOT);
        effect.SetAbnormal(AbnormalState.ROOT);
        // PacketSendUtility.broadcastPacketAndReceive(effected, new SM_POSITION(effected));
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetMoveController().AbortMove();
        }

        effect.AddObserver(effected, new RootResistObserver(this, effect, effected));
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.ROOT);
    }

    private sealed class RootResistObserver : ActionObserver
    {
        private readonly RootEffect outer;
        private readonly Effect effect;
        private readonly Creature effected;

        public RootResistObserver(RootEffect outer, Effect effect, Creature effected)
            : base(ObserverType.ATTACKED)
        {
            this.outer = outer;
            this.effect = effect;
            this.effected = effected;
        }

        public override void Attacked(Creature creature, int skillId)
        {
            if (Rnd.Chance() >= outer.resistchance)
                effected.GetEffectController().RemoveEffect(effect.GetSkillId());
        }
    }
}
