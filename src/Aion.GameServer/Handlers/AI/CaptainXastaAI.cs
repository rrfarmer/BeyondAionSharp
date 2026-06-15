using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/rentusBase/CaptainXastaAI (@author xTz).
/// </summary>
[AIName("captain_xasta")]
public class CaptainXastaAI : AggressiveNpcAI
{
    private bool canThink = true;
    private ScheduledTask? phaseTask;
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);

    public CaptainXastaAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            if (GetNpcId() == 217309)
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500388);
                StartPhaseTask(this);
            }
            else
            {
                StartPhase2Task();
            }
        }
    }

    private void CancelPhaseTask()
    {
        if (phaseTask != null && !phaseTask.IsDone())
        {
            phaseTask.Cancel(true);
        }
    }

    private void StartPhaseTask(NpcAI ai)
    {
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                canThink = false;
                EmoteManager.EmoteStopAttacking(GetOwner());
                GetSpawnTemplate().SetWalkerId("B186C8F43FF13FDD50FA9483B7D8C2BEABAE7F5C");
                WalkManager.StartWalking(ai);
                StartRun(GetOwner());
                SpawnHelpers(ai);
                StartSanctuaryEvent();
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(28000), TimeSpan.FromMilliseconds(28000));
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetSpawnTemplate().GetWalkerId() != null)
        {
            GetSpawnTemplate().SetWalkerId(null);
            WalkManager.StopWalking(this);
        }
    }

    private void StartPhase2Task()
    {
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19729, 60, GetOwner()).UseNoAnimationSkill();
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500392);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(30000), TimeSpan.FromMilliseconds(30000));
    }

    private void StartSanctuaryEvent()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                canThink = true;
                Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                if (creature == null)
                {
                    SetStateIfNot(AIState.FIGHT);
                    GetMoveController().AbortMove();
                    OnGeneralEvent(AiEventType.ATTACK_FINISH);
                    OnGeneralEvent(AiEventType.BACK_HOME);
                }
                else
                {
                    GetMoveController().AbortMove();
                    GetOwner().SetTarget(creature);
                    GetOwner().GetGameStats().RenewLastAttackTime();
                    GetOwner().GetGameStats().RenewLastAttackedTime();
                    GetOwner().GetGameStats().RenewLastSkillTime();
                    SetStateIfNot(AIState.FIGHT);
                    HandleMoveValidate();
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19657, 60, GetOwner()).UseNoAnimationSkill();
                }
            }
            return ValueTask.CompletedTask;
        }, 23000L);
    }

    private void StartRun(Npc npc)
    {
        npc.SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
    }

    private void SpawnHelpers(NpcAI ai)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500389);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19968, 60, GetOwner()).UseNoAnimationSkill();
                Npc npc1 = (Npc)Spawn(282604, 263f, 537f, 203f, (sbyte)0);
                Npc npc2 = (Npc)Spawn(282604, 186f, 555f, 203f, (sbyte)0);
                npc1.GetSpawn().SetWalkerId("30028000014");
                WalkManager.StartWalking((NpcAI)npc1.GetAi());
                npc2.GetSpawn().SetWalkerId("30028000015");
                WalkManager.StartWalking((NpcAI)npc2.GetAi());
                StartRun(npc1);
                StartRun(npc2);
            }
            return ValueTask.CompletedTask;
        }, 3000L);
    }

    private void DeleteHelpers()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            DeleteNpcs(instance.GetNpcs(282604));
        }
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
        CancelPhaseTask();
        if (GetNpcId() == 217309)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500390);
            Spawn(217310, 238.160f, 598.624f, 178.480f, (sbyte)0);
            DeleteHelpers();
            AIActions.DeleteOwner(this);
        }
        else
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500391);
            WorldMapInstance instance = GetPosition().GetWorldMapInstance();
            if (instance != null)
            {
                Npc ariana = instance.GetNpc(799668);
                if (ariana != null)
                {
                    ariana.GetEffectController().RemoveEffect(19921);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        ariana.GetSpawn().SetWalkerId("30028000016");
                        WalkManager.StartWalking((NpcAI)ariana.GetAi());
                        return ValueTask.CompletedTask;
                    }, 1000L);
                    PacketSendUtility.BroadcastMessage(ariana, 1500415, 4000);
                    PacketSendUtility.BroadcastMessage(ariana, 1500416, 13000);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(ariana, 19358, 60, ariana).UseNoAnimationSkill();
                        instance.SetDoorState(145, true);
                        DeleteNpcs(instance.GetNpcs(701156));
                        ThreadPoolManager.GetInstance().Schedule(_ => { ariana.GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, 13000L);
                        return ValueTask.CompletedTask;
                    }, 13000L);
                }
            }
        }
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelPhaseTask();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        canThink = true;
        CancelPhaseTask();
        DeleteHelpers();
        isHome.Set(true);
        base.HandleBackHome();
    }
}
