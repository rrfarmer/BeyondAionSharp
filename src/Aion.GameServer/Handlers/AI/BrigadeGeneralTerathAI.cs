using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/BrigadeGeneralTerathAI (@author Cheatkiller).</summary>
[AIName("brigadegeneralterath")]
public class BrigadeGeneralTerathAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(90, 70, 50, 30, 25);
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? skillTask;
    private bool canThink = true;
    private Npc aethericField;
    private bool isGravityEvent;
    private bool isFinalBuff;

    public BrigadeGeneralTerathAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            if (aethericField == null)
            {
                aethericField = (Npc)Spawn(730692, 1030.08f, 1030.08f, 1030.08f, (sbyte)0);
                GetPosition().GetWorldMapInstance().SetDoorState(706, false);
            }
            if (!isGravityEvent)
            {
                StartSkillTask();
            }
        }
        hpPhases.TryEnterNextPhase(this);
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= 25)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20942);
        }
    }

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
                GravityDistortionEvent();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(30000));
    }

    private void CancelskillTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    private void GravityDistortionEvent()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20739, 55, GetOwner()).UseNoAnimationSkill();
        Spawn(283096, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        Spawn(283097, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        Spawn(283098, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20741, 55, GetOwner()).UseNoAnimationSkill(); return ValueTask.CompletedTask; }, 5000L);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        if (isGravityEvent)
            return;
        canThink = false;
        isGravityEvent = true;
        CancelskillTask();
        Spawn(283558, 1056.8f, 297.6f, 409.9f, (sbyte)0); // TODO find Right ID 4.0
        Spawn(283558, 1002.07f, 297.4f, 409.85f, (sbyte)0); // TODO find Right ID 4.0
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20737, 55, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            EmoteManager.EmoteStopAttacking(GetOwner());
            SetStateIfNot(AIState.WALKING);
            GetOwner().GetMoveController().MoveToPoint(GetOwner().GetSpawn().GetX(), GetOwner().GetSpawn().GetY(), GetOwner().GetSpawn().GetZ());
            WalkManager.StartWalking(this);
            GetOwner().SetState(CreatureState.ACTIVE, true);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
            return ValueTask.CompletedTask;
        }, 4000L);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(283109, 1029.9f, 297.26f, 409.08f, (sbyte)0); // 4.0
            Spawn(283110, 1029.93f, 297.31f, 409.08f, (sbyte)0); // 4.0
            return ValueTask.CompletedTask;
        }, 10000L);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead())
                return ValueTask.CompletedTask;
            Despawn();
            GetEffectController().RemoveEffect(20737);
            canThink = true;
            isGravityEvent = false;
            StartSkillTask();
            Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
            if (creature == null)
            {
                SetStateIfNot(AIState.FIGHT);
                Think();
            }
            else
            {
                GetMoveController().AbortMove();
                GetOwner().SetTarget(creature);
                GetOwner().GetGameStats().RenewLastAttackTime();
                GetOwner().GetGameStats().RenewLastAttackedTime();
                GetOwner().GetGameStats().RenewLastSkillTime();
                SetStateIfNot(AIState.WALKING);
                GetOwner().SetState(CreatureState.ACTIVE, true);
                GetOwner().GetMoveController().MoveToTargetObject();
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
            }
            return ValueTask.CompletedTask;
        }, 30000L);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelskillTask();
        aethericField.GetController().Delete();
        GetPosition().GetWorldMapInstance().SetDoorState(706, true);
        Despawn();
    }

    private void Despawn()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(283558)); // TODO find Right ID 4.0
        DeleteNpcs(instance.GetNpcs(283109)); // 4.0
        DeleteNpcs(instance.GetNpcs(283110)); // 4.0
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        isFinalBuff = false;
        CancelskillTask();
        isGravityEvent = false;
        canThink = true;
        isHome.Set(true);
        aethericField.GetController().Delete();
        Despawn();
        GetPosition().GetWorldMapInstance().SetDoorState(706, true);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelskillTask();
    }

    public override bool CanThink()
    {
        return canThink;
    }
}
