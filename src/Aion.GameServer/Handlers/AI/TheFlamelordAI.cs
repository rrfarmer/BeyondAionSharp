using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Flamelord, Raksang Ruins. Retail pattern Raksha_Firemage_Nmd (217451).
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. This replaces an HP-phase ladder with the four
/// battle timers the pattern actually runs. aionemu delivered its executors in bursts at an invented
/// 40/30/20/10 — one, then two, then three, then four — whereas retail delivers them continuously on
/// a 25s rotation and only thickens the wave below 25% HP. It also never spawned Torment Blaze at
/// all, which sat in npc_templates with a skill and nothing to bring it into the world.
/// <para>
/// The four timers: a 9s beat casting Blazing Cut; a 7s beat carrying three one-shot HP steps at
/// 75/50/25; a 20s flame timer that casts and spawns a Torment Blaze; and the 25s delivery rotation.
/// </para>
/// <para>
/// Skill indices come from our list order, which the pattern's usage corroborates: index 0 is the
/// only entry with a nonzero probability and is what the 9s beat repeats. The delivery tick's other
/// cast is index 4, beyond our four-entry list, so it is not reproduced — noted rather than guessed.
/// </para>
/// </remarks>
[AIName("the_flamelord")]
public class TheFlamelordAI : AggressiveNpcAI
{
    /// <summary>Blazing Cut: the 9s beat.</summary>
    private const int BlazingCut = 19923;

    /// <summary>Cast on each of the three one-shot HP steps, and on every delivery tick.</summary>
    private const int FlameBurst = 19925;

    /// <summary>Cast alongside each Torment Blaze.</summary>
    private const int FlameSummon = 19924;

    private const int TormentBlaze = 282459;

    /// <summary>Delivery executors, spawned in rotation. Each walks to its own target brazier.</summary>
    private static readonly int[] DeliveryExecutors = { 282451, 282452, 282453, 282454 };

    /// <summary>Brazier each executor walks to, by the same index.</summary>
    private static readonly int[] DeliveryTargets = { 701062, 701063, 701064, 701065 };

    /// <summary>Where the executors enter, unchanged from the previous implementation.</summary>
    private const float DeliveryX = 802.845f;
    private const float DeliveryY = 964.903f;
    private const float DeliveryZ = 792.102f;

    /// <summary>One-shot HP steps carried by the 7s beat.</summary>
    private static readonly int[] BurstSteps = { 75, 50, 25 };

    /// <summary>Below this, a delivery tick sends more than one executor.</summary>
    private const int ThickenBelowHp = 25;

    private readonly object stateLock = new object();
    private int burstStepsTaken;
    private int deliveryIndex;

    private ScheduledTask? beatTask;
    private ScheduledTask? burstTask;
    private ScheduledTask? flameTask;
    private ScheduledTask? deliveryTask;

    public TheFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        StartFight();
    }

    private void StartFight()
    {
        if (beatTask != null)
            return;

        NpcSkillCasting.QueueAtDataLevel(GetOwner(), BlazingCut, NpcSkillTargetAttribute.MOST_HATED);

        beatTask = Repeat(9000, OnBeatTick);
        burstTask = Repeat(7000, OnBurstTick);
        flameTask = Repeat(20000, OnFlameTick);
        deliveryTask = Repeat(25000, OnDeliveryTick);
    }

    private static ScheduledTask Repeat(int periodMillis, System.Action tick) =>
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { tick(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(periodMillis),
            System.TimeSpan.FromMilliseconds(periodMillis));

    private bool Fighting() => !IsDead() && IsInState(AIState.FIGHT);

    private void OnBeatTick()
    {
        if (Fighting())
            NpcSkillCasting.QueueAtDataLevel(GetOwner(), BlazingCut, NpcSkillTargetAttribute.MOST_HATED);
    }

    /// <summary>The 7s beat only acts on the tick that first crosses one of its three HP steps.</summary>
    private void OnBurstTick()
    {
        if (!Fighting())
            return;

        int hp = GetLifeStats().GetHpPercentage();
        lock (stateLock)
        {
            if (burstStepsTaken >= BurstSteps.Length || hp >= BurstSteps[burstStepsTaken])
                return;
            burstStepsTaken++;
        }
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), FlameBurst, NpcSkillTargetAttribute.MOST_HATED);
    }

    private void OnFlameTick()
    {
        if (!Fighting())
            return;
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), FlameSummon, NpcSkillTargetAttribute.ME);
        Spawn(TormentBlaze, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading());
    }

    /// <summary>
    /// Sends the next executor in rotation, or several at once once the fight is nearly over.
    /// </summary>
    private void OnDeliveryTick()
    {
        if (!Fighting())
            return;

        int count = GetLifeStats().GetHpPercentage() < ThickenBelowHp ? 3 : 1;
        for (int i = 0; i < count; i++)
        {
            int index;
            lock (stateLock)
            {
                index = deliveryIndex % DeliveryExecutors.Length;
                deliveryIndex++;
            }
            SendExecutor(index);
        }
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), FlameBurst, NpcSkillTargetAttribute.MOST_HATED);
    }

    /// <summary>Spawns an executor and walks it to its brazier a moment later.</summary>
    private void SendExecutor(int index)
    {
        var executor = (Npc)Spawn(DeliveryExecutors[index], DeliveryX, DeliveryY, DeliveryZ, (sbyte)0);
        int targetId = DeliveryTargets[index];
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                Npc target = GetPosition().GetWorldMapInstance().GetNpc(targetId);
                if (target != null)
                {
                    executor.SetTarget(target);
                    executor.GetMoveController().MoveToTargetObject();
                }
            }
            return ValueTask.CompletedTask;
        }, 1500L);
    }

    protected override void HandleDied()
    {
        CancelTimers();
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTimers();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CancelTimers();
        base.HandleDespawned();
    }

    private void CancelTimers()
    {
        Cancel(ref beatTask);
        Cancel(ref burstTask);
        Cancel(ref flameTask);
        Cancel(ref deliveryTask);
        lock (stateLock)
        {
            burstStepsTaken = 0;
            deliveryIndex = 0;
        }
    }

    private static void Cancel(ref ScheduledTask? task)
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
        task = null;
    }
}
