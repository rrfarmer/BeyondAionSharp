using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/TiamatWeakenedDragonAI (Cheatkiller, Luzien, Estrayl).</summary>
[AIName("tiamat_weakened_dragon")]
public class TiamatWeakenedDragonAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    protected HpPhases hpPhases = new HpPhases(50, 25, 15, 5);
    protected AtomicBoolean hasAggro = new AtomicBoolean();
    protected List<ScheduledTask> spawnTasks = new List<ScheduledTask>();

    public TiamatWeakenedDragonAI(Npc owner)
        : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (hasAggro.CompareAndSet(false, true))
        {
            OfferAtrocityEvent();
            ScheduleSinkingSand();
        }
        hpPhases.TryEnterNextPhase(this);
    }

    public virtual void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 50:
                ScheduleDivisiveCreations(60000);
                break;
            case 25:
                ScheduleInfinitePain();
                SpawnGravityCrusher();
                break;
            case 15:
            case 5:
                SpawnGravityCrusher();
                break;
        }
    }

    private void OfferAtrocityEvent()
    {
        if (!IsDead() && hasAggro.Get())
        {
            if (GetLifeStats().GetHpPercentage() <= 50)
                GetOwner().QueueSkill(20921, 1, 0);
            GetOwner().QueueSkill(CalculateAtrocitySkillId(), 1);
        }
    }

    protected virtual int CalculateAtrocitySkillId()
    {
        return 20922 + (Rnd.NextInt(3) * 2);
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20922: // Ultimate Atrocity
                Spawn(283237, 445.0f, 550.70f, 417.41f, (sbyte)0);
                Spawn(283241, 469.8f, 543.01f, 417.41f, (sbyte)0);
                break;
            case 20924: // Ultimate Atrocity
                Spawn(283240, 457.8f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283240, 462.3f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283240, 469.7f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283240, 466.6f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283240, 473.6f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283240, 479.3f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283241, 475.8f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283241, 491.2f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283241, 482.7f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283241, 485.2f, 514.6f, 417.41f, (sbyte)0);
                Spawn(283241, 488.1f, 514.6f, 417.41f, (sbyte)0);
                break;
            case 20926: // Ultimate Atrocity
                Spawn(283244, 458.67f, 480.76f, 417.41f, (sbyte)0);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20922: // Ultimate Atrocity
            case 20924:
            case 20926:
                OfferAtrocityEvent();
                break;
        }
    }

    protected virtual void ScheduleSinkingSand()
    {
        spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                int delay = 0;
                for (float degree = -25.0f; degree <= 25.0f; degree += 4)
                {
                    double radian = degree * Math.PI / 180.0;
                    spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(__ => { SpawnSinkingSand(radian); return ValueTask.CompletedTask; }, (long)(delay += 1800)));
                }
                ScheduleSinkingSand();
            }
            return ValueTask.CompletedTask;
        }, 120000L));
    }

    private void SpawnSinkingSand(double radian)
    {
        float dist = 25.0f;
        for (int i = 0; i < 7; i++)
        {
            dist += 2.5f;
            float x = (float)(Math.Cos(radian) * dist);
            float y = (float)(Math.Sin(radian) * dist);
            Spawn(283135, GetPosition().GetX() + x, GetPosition().GetY() + y, GetPosition().GetZ(), (sbyte)0);
        }
    }

    /// <summary>Limited to 3 times.</summary>
    protected virtual void ScheduleDivisiveCreations(int delay)
    {
        spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (hasAggro.Get() && !IsDead() && delay > 30000)
            {
                Spawn(283139, 464.24f, 462.26f, 417.4f, (sbyte)18);
                Spawn(283139, 542.79f, 465.03f, 417.4f, (sbyte)43);
                Spawn(283139, 541.79f, 563.71f, 417.4f, (sbyte)74);
                Spawn(283139, 465.79f, 565.43f, 417.4f, (sbyte)100);
                ScheduleDivisiveCreations(delay - 10000);
            }
            return ValueTask.CompletedTask;
        }, (long)delay));
    }

    protected virtual void SpawnGravityCrusher()
    {
        Spawn(283141, 464.24f, 462.26f, 417.4f, (sbyte)18);
        Spawn(283141, 542.79f, 465.03f, 417.4f, (sbyte)43);
        Spawn(283141, 541.79f, 563.71f, 417.4f, (sbyte)74);
        Spawn(283141, 465.79f, 565.43f, 417.4f, (sbyte)100);
    }

    protected virtual void ScheduleInfinitePain()
    {
        spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (hasAggro.Get() && !IsDead())
            {
                SpawnInfinitePain();
                ScheduleInfinitePain();
            }
            return ValueTask.CompletedTask;
        }, 90000L));
    }

    protected virtual void SpawnInfinitePain()
    {
        Spawn(283143, 508.32f, 515.18f, 417.4f, (sbyte)0);
    }

    protected virtual void DespawnAdds()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(283141));
        DeleteNpcs(instance.GetNpcs(283139));
        DeleteNpcs(instance.GetNpcs(283140));
    }

    protected void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
            if (npc != null)
                npc.GetController().Delete();
    }

    private void CancelTasks()
    {
        foreach (ScheduledTask task in spawnTasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
    }

    protected override void HandleDied()
    {
        CancelTasks();
        DespawnAdds();
        RespawnService.ScheduleDecayTask(GetOwner(), 3000);
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        DespawnAdds();
        CancelTasks();
    }

    protected override void HandleBackHome()
    {
        CancelTasks();
        hasAggro.Set(false);
        DespawnAdds();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_LOOT:
                return false;
            case AIQuestion.CONSIDER_BOUNDS_IN_CAN_SEE_CHECK_WHEN_ATTACKED:
            case AIQuestion.CONSIDER_BOUNDS_IN_CAN_SEE_CHECK_WHEN_ATTACKING:
                return true;
            default:
                return base.Ask(question);
        }
    }
}
