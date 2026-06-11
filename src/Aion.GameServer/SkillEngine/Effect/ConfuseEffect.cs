using System;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ConfuseEffect (Yeats) : EffectTemplate. applyEffect: removeHideEffects, Player gliding→onStopGliding, addToEffectedController; calculate→CONFUSE_RESISTANCE; startEffect: reflected→originalEffected, cancelCurrentSkill, set CONFUSE, abortMove; Npc→emoteStartAttacking+AIState.CONFUSE, Player WALK_MODE→unset+SM_EMOTION RUN; FEAR_ENABLE→scheduleAtFixedRate ConfuseTask(0,1000ms)→setPeriodicTask(Position); endEffect→unset+abortMove+SM_POSITION, Npc→IDLE+ATTACK. inner Runnable ConfuseTask→nested. ScheduledFuture→ScheduledTask. Many types red-tolerated.</summary>
[XmlType("ConfuseEffect")]
public class ConfuseEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetEffectController().RemoveHideEffects();

        if (effected is Player && ((Player)effected).IsInGlidingState())
        {
            ((Player)effected).GetFlyController().OnStopGliding();
        }
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.CONFUSE_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effector = effect.IsReflected() ? effect.GetOriginalEffected() : effect.GetEffector();
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        effected.GetEffectController().SetAbnormal(AbnormalState.CONFUSE);
        effect.SetAbnormal(AbnormalState.CONFUSE);

        effected.GetMoveController().AbortMove();
        if (effected is Npc)
        {
            EmoteManager.EmoteStartAttacking((Npc)effected, effector);
            effected.GetAi().SetStateIfNot(AIState.CONFUSE);
        }
        else if (effected is Player && effected.IsInState(CreatureState.WALK_MODE))
        {
            effected.UnsetState(CreatureState.WALK_MODE);
            PacketSendUtility.BroadcastPacket((Player)effected, new SM_EMOTION(effected, EmotionType.RUN), true);
        }
        if (GeoDataConfig.FEAR_ENABLE)
        {
            ConfuseTask task = new ConfuseTask(effected);
            ScheduledTask confuseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000));
            effect.SetPeriodicTask(confuseTask, Position);
        }
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.CONFUSE);
        effect.GetEffected().GetMoveController().AbortMove();
        PacketSendUtility.BroadcastPacketAndReceive(effect.GetEffected(), new SM_POSITION(effect.GetEffected()));

        if (effect.GetEffected() is Npc)
        {
            effect.GetEffected().GetAi().SetStateIfNot(AIState.IDLE);
            effect.GetEffected().GetAi().OnCreatureEvent(AIEventType.ATTACK, effect.GetEffected());
        }
    }

    private sealed class ConfuseTask
    {
        private readonly Creature effected;

        public ConfuseTask(Creature effected)
        {
            this.effected = effected;
        }

        public void Run()
        {
            if (effected.GetEffectController().IsConfused())
            {
                float angle = Rnd.NextFloat(360f);
                float maxDistance = effected.GetGameStats().GetMovementSpeedFloat();
                Vector3f closestCollision = GeoService.GetInstance().FindMovementCollision(effected, angle, maxDistance);
                if (effected is Npc)
                {
                    ((Npc)effected).GetMoveController().ResetMove();
                    ((Npc)effected).GetMoveController().MoveToPoint(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
                }
                else
                {
                    byte heading = PositionUtil.ConvertAngleToHeading(angle);
                    effected.GetMoveController().SetNewDirection(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), heading);
                    effected.GetMoveController().StartMovingToDestination();
                }
            }
        }
    }
}
