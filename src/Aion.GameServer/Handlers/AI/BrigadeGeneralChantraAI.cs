using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Brigade General Chantra (219353). Retail pattern <c>IDTiamat_Chantra</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/BrigadeGeneralChantraAI (@author Cheatkiller).
/// Retail-sourced corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>His area attack ran once every forty seconds and retail runs it every seven.</b> Retail arms
/// <c>BTIMERI_INDEX_1</c> at four seconds and re-arms it at seven after every firing, guarded by
/// <c>is_hp_in_boundary 15-100</c>. This class opened at five seconds and repeated every forty, so a
/// raid saw the ring about six times less often than it should.
/// </para>
/// <para>
/// <b>And the two rings are not a coin flip.</b> Retail's A branch carries
/// <c>test_probability percent=36</c> and the B branch is the fallback, so it is 36/64. This class rolled
/// evenly between them.
/// </para>
/// <para>
/// Each firing places <b>two</b> npcs at the same fixed point for four seconds — the ring itself and a
/// <c>DranaFX</c> (283173) beside it. <b>Nothing in this port had ever placed the drana.</b> The
/// after-ring that follows now belongs to the ring, in <see cref="ChantraAreaRingAI"/>, which is where
/// retail keeps it.
/// </para>
/// <para>
/// <b>And the rage is at fourteen per cent, not twenty-five</b> — the same correction as his neighbour
/// Terath, from the same shape of rung.
/// </para>
/// <para>
/// <b>Not translated.</b> His <c>on_die</c> (two condition variables, two doors, a reward message), his
/// <c>on_leave_attack_state</c> dispel-and-heal, and the power attack on <c>BTIMERI_INDEX_0</c> every
/// eight seconds — that one is a plain <c>use_skill</c> on his target and needs a skill index.
/// </para>
/// </remarks>
[AIName("brigadegeneralchantra")]
public class BrigadeGeneralChantraAI : AggressiveNpcAI
{
    /// <summary>Retail's <c>BTIMERI_INDEX_1</c>: four seconds to the first ring, seven between.</summary>
    private static readonly System.TimeSpan RingFirst = System.TimeSpan.FromSeconds(4);
    private static readonly System.TimeSpan RingRepeat = System.TimeSpan.FromSeconds(7);

    /// <summary>Retail's <c>test_probability</c> on the A branch; B is the fallback.</summary>
    private const int RingAChance = 36;

    /// <summary>Retail's <c>is_hp_in_boundary larger_than=15</c> on both area rungs.</summary>
    private const int RingFloorPercent = 15;

    /// <summary>The two rings, the drana beside them, and the point all three stand on.</summary>
    private const int RingA = 283092;
    private const int RingB = 283094;
    private const int DranaFx = 283173;
    private const float RingX = 1031.1f;
    private const float RingY = 466.38f;
    private const float RingZ = 445.45f;

    /// <summary>Retail's <c>live_time</c> on everything this attack places.</summary>
    private const int RingLife = 4;

    /// <summary>Retail's <c>is_hp_lower_than percent=14</c> on the rage rung.</summary>
    private const int RagePercent = 14;

    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? trapTask;
    private bool isFinalBuff;

    public BrigadeGeneralChantraAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
            StartSkillTask();
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= RagePercent)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20942);
        }
    }

    private void StartSkillTask()
    {
        trapTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
                StartTrapEvent();
            return ValueTask.CompletedTask;
        }, RingFirst, RingRepeat);
    }

    private void CancelTask()
    {
        if (trapTask != null && !trapTask.IsCancelled)
        {
            trapTask.Cancel(true);
        }
    }

    /// <summary>
    /// One turn of retail's area rung: a ring and a drana at the fixed point, four seconds each.
    /// </summary>
    /// <remarks>
    /// The after-ring is not placed here. Retail hangs it off the ring itself, three seconds in, and so
    /// does <see cref="ChantraAreaRingAI"/> — which also means it arrives even if Chantra dies in the
    /// meantime, exactly as a spawned npc with its own timer does in retail.
    /// </remarks>
    private void StartTrapEvent()
    {
        // Retail's rungs are guarded by a health band; under fifteen per cent the area attack stops and
        // only the rage rung is left.
        if (GetLifeStats().GetHpPercentage() <= RingFloorPercent)
            return;

        int ring = Rnd.NextInt(100) < RingAChance ? RingA : RingB;
        if (GetPosition().GetWorldMapInstance().GetNpc(ring) != null)
            return;

        SpawnFor(ring, RingX, RingY, RingZ, (sbyte)0, RingLife);
        SpawnFor(DranaFx, RingX, RingY, RingZ, (sbyte)0, RingLife);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        isFinalBuff = false;
        isHome.Set(true);
    }
}
