using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Queen Alukina, Empyrean Crucible. Retail pattern <c>IDArena_S8_Named_3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two corrections against the pattern: her phase steps
/// are 80/55/25 rather than 75/50/25, and killing her bursts seven azure blobbles — which our server
/// spawned nowhere at all.
/// <para>
/// Her rotation is left alone. The pattern addresses seven indices against our seven skills and carries
/// no branch comments, so nothing corroborates which index is which; see the skill-index rule in the
/// fidelity doc. The phase thresholds and the death spawn need no such mapping, which is why they are
/// the parts translated.
/// </para>
/// </remarks>
[AIName("alukina_emp")]
public class QueenAlukinaAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail's steps, read off the boundaries its once-only branches guard: 56-80, 26-55, below 25.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(80, 55, 25);

    private const int AzureBlobble = 280713;

    /// <summary>Retail <c>IDArena_S8_Named_3</c> gives the blobbles thirty seconds.</summary>
    private const int BlobbleLife = 30;
    private const int BlobblesOnDeath = 7;

    private ScheduledTask? task;

    public QueenAlukinaAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTask();
        BurstIntoBlobbles();
        base.HandleDied();
    }

    /// <summary>Seven azure blobbles scatter from her body and live thirty seconds.</summary>
    /// <remarks>
    /// The pattern hangs this on <c>on_killed_by_user</c>, so it is a reward for the kill rather than
    /// cleanup on any despawn -- which is why it sits here and not in <c>HandleDespawned</c>.
    /// </remarks>
    private void BurstIntoBlobbles()
    {
        for (int i = 0; i < BlobblesOnDeath; i++)
        {
            // Behaviourally identical to the hand-written schedule it replaces, which predated
            // SpawnFor. Retail's own thirty seconds, now expressed the same way as everywhere else.
            Expire(RndSpawnInRange(AzureBlobble, 2f), BlobbleLife);
        }
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17899, 41, GetTarget()).UseNoAnimationSkill();

        switch (phaseHpPercent)
        {
            case 80:
                ScheduleSkill(17900, 4500);
                PacketSendUtility.BroadcastMessage(GetOwner(), 340487, 10000);
                ScheduleSkill(17899, 14000);
                ScheduleSkill(17900, 18000);
                break;
            case 55:
                ScheduleSkill(17280, 4500);
                ScheduleSkill(17902, 8000);
                break;
            case 25:
                task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
                {
                    if (IsDead())
                    {
                        CancelTask();
                    }
                    else
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17901, 41, GetTarget()).UseNoAnimationSkill();
                        ScheduleSkill(17902, 5500);
                        ScheduleSkill(17902, 7500);
                    }
                    return ValueTask.CompletedTask;
                }, System.TimeSpan.FromMilliseconds(4500), System.TimeSpan.FromMilliseconds(20000));
                break;
        }
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
            task.Cancel(true);
    }

    private void ScheduleSkill(int skill, int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skill, 41, GetTarget()).UseNoAnimationSkill();
            }
            return ValueTask.CompletedTask;
        }, delay);
    }
}
