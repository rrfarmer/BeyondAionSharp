using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/GoldenEyeMantutuAI (xTz).</summary>
[AIName("golden_eye_mantutu")]
public class GoldenEyeMantutuAI : AggressiveNpcAI
{
    private bool canThink = true;
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask hungerTask;

    public GoldenEyeMantutuAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleCustomEvent(int eventId, params object[] args)
    {
        if (eventId == 1 && args != null)
        {
            canThink = false;
            GetMoveController().AbortMove();
            EmoteManager.EmoteStopAttacking(GetOwner());
            Npc npc = (Npc)args[0];
            GetOwner().SetTarget(npc);
            SetStateIfNot(AIState.FOLLOWING);
            GetMoveController().MoveToTargetObject();
            GetOwner().SetState(CreatureState.ACTIVE, true);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
        }
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (!canThink)
        {
            VisibleObject target = GetTarget();
            GetMoveController().AbortMove();
            if (target != null && target.IsSpawned() && target is Npc npc)
            {
                if (npc.GetNpcId() == 281128 || npc.GetNpcId() == 281129)
                {
                    StartFeedTime(npc);
                }
            }
        }
    }

    private void StartFeedTime(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && npc != null)
            {
                switch (npc.GetNpcId())
                {
                    case 281128: // Feed Supply Device
                        GetEffectController().RemoveEffect(20489);
                        Spawn(701386, 716.508f, 508.571f, 939.607f, (sbyte)119);
                        break;
                    case 281129: // Water Supply Device
                        GetEffectController().RemoveEffect(20490);
                        Spawn(701387, 716.389f, 494.207f, 939.607f, (sbyte)119);
                        break;
                }

                npc.GetController().Delete();
                canThink = true;
                Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                if (creature == null)
                {
                    SetStateIfNot(AIState.FIGHT);
                    Think();
                }
                else
                {
                    GetOwner().SetTarget(creature);
                    GetOwner().GetGameStats().RenewLastAttackTime();
                    GetOwner().GetGameStats().RenewLastAttackedTime();
                    GetOwner().GetGameStats().RenewLastSkillTime();
                    SetStateIfNot(AIState.FIGHT);
                    HandleMoveValidate();
                }
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(6000));
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            DoSchedule();
        }
    }

    protected override void HandleDespawned()
    {
        CancelHungerTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelHungerTask();
        Npc npc = GetPosition().GetWorldMapInstance().GetNpc(219037);
        if (npc != null && !npc.IsDead())
        {
            npc.GetEffectController().RemoveEffect(18189);
        }

        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelHungerTask();
        GetEffectController().RemoveEffect(20489);
        GetEffectController().RemoveEffect(20490);
        canThink = true;
        isHome.Set(true);
        base.HandleBackHome();
    }

    private void DoSchedule()
    {
        hungerTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            int skill = Rnd.NextBoolean() ? 20489 : 20490; // Hunger / Thirst
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skill, 20, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(30000));
    }

    private void CancelHungerTask()
    {
        if (hungerTask != null && !hungerTask.IsDone())
        {
            hungerTask.Cancel(true);
        }
    }
}
