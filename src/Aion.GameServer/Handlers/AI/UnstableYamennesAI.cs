using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/unstableSplinterpath/UnstableYamennesAI (@author Ritsu, Luzien, Cheatkiller),
/// with the portal cadence corrected against retail patterns <c>IDAbRe_Core_NamedD_02</c> and
/// <c>IDAbRe_Core_NamedD_Hard_02</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The alternating portals were already right in shape
/// — upstairs, then downstairs, then upstairs — because retail builds that alternation out of one flag
/// var toggled by two branches, and this class had arrived at the same behaviour independently. Only
/// the timing was off: retail arms the portal timer at <b>30s</b> and re-arms it at <b>65s</b>, where
/// this waited a flat 60 both times.
/// <para>
/// <b>The gates are now retail's, and so is what they do.</b> An earlier pass left 219567/219579/219580
/// in place on the reasoning that the ids the pattern names — 283203/283222/283223 upstairs, 283233
/// downstairs — would be inert scenery, since only ours carried an AI. The conclusion was right and
/// the reason stopped a level short: our gate AI is not a translation of the pattern those ids bind at
/// all. It spawns two npcs at ±3 metres twelve seconds in and once more at seventy-two, where retail's
/// gate feeds an orkanimum onto a fixed mark every twelve seconds — or, downstairs, a lapilima at its
/// own feet every nine. Swapping ids alone would indeed have been inert; swapping ids <i>and</i>
/// porting the pattern is <see cref="YamennesSpawnGateAI"/>.
/// <para>
/// The upstairs three are three different gates on three marks; the downstairs three are the
/// <b>same</b> gate on three marks, which is why only one id appears below.
/// </para>
/// </para>
/// </remarks>
[AIName("unstableyamennes")]
public class UnstableYamennesAI : AggressiveNpcAI
{
    /// <summary>Retail's battle timer 1: armed at 30s on entering combat, re-armed at 65s.</summary>
    private const long FirstPortalMillis = 30000L;
    private const long PortalIntervalMillis = 65000L;

    /// <summary>Retail's <c>live_time</c> on each gate.</summary>
    private const long GateLifeMillis = 70000L;

    // Retail's own gate ids, from IDAbRe_Core_NamedD_02. Both bosses spawn this b-prefixed set.
    private const int GateNorthWest = 283203;
    private const int GateSouth = 283222;
    private const int GateEast = 283223;
    private const int GateLow = 283233;

    /// <summary>
    /// Retail's <c>dir</c> is degrees, and the spawn helper wants a heading.
    /// </summary>
    /// <remarks>
    /// The coordinates this class carried before passed their <c>dir</c> straight through as a
    /// heading. That happened to compile because they were all small — 0, 3, 35, 59 — but retail's
    /// own gate directions run to 279, which does not fit a heading at all. The old numbers were
    /// being read as headings when they were degrees.
    /// </remarks>
    private static sbyte Facing(int degrees) =>
        (sbyte)Aion.GameServer.Utils.PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    /// <summary>
    /// Retail's <c>IDCatacombs_Hard_Buff</c> — protector's fury, dropped on the most-hated. Battle
    /// timer 6, armed at sixty seconds and re-armed at twenty, one per target, ten-second life.
    /// </summary>
    private const int ProtectorsFury = 281819;
    private const long FirstFuryMillis = 60000L;
    private const long FuryIntervalMillis = 20000L;
    private const long FuryLifeMillis = 10000L;
    private const float FuryRange = 300f;

    /// <summary>Retail's <c>hatepoints_to_add</c> on the fury, with <c>attack_target_after_spawn</c>.</summary>
    private const int FuryHate = 2000000;

    /// <summary>Retail's <c>IDAbRe_Core_Sum_NamedD_onDie</c> — a sliver left on the killer's side.</summary>
    private const int YamennesSliver = 282065;
    private const float SliverRange = 50f;

    /// <summary>
    /// Painflare is the hard-mode twin and gets one more of each: three furies a wave against two, and
    /// two slivers on death against one. Both npc ids share this class, as they share the pattern.
    /// </summary>
    private const int Painflare = 219563;

    private int FuriesPerWave => GetOwner().GetNpcId() == Painflare ? 3 : 2;

    private int SliversOnDeath => GetOwner().GetNpcId() == Painflare ? 2 : 1;

