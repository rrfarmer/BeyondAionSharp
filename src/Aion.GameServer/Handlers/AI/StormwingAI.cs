using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Stormwing, Beshmundir Temple. Retail patterns IDCT_Rudra (216264) and IDCTH_Rudra (216183,
/// the variant our instance handler actually spawns).
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Neither server had an AI class for this
/// boss, so his signature mechanic did not exist: he never summoned a single twister, and the
/// four twister NPCs sat in npc_templates spawned by nothing.
/// <para>
/// Retail runs two add timers. A 10s beat crosses seven HP bands (95/80/65/50/35/20/5), and the
/// first time each is crossed he calls Threshing Wind down on himself and four twisters appear
/// — at the four diagonals on alternating bands, on top of him on the others. Below 50% a
/// second timer escalates every 30s through four waves: sharp twisters twice, then root
/// twisters twice.
/// </para>
/// <para>
/// Skill indices resolved from our skill list order, corroborated by index 3: the pattern casts
/// it on an attacker only below 50% HP, and our entry for Dragon's Quake carries a matching
/// max_hp="45" gate. Only Threshing Wind is driven from here; the remaining skills stay on
/// their npc_skills probabilities, which continue to approximate the retail rotation timers
/// this class does not reproduce.
/// </para>
/// </remarks>
[AIName("stormwing")]
public class StormwingAI : AggressiveNpcAI
{
    private const int HardModeNpcId = 216183;

    /// <summary>Threshing Wind: called down on himself as each HP band breaks.</summary>
    private const int ThreshingWind = 18613;

    /// <summary>Midnight Wind: the opener on whoever pulls him.</summary>
    private const int MidnightWind = 18614;

    private const int SharpTwister = 281796;
    private const int RootTwister = 281794;
    private const int SharpTwisterElite = 281797;
    private const int RootTwisterElite = 281795;

    /// <summary>HP bands, each firing once. Alternating bands scatter the twisters.</summary>
    private static readonly int[] Bands = { 95, 80, 65, 50, 35, 20, 5 };

    /// <summary>
    /// How long each band'''s twisters stand, in retail'''s own order: <c>IDCTH_Rudra</c> writes seven
    /// twister branches at p40 down to p34 carrying 80, 45, 45, 30, 30, 30 and 30 seconds.
    /// </summary>
    /// <remarks>
    /// <b>Seven branches against our seven bands, and four elite branches against our four escalation
    /// waves</b>, which is what makes this a mapping rather than a guess. Until now every twister was
    /// spawned with no lifetime at all and stood for the rest of the fight, so a long pull ended with
    /// dozens of them: <b>the mechanic was strictly harsher than retail'''s and got harsher the longer
    /// the fight ran.</b>
    /// </remarks>
    private static readonly int[] BandLives = { 80, 45, 45, 30, 30, 30, 30 };

    /// <summary>Retail p10 to p7, all fifteen seconds.</summary>
    private const int EscalationLife = 15;

    /// <summary>
    /// Retail's <c>BIDCTN_SumLightning_55_Ae</c>, and the chain that decides when it lands.
    /// </summary>
    /// <remarks>
    /// <b>This whole mechanic was missing.</b> Two of retail's battle timers hand back and forth --
    /// timer 2 casts and arms timer 3, timer 3 arms timer 2 -- and only in the bottom two bands does the
    /// timer-3 rung also drop a lightning. Above fifty per cent the chain runs and summons nothing, so a
    /// port that read only the top of the ladder would see no add at all.
    /// <para>
    /// <b>One lightning, not a raid-wide wave.</b> The below-thirty rung reads
    /// <c>spawn_on_multi_target</c>, which sounds like everybody -- and carries
    /// <c>total_set_to_spawn=1</c> with <c>ORDERI_RANDOM</c>. The 31-50 rung is
    /// <c>spawn_on_target_by_attacker_indicator RANDOM_ONE</c>. Both are one add on one random player;
    /// they differ only in how long it lives, seven seconds against fifteen.
    /// </para>
    /// </remarks>
    private const int Lightning = 281798;
    private const float LightningReach = 50f;

    /// <summary>Diagonal offsets used on the scattering bands.</summary>
    private static readonly (float X, float Y)[] Diagonals =
    {
        (10f, 10f), (-10f, 10f), (-10f, -10f), (10f, -10f),
    };

    /// <summary>
    /// The four routes each band's twisters take, which this class never started them on.
    /// </summary>
    /// <remarks>
    /// <b>Every twister spawn in the pattern carries a <c>pathname</c>, and all eight routes are in our
    /// walker data.</b> Without them the four appear at their offsets and stand there, which is a
    /// different room: the twisters are meant to sweep, and where they sweep is what the raid moves
    /// around. The two sets pair with the two kinds of band -- scattered spawns take the wide routes,
    /// spawns on top of him take the tight ones -- which is the same alternation this class already had
    /// for the offsets.
    /// </remarks>
    private static readonly string[] WidePaths =
    {
        "NPCPathPath_RudraWindC1", "NPCPathPath_RudraWindC2",
        "NPCPathPath_RudraWindC3", "NPCPathPath_RudraWindC4",
    };

    private static readonly string[] TightPaths =
    {
        "NPCPathPath_RudraWindC1_1", "NPCPathPath_RudraWindC2_1",
        "NPCPathPath_RudraWindC3_1", "NPCPathPath_RudraWindC4_1",
    };

    private readonly object bandLock = new object();
    private int bandsCrossed;
    private int escalationWave;

    private ScheduledTask? bandTask;
    private ScheduledTask? escalationTask;
    private ScheduledTask? lightningTask;

    public StormwingAI(Npc owner)
        : base(owner)
    {
    }

    private bool IsHardMode => GetNpcId() == HardModeNpcId;

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        StartFight();
    }

    private void StartFight()
    {
        if (bandTask != null)
            return;

        NpcSkillCasting.QueueAtDataLevel(GetOwner(), MidnightWind, NpcSkillTargetAttribute.MOST_HATED);

        bandTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnBandTick(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(10000));

        escalationTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnEscalationTick(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(30000), System.TimeSpan.FromMilliseconds(30000));

        // Retail arms timer 2 fifteen seconds in, and the two timers hand back and forth from there.
        lightningTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { OnCastRung(); return ValueTask.CompletedTask; }, 15000L);
    }

    /// <summary>Retail's timer-2 rungs: a cast, and timer 3 armed at a delay chosen by band.</summary>
    private void OnCastRung()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;

        int hp = GetLifeStats().GetHpPercentage();
        long toLightning = hp <= 30 ? 15000L : hp <= 50 ? 25000L : 20000L;
        lightningTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { OnLightningRung(); return ValueTask.CompletedTask; }, toLightning);
    }

    /// <summary>
    /// Retail's timer-3 rungs. Only the bottom two summon; the rest simply hand back to timer 2.
    /// </summary>
    private void OnLightningRung()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;

        int hp = GetLifeStats().GetHpPercentage();
        if (hp <= 30)
            SpawnLightning(7);
        else if (hp <= 50)
            SpawnLightning(15);

        long backToCast = hp <= 30 ? 35000L : hp <= 50 ? 30000L : 20000L;
        lightningTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { OnCastRung(); return ValueTask.CompletedTask; }, backToCast);
    }

    /// <summary>One lightning, on a random player inside retail's fifty-metre valid distance.</summary>
    private void SpawnLightning(int liveSeconds)
    {
        List<Creature> valid = GetAggroList().StreamValidTargets(LightningReach).ToList();
        if (valid.Count == 0)
            return;

        Creature target = valid[Rnd.NextInt(valid.Count)];
        SpawnFor(Lightning, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0, liveSeconds);
    }

    private void OnBandTick()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;

        int band = NextBrokenBand();
        if (band < 0)
            return;

        NpcSkillCasting.QueueAtDataLevel(GetOwner(), ThreshingWind, NpcSkillTargetAttribute.ME);

        // Bands alternate between scattering the twisters and dropping them on top of him.
        bool scatter = band % 2 == 0;
        string[] paths = scatter ? WidePaths : TightPaths;
        for (int i = 0; i < 4; i++)
        {
            int npcId = i % 2 == 0 ? SharpTwister : RootTwister;
            (float dx, float dy) = scatter ? Diagonals[i] : (0f, 0f);
            SpawnNear(npcId, dx, dy, BandLives[band], paths[i]);
        }
    }

    /// <summary>Returns the index of the band just broken, latching it, or -1 if none.</summary>
    private int NextBrokenBand()
    {
        int hp = GetLifeStats().GetHpPercentage();
        lock (bandLock)
        {
            if (bandsCrossed >= Bands.Length || hp >= Bands[bandsCrossed])
                return -1;
            return bandsCrossed++;
        }
    }

    /// <summary>
    /// Below half health the fight escalates every 30s through four waves: sharp twisters
    /// twice, then root twisters twice. Retail uses the elite variants here even in normal
    /// mode.
    /// </summary>
    private void OnEscalationTick()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;
        if (GetLifeStats().GetHpPercentage() > 50)
            return;

        int wave;
        lock (bandLock)
        {
            if (escalationWave >= 4)
                return;
            wave = escalationWave++;
        }

        int npcId = wave < 2 ? SharpTwisterElite : RootTwisterElite;
        int count = IsHardMode ? 8 : 4;
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), ThreshingWind, NpcSkillTargetAttribute.ME);
        for (int i = 0; i < count; i++)
            SpawnNear(npcId, 0f, 0f, EscalationLife);
    }

    private void SpawnNear(int npcId, float dx, float dy, int liveSeconds, string? path = null)
    {
        Npc owner = GetOwner();
        if (SpawnFor(npcId, owner.GetX() + dx, owner.GetY() + dy, owner.GetZ(),
                (sbyte)owner.GetHeading(), liveSeconds) is not Npc twister || path == null)
            return;

        // Retail's spawn is RELATIVE *and* names a path: it appears at the offset and then walks. A
        // twister whose route cannot be resolved still stands where it was put, which is the behaviour
        // this class had for every one of them.
        twister.GetSpawn().SetWalkerId(path);
        if (twister.GetAi() is NpcAI ai)
            Aion.GameServer.Ai.Manager.WalkManager.StartWalking(ai);
    }

    protected override void HandleDied()
    {
        CancelTasks();
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTasks();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        base.HandleDespawned();
    }

    private void CancelTasks()
    {
        Cancel(ref bandTask);
        Cancel(ref escalationTask);
        // The lightning chain books its own successor each rung, so cancelling the handle is what ends
        // it; a guard alone would leave it rescheduling for ever.
        Cancel(ref lightningTask);
        lock (bandLock)
        {
            bandsCrossed = 0;
            escalationWave = 0;
        }
    }

    private static void Cancel(ref ScheduledTask? task)
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
        task = null;
    }
}
