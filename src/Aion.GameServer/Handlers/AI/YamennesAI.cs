using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/abyssal_splinter/YamennesAI (@author Ritsu, Luzien).
/// </summary>
[AIName("yamennes")]
public class YamennesAI : AggressiveNpcAI
{
    private ScheduledTask? portalTask;
    private ScheduledTask? enrageTask;
    private ScheduledTask? golemTask;
    private ScheduledTask? furyTask;
    private readonly AtomicBoolean isStart = new AtomicBoolean();

    public YamennesAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStart.CompareAndSet(false, true))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500013); // Those who threaten the artefact shall be returned to the flow of Aether!
            StartTasks();
        }
    }

    private void StartTasks()
    {
        enrageTask = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(19098, 55); return ValueTask.CompletedTask; }, 600000L);
        // Hard mode only. IDAbRe_Core_NamedD_Hard carries the three ametgolems; IDAbRe_Core_NamedD, the
        // normal Yamennes at 216952, has the same portals and no golems at all, so both npcs can share
        // this class only if the golems are gated. The portals below are not: they are in both patterns.
        if (GetNpcId() == HardYamennes)
        {
            golemTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
                _ => { SpawnGolems(); return ValueTask.CompletedTask; },
                System.TimeSpan.FromMilliseconds(GolemCycleMillis),
                System.TimeSpan.FromMilliseconds(GolemCycleMillis));
        }

        // The upper floor comes first. Retail's two branches share one battle timer and are told apart
        // by a test-and-set flag: the upper branch passes while the flag is unset, so it takes the
        // first firing, and the lower branch -- test-and-unset -- can only pass after it. This started
        // with the lower floor, which inverts every wave of the fight.
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(true); return ValueTask.CompletedTask; }, 60000L);
        furyTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { SpawnFuries(); return ValueTask.CompletedTask; }, FirstFuryMillis);
    }

    /// <summary>Drops a fury on each of the most-hated, then books the next wave.</summary>
    /// <remarks>
    /// Taken from the top of the hate list rather than at random — retail's
    /// <c>order_in_attacker_list=ORDERI_DESCENDING</c> — and capped at <c>total_set_to_spawn</c>. The
    /// three-hundred-metre <c>valid_distance</c> is the widest in the 5.8 dump and is effectively the
    /// whole room, which is the point: there is nowhere in it to stand and be skipped.
    /// </remarks>
    private void SpawnFuries()
    {
        if (IsDead() || !GetOwner().IsSpawned())
            return;

        AggroList aggro = GetAggroList();
        List<Creature> targets = aggro.StreamValidTargets(FuryRange)
            .OrderByDescending(t => aggro.GetHate(t))
            .Take(FuriesPerWave)
            .ToList();

        foreach (Creature target in targets)
        {
            if (Spawn(ProtectorsFury, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0) is not Npc fury)
                continue;

            AttackAfterSpawn.NextTick(fury, target, FuryHate);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                fury.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, FuryLifeMillis);
        }

        furyTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { SpawnFuries(); return ValueTask.CompletedTask; }, FuryIntervalMillis);
    }

    /// <summary>One sliver, at his own feet, with no lifetime.</summary>
    private void SpawnSliver()
    {
        Spawn(YamennesSliver, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading());
    }

    /// <summary>
    /// Retail <c>IDAbRe_Core_NamedD_Hard</c>: the portals carry <c>live_time</c> 70 on a timer re-armed
    /// at 70 seconds, so a set expires exactly as the next arrives. Ours waited a flat 60.
    /// </summary>
    /// <summary>
    /// Retail's gates, by floor. Upstairs is three different npcs; downstairs is one npc three times.
    /// </summary>
    /// <remarks>
    /// Retail-sourced; see docs/retail-ai-fidelity.md. <c>IDAbRe_Core_NamedD</c> alternates on a single
    /// battle timer through a test-and-set flag: the set branch opens
    /// <c>IDAbRe_Core_Sum_Teleport2</c>, <c>_03</c> and <c>_06</c> on the upper floor, and the
    /// test-and-unset branch opens three <c>_Low</c> downstairs. Each gate then puts one enemy on
    /// itself with a hundred thousand hate and lives seventy seconds.
    /// <para>
    /// <b>Coordinates are left as they were.</b> Retail gives its own marks and headings for all six,
    /// and they are not these; the numbers here came from the Java class and presumably from a live
    /// sniff. Moving portals is a different decision from naming them and is recorded as owed rather
    /// than taken in passing.
    /// </para>
    /// </remarks>
    internal const int UpperGateA = 281906;
    internal const int UpperGateB = 282014;
    internal const int UpperGateC = 282015;
    internal const int LowerGate = 282131;

    private const int PortalLife = 70;
    private const int PortalCycleMillis = 70000;

    /// <summary>
    /// Retail gives the ametgolems three minutes on a timer of their own.
    /// </summary>
    /// <remarks>
    /// <b>Ours are still driven by the healing-debuff chain rather than by retail's independent
    /// 180-second timer</b>, and are cleared explicitly at the start of each debuff, so this lifetime
    /// rarely fires. It is set anyway because the explicit clear is not a lifetime -- if the chain ever
    /// stops, the golems standing at that moment would otherwise stay forever. <b>The cadence itself is
    /// a known structural divergence and is not fixed here.</b>
    /// </remarks>
    private const int GolemLife = 180;

    /// <summary>Retail re-arms the golem branch's own timer at three minutes.</summary>
    private const long GolemCycleMillis = 180_000L;

    /// <summary>The hard variant, and the only one retail gives golems.</summary>
    private const int HardYamennes = 216960;

    /// <summary>
    /// Retail's <c>IDCatacombs_Hard_Buff</c> — protector's fury, dropped on the top of the hate list.
    /// </summary>
    /// <remarks>
    /// <b>Neither Yamennes had this, and both patterns carry it.</b> It is the fight's only continuous
    /// add stream: a fury arrives <em>already fighting</em> the player it landed on, with two million
    /// hate, and lives ten seconds. The number is not decoration — it is far past anything a raid
    /// accumulates, so the fury stays on its own victim rather than peeling to the tank.
    /// <para>
    /// <b>The two modes differ in cadence and count, and the hard one is much the harsher</b>: two every
    /// twenty seconds from the first minute, against three every eight from fifty-four seconds. That is
    /// a third more adds arriving two and a half times as often.
    /// </para>
    /// <para>
    /// The unstable variant has had this for several passes; only these two npcs were missing it, which
    /// is the same asymmetry between the two classes that the golems and the portals were.
    /// </para>
    /// </remarks>
    private const int ProtectorsFury = 281819;
    private const long FuryLifeMillis = 10000L;
    private const float FuryRange = 300f;
    private const int FuryHate = 2000000;

    private long FirstFuryMillis => GetNpcId() == HardYamennes ? 54000L : 60000L;

    private long FuryIntervalMillis => GetNpcId() == HardYamennes ? 8000L : 20000L;

    private int FuriesPerWave => GetNpcId() == HardYamennes ? 3 : 2;

    /// <summary>
    /// Retail's <c>IDAbRe_Core_Sum_NamedD_onDie</c> — a sliver left where he falls.
    /// </summary>
    /// <remarks>
    /// <c>spawn_on_target target_obj=OBJI_SELF</c>, so it goes at his own feet. The hard pattern writes
    /// the branch twice with the same test-and-set flag var, which means one sliver and not two: the
    /// first match sets the flag and the second can never run.
    /// </remarks>
    private const int YamennesSliver = 282065;

    /// <summary>
    /// Retail's three marks for the ametgolems, from <c>IDAbRe_Core_NamedD_Hard</c>.
    /// </summary>
    /// <remarks>
    /// <b>They are absolute in retail and were relative here</b> — this class placed them at ten metres
    /// diagonally off Yamennes, so they followed him around the room instead of standing where the fight
    /// expects them.
    /// </remarks>
    private static readonly (float X, float Y, float Z)[] GolemMarks =
    [
        (361.53f, 741.54f, 198.31f),
        (302.85f, 735.30f, 198.15f),
        (334.30f, 709.31f, 198.81f),
    ];

    /// <summary>
    /// Retail hangs the golems off a timer of their own, re-armed at three minutes, and lets each expire
    /// on its own three-minute <c>live_time</c>.
    /// </summary>
    /// <remarks>
    /// <b>This class drove them from the healing-debuff chain instead</b>, clearing the previous three at
    /// the start of every debuff — so their cadence was the debuff's, not theirs, and the lifetime added
    /// in an earlier pass almost never fired because the explicit clear got there first. That divergence
    /// was recorded at the time and left standing; this is it corrected.
    /// </remarks>
    private void SpawnGolems()
    {
        if (IsDead() || !GetOwner().IsSpawned())
            return;

        foreach ((float x, float y, float z) in GolemMarks)
            SpawnFor(282107, x, y, z, 0, GolemLife);
    }

    private void OnHealingDebuff()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        GetOwner().QueueSkill(19282, 55);
        GetOwner().ClearAttackedCount();
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_ResetAggro());
    }

    private void SpawnPortals(bool isTopSpawn)
    {
        // Retail spawns unconditionally and lets the portals time out. This used to spawn only when
        // none of the three were still standing and gave them no lifetime at all, so a group that
        // ignored the portals rather than killing them saw the first wave and never another -- the same
        // shape the unstable variant was corrected for, and this class kept.
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_SummonStart());
        // Retail's two floors use different gates, and this used the wrong mix on both.
        //
        // Upstairs is three DIFFERENT gates -- Teleport2, _03 and _06 -- and downstairs is _Low three
        // times over. What was here used _03, _06 and _Low upstairs and the same three again
        // downstairs, so 281906 never appeared anywhere in the encounter and the lower floor was
        // opened by two gates that belong upstairs.
        if (isTopSpawn)
        {
            SpawnFor(UpperGateA, 288.10f, 741.95f, 216.81f, (sbyte)3, PortalLife);
            SpawnFor(UpperGateB, 375.05f, 750.67f, 216.82f, (sbyte)59, PortalLife);
            SpawnFor(UpperGateC, 341.33f, 699.38f, 216.86f, (sbyte)59, PortalLife);
        }
        else
        {
            SpawnFor(LowerGate, 303.69f, 736.35f, 198.7f, (sbyte)0, PortalLife);
            SpawnFor(LowerGate, 335.19f, 708.92f, 198.9f, (sbyte)35, PortalLife);
            SpawnFor(LowerGate, 360.23f, 741.07f, 198.7f, (sbyte)0, PortalLife);
        }
        ThreadPoolManager.GetInstance().Schedule(_ => { OnHealingDebuff(); return ValueTask.CompletedTask; }, 3000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(!isTopSpawn); return ValueTask.CompletedTask; }, PortalCycleMillis);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        npcs.Where(n => n != null).ToList().ForEach(n => n.GetController().Delete());
    }

    private void CancelTasks()
    {
        if (portalTask != null && !portalTask.IsDone())
            portalTask.Cancel(true);
        if (enrageTask != null && !enrageTask.IsDone())
            enrageTask.Cancel(true);
        // The golem clock repeats, so it has to be cancelled and not merely guarded: a repeating task
        // that only checks IsDead() keeps running forever, which is what StopsEveryTimerWhenItDies was
        // written for on Stormwing.
        if (golemTask != null && !golemTask.IsDone())
            golemTask.Cancel(true);
        // The fury chain books its own successor, so cancelling the handle is the only thing that ends
        // it -- the same shape the portal chain has.
        if (furyTask != null && !furyTask.IsDone())
            furyTask.Cancel(true);
    }

    protected override void HandleBackHome()
    {
        CancelTasks();
        base.HandleBackHome();
        GetOwner().GetController().Delete();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282107));
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282107));
        // Before base, which clears his position and hate list: retail's branch runs while he is still
        // standing where he fell, and the sliver goes there.
        SpawnSliver();
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