    private ScheduledTask? portalTask;
    private ScheduledTask? furyTask;
    private ScheduledTask? enrageTask;
    private readonly AtomicBoolean isStart = new AtomicBoolean();

    public UnstableYamennesAI(Npc owner)
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
        enrageTask = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(19098, 55, 0); return ValueTask.CompletedTask; }, 600000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(false); return ValueTask.CompletedTask; }, FirstPortalMillis);
        furyTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnFuries(); return ValueTask.CompletedTask; }, FirstFuryMillis);
    }

    /// <summary>
    /// Drops a protector's fury on each of the most-hated, then books the next wave. Retail's cap is
    /// two here and three for Painflare, taken from the top of the hate list rather than at random.
    /// </summary>
    /// <remarks>
    /// Each one arrives <b>already fighting the player it landed on</b>, with two million hate. That is
    /// retail's <c>attack_target_after_spawn</c>, and the number is not decoration: a fury lives ten
    /// seconds, so it has to be dealt with by whoever it picked rather than peeled onto a tank.
    /// </remarks>
    private void SpawnFuries()
    {
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

        furyTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnFuries(); return ValueTask.CompletedTask; }, FuryIntervalMillis);
    }

    /// <summary>Retail leaves these behind when it falls; they have no lifetime and stay.</summary>
    private void SpawnSlivers()
    {
        Creature? target = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
        if (target == null || !PositionUtil.IsInRange(GetOwner(), target, SliverRange, false))
            return;

        for (int i = 0; i < SliversOnDeath; i++)
            Spawn(YamennesSliver, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0);
    }

    private void OnHealingDebuff()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(219586));
        GetOwner().QueueSkill(19282, 55);
        Spawn(219586, GetOwner().GetX() + 10, GetOwner().GetY() - 10, GetOwner().GetZ(), (sbyte)0);
        Spawn(219586, GetOwner().GetX() - 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0);
        Spawn(219586, GetOwner().GetX() + 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0);
        GetOwner().ClearAttackedCount();
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_ResetAggro());
    }

    private void SpawnPortals(bool isTopSpawn)
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_SummonStart());
        if (isTopSpawn)
        {
            SpawnGate(GateNorthWest, 297.66f, 736.42f, 215.99f, Facing(171));
            SpawnGate(GateSouth, 333.15f, 702.77f, 215.99f, Facing(279));
            SpawnGate(GateEast, 368.95f, 740.09f, 215.99f, Facing(3));
        }
        else
        {
            SpawnGate(GateLow, 302.22f, 735.60f, 197.70f, Facing(119));
            SpawnGate(GateLow, 334.41f, 708.80f, 197.90f, Facing(33));
            SpawnGate(GateLow, 361.11f, 741.87f, 197.52f, Facing(64));
        }
        ThreadPoolManager.GetInstance().Schedule(_ => { OnHealingDebuff(); return ValueTask.CompletedTask; }, 3000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(!isTopSpawn); return ValueTask.CompletedTask; }, PortalIntervalMillis);
    }

    /// <summary>
    /// Opens one gate, which closes on its own after seventy seconds.
    /// </summary>
    /// <remarks>
    /// The lifetime is retail's (<c>live_time=70</c>) and it is what makes the alternation work. This
    /// used to spawn a wave only when none of the three gates were still standing, and gave them no
    /// lifetime at all — so a group that ignored the portals rather than killing them saw the first
    /// wave and never another. Retail spawns unconditionally and lets the gates time out, which
    /// bounds them at two overlapping sets for the five seconds the 70s life exceeds the 65s cycle.
    /// </remarks>
    private void SpawnGate(int npcId, float x, float y, float z, sbyte heading)
    {
        if (Spawn(npcId, x, y, z, heading) is not Npc gate)
            return;

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            gate.GetController().DeleteIfAliveOrCancelRespawn();
            return ValueTask.CompletedTask;
        }, GateLifeMillis);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        npcs.Where(n => n != null).ToList().ForEach(n => n.GetController().Delete());
    }

    private void CancelTasks()
    {
        if (portalTask != null && !portalTask.IsDone())
            portalTask.Cancel(true);
        if (furyTask != null && !furyTask.IsDone())
            furyTask.Cancel(true);
        if (enrageTask != null && !enrageTask.IsDone())
            enrageTask.Cancel(true);
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
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(219586));
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTasks();
        SpawnSlivers();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(219586));
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
