using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/rentusBase/KuharaTheVolatileAI (@author xTz, Estrayl).
/// Still not retail-like and fully refactored.
/// </summary>
[AIName("kuhara_the_volatile")]
public class KuharaTheVolatileAI : AggressiveNpcAI
{
    private ScheduledTask? activeEventTask, barrelEventTask, bombEventTask;
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    private bool canThink = true;

    public KuharaTheVolatileAI(Npc owner)
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
        if (isStarted.CompareAndSet(false, true))
        {
            StartActiveEvent();
            StartBarrelEvent();
        }
    }

    private void CancelTask(ScheduledTask? task)
    {
        if (task != null && !task.IsCancelled)
            task.Cancel(true);
    }

    private void StartBarrelEvent()
    {
        barrelEventTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            switch (Rnd.Get(1, 4))
            {
                case 1:
                    RndSpawn(282394, 126.53f, 274.49f, 209.819f);
                    RndSpawn(282394, 126.53f, 274.49f, 209.819f);
                    break;
                case 2:
                    RndSpawn(282394, 162.22f, 263.89f, 209.819f);
                    RndSpawn(282394, 162.22f, 263.89f, 209.819f);
                    break;
                case 3:
                    RndSpawn(282394, 156.32f, 235.73f, 209.819f);
                    RndSpawn(282394, 156.32f, 235.73f, 209.819f);
                    break;
                case 4:
                    RndSpawn(282394, 119.24f, 245.89f, 209.819f);
                    RndSpawn(282394, 119.24f, 245.89f, 209.819f);
                    break;
            }
            StartBombEvent();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(50000), TimeSpan.FromMilliseconds(50000));
    }

    private void StartBombEvent()
    {
        bombEventTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500394);
                canThink = false;
                EmoteManager.EmoteStopAttacking(GetOwner());
                SetStateIfNot(AIState.WALKING);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19703, 60, GetOwner()).UseNoAnimationSkill();
                SpawnBombEvent();

                bombEventTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (!IsDead())
                    {
                        canThink = true;
                        Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                        if (creature == null)
                        {
                            SetStateIfNot(AIState.FIGHT);
                            Think();
                        }
                        else
                        {
                            GetOwner().GetMoveController().AbortMove();
                            GetOwner().SetTarget(creature);
                            GetOwner().GetGameStats().RenewLastAttackTime();
                            GetOwner().GetGameStats().RenewLastAttackedTime();
                            GetOwner().GetGameStats().RenewLastSkillTime();
                            SetStateIfNot(AIState.FIGHT);
                            HandleMoveValidate();
                            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19375, 60, GetOwner()).UseNoAnimationSkill();
                        }
                        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282394));
                        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282396));
                    }
                    return ValueTask.CompletedTask;
                }, 11000L);
            }
            return ValueTask.CompletedTask;
        }, 14000L);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
                npc.GetController().Delete();
        }
    }

    private void SpawnBombEvent()
    {
        MoveBombToBoss(RndSpawn(282396, 126.53f, 274.49f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 126.53f, 274.49f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 162.22f, 263.89f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 162.22f, 263.89f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 156.32f, 235.73f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 156.32f, 235.73f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 119.24f, 245.89f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 119.24f, 245.89f, 209.819f));
    }

    private void MoveBombToBoss(Npc npc)
    {
        if (!IsDead())
        {
            npc.SetTarget(GetOwner());
            npc.GetMoveController().MoveToTargetObject();
        }
    }

    private Npc RndSpawn(int npcId, float x, float y, float z)
    {
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        float distance = Rnd.Get(0, 4);
        float x1 = (float)(Math.Cos(angleRadians) * distance) + x;
        float y1 = (float)(Math.Sin(angleRadians) * distance) + y;
        return (Npc)Spawn(npcId, x1, y1, z, (sbyte)0);
    }

    private void StartActiveEvent()
    {
        activeEventTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500395);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (!IsDead())
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19704, 60, GetOwner()).UseNoAnimationSkill();
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (!IsDead())
                            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19705, 60, GetOwner()).UseNoAnimationSkill();
                        return ValueTask.CompletedTask;
                    }, 3500L);
                }
                return ValueTask.CompletedTask;
            }, 1000L);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(8000), TimeSpan.FromMilliseconds(14000));
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask(activeEventTask);
        CancelTask(barrelEventTask);
        CancelTask(bombEventTask);
    }

    protected override void HandleBackHome()
    {
        isStarted.Set(false);
        canThink = true;
        CancelTask(activeEventTask);
        CancelTask(barrelEventTask);
        CancelTask(bombEventTask);
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        CancelTask(activeEventTask);
        CancelTask(barrelEventTask);
        CancelTask(bombEventTask);
        WorldPosition p = GetPosition();
        if (p != null)
        {
            DeleteNpcs(p.GetWorldMapInstance().GetNpcs(282394));
            DeleteNpcs(p.GetWorldMapInstance().GetNpcs(282396));
        }
        base.HandleDied();
    }
}
