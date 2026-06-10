using System;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/FearEffect (Sarynth) : EffectTemplate. @XmlAttribute resistchance=100; applyEffect: removeHideEffects, Player gliding→onStopGliding, addToEffectedController; calculate→FEAR_RESISTANCE; startEffect: reflected→originalEffected, cancelCurrentSkill, set FEAR, abortMove; Npc→emoteStartAttacking+AIState.FEAR, Player WALK_MODE→unset+SM_EMOTION RUN; FEAR_ENABLE→scheduleAtFixedRate FearTask(0,1000)→setPeriodicTask(Position); resistchance&lt;100→anonymous ATTACKED observer→nested FearResistObserver (Rnd.Chance>=resistchance→removeEffect); endEffect→unset+abortMove+SM_POSITION, Npc→IDLE+ATTACK. inner Runnable FearTask→nested (isUnderFear+isInRange 40, calculateAngleFrom, findMovementCollision). Many types red-tolerated.</summary>
[XmlType("FearEffect")]
public class FearEffect : EffectTemplate
{
    [XmlAttribute]
    protected int resistchance = 100;

    public override void ApplyEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetEffectController().RemoveHideEffects();
        // Fear stops gliding
        if (effected is Player && ((Player)effected).IsInGlidingState())
        {
            ((Player)effected).GetFlyController().OnStopGliding();
        }

        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.FEAR_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effector = effect.IsReflected() ? effect.GetOriginalEffected() : effect.GetEffector();
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effector);
        effect.SetAbnormal(AbnormalState.FEAR);
        effected.GetEffectController().SetAbnormal(AbnormalState.FEAR);

        effected.GetMoveController().AbortMove();

        if (effected is Npc)
        {
            EmoteManager.EmoteStartAttacking((Npc)effected, effector); // set weapon_equipped for faster walk speed
            effected.GetAi().SetStateIfNot(AIState.FEAR);
        }
        else if (effected is Player && effected.IsInState(CreatureState.WALK_MODE))
        {
            effected.UnsetState(CreatureState.WALK_MODE);
            PacketSendUtility.BroadcastPacket((Player)effected, new SM_EMOTION(effected, EmotionType.RUN), true);
        }
        if (GeoDataConfig.FEAR_ENABLE)
        {
            FearTask task = new FearTask(effector, effected);
            ScheduledTask fearTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000));
            effect.SetPeriodicTask(fearTask, Position);
        }

        // resistchance of fear effect to damage, if value is lower than 100, fear can be interrupted bz damage
        // example skillId: 540 Terrible howl
        if (resistchance < 100)
        {
            effect.AddObserver(effected, new FearResistObserver(this, effect, effected));
        }
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.FEAR);

        effect.GetEffected().GetMoveController().AbortMove();
        PacketSendUtility.BroadcastPacketAndReceive(effect.GetEffected(), new SM_POSITION(effect.GetEffected()));

        if (effect.GetEffected() is Npc)
        {
            effect.GetEffected().GetAi().SetStateIfNot(AIState.IDLE);
            effect.GetEffected().GetAi().OnCreatureEvent(AIEventType.ATTACK, effect.GetEffected());
        }
    }

    private sealed class FearResistObserver : ActionObserver
    {
        private readonly FearEffect outer;
        private readonly Effect effect;
        private readonly Creature effected;

        public FearResistObserver(FearEffect outer, Effect effect, Creature effected)
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

    private sealed class FearTask
    {
        private readonly Creature effector;
        private readonly Creature effected;

        public FearTask(Creature effector, Creature effected)
        {
            this.effector = effector;
            this.effected = effected;
        }

        public void Run()
        {
            if (effected.GetEffectController().IsUnderFear() && PositionUtil.IsInRange(effected, effector, 40))
            {
                float angle = PositionUtil.CalculateAngleFrom(effector, effected);
                float maxDistance = effected.GetGameStats().GetMovementSpeedFloat();
                Vector3f closestCollision = GeoService.GetInstance().FindMovementCollision(effected, angle, maxDistance);
                if (effected is Npc)
                {
                    ((Npc)effected).GetMoveController().ResetMove();
                    ((Npc)effected).GetMoveController().MoveToPoint(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
                }
                else
                {
                    byte moveAwayHeading = PositionUtil.ConvertAngleToHeading(angle);
                    effected.GetMoveController().SetNewDirection(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), moveAwayHeading);
                    effected.GetMoveController().StartMovingToDestination();
                }
            }
        }
    }
}
