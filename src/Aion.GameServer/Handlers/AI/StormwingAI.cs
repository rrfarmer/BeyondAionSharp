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
    /// How many twisters each band sends and how long they stand, per mode, in band order.
    /// </summary>
    /// <remarks>
    /// <b>These were reversed.</b> The lifetimes were transcribed in retail's own branch order -- p40
    /// down to p34, which is 5% first and 95% last -- and then indexed by <see cref="Bands"/>, which
    /// runs 95% first. So the opening band got the lifetime of the last and the last got the opening's:
    /// the twisters that should stand thirty seconds stood eighty, and the ones meant to stand eighty
    /// stood thirty. <b>The class's own remark stated the order correctly and the array applied it
    /// backwards</b>, which is why reading the comment was not enough to catch it.
    /// <para>
    /// <b>And the two modes differ in count and lifetime both.</b> Hard mode is not "the same fight with
    /// more adds" -- it sends <i>fewer</i> per band, two in the top four and three in the bottom three
    /// against normal's flat four, and it holds them longer once past thirty-five per cent. Normal's
    /// last band is the outlier in the other direction: four twisters standing a full two minutes.
    /// </para>
    /// </remarks>
    private static readonly (int Count, int Life)[] NormalBands =
    {
        (4, 30), (4, 30), (4, 30), (4, 30), (4, 30), (4, 30), (4, 120),
    };

    private static readonly (int Count, int Life)[] HardBands =
    {
        (2, 30), (2, 30), (2, 30), (2, 30), (3, 45), (3, 45), (3, 80),
    };


    /// <summary>
    /// The escalation: four timer-1 branches that ping-pong on one flag var.
    /// </summary>
    /// <remarks>
    /// <b>This is not a four-wave sequence that runs out, which is what the class did.</b> The four
    /// branches are two pairs — bleed and root — and each pair has a test-and-set copy and a
    /// test-and-unset copy of the same flag. So on any tick exactly one of four things happens:
    /// <list type="number">
    /// <item>bleed elites, at <b>70%</b>, on one of its two route sets;</item>
    /// <item>otherwise root elites, at <b>50%</b> in normal mode and <b>always</b> in hard;</item>
    /// <item>otherwise nothing at all — a 15% chance in normal mode, impossible in hard;</item>
    /// <item>and whichever fires <b>flips the flag</b>, which picks the other route set next time.</item>
    /// </list>
    /// <b>It never stops.</b> The old reading — sharp twice, root twice, then silence for the rest of
    /// the fight — got the kinds roughly right and the structure wrong, and made the second half of
    /// every fight quieter than retail.
    /// <para>
    /// The lifetime was hard mode's fifteen seconds applied to both. Normal holds its elites for
    /// <b>thirty</b>, which with four of them arriving on most ticks is a materially denser room.
    /// </para>
    /// </remarks>
    private int EscalationLife => IsHardMode ? 15 : 30;

    /// <summary>Retail's <c>test_probability</c> on the bleed pair and the root pair.</summary>
    private const int BleedChance = 70;

    private int RootChance => IsHardMode ? 100 : 50;

    /// <summary>
    /// The sixteen <c>NPCPathPath_RudraWind_N</c> routes, split the way retail splits them.
    /// </summary>
    /// <remarks>
    /// Hard mode sends eight at once and uses all sixteen across its two sets; normal sends four and
    /// gives each of its four branches a distinct quarter. <b>None of these were being used at all</b> —
    /// the escalation spawned its elites on top of the boss and left them there.
    /// </remarks>
    private static readonly int[] NormalBleedA = { 0, 8, 4, 12 };
    private static readonly int[] NormalBleedB = { 2, 10, 6, 14 };
    private static readonly int[] NormalRootA = { 1, 9, 5, 13 };
    private static readonly int[] NormalRootB = { 3, 11, 7, 15 };

    private static readonly int[] HardEven = { 0, 8, 2, 10, 4, 12, 6, 14 };
    private static readonly int[] HardOdd = { 1, 9, 3, 11, 5, 13, 7, 15 };

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
    /// <summary>Retail's <c>FLAGVARI_GAMMA_1</c>: which of the two route sets comes next.</summary>
    private bool escalationFlag;

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

        // Hard mode's own rung, which normal does not have: a bleed twister planted on whoever he is
        // facing, standing thirty seconds. It is the only twister in this fight aimed at a player
        // rather than at a route.
        if (IsHardMode && hp > 30 && hp <= 50)
            SpawnOnCurrentTarget(SharpTwister, 30, spawnRange: 0f);

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
        {
            SpawnLightning(7);
        }
        else if (hp <= 50)
        {
            SpawnLightning(15);

            // And hard mode adds a root twister on a random attacker beside it -- five seconds, which
            // is the shortest-lived add in the fight, scattered five metres off whoever it picked.
            if (IsHardMode)
                SpawnOnRandomAttacker(RootTwister, 5, spawnRange: 5f);
        }

        long backToCast = hp <= 30 ? 35000L : hp <= 50 ? 30000L : 20000L;
        lightningTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { OnCastRung(); return ValueTask.CompletedTask; }, backToCast);
    }

    /// <summary>
    /// Lightning on up to <paramref name="cap"/> random players inside retail's fifty-metre valid
    /// distance.
    /// </summary>
    /// <remarks>
    /// <b>The cap is the whole content of the op.</b> Both rungs of the timer chain carry
    /// <c>total_set_to_spawn=1</c>; the escalation's root branches carry <b>three</b>. Reading
    /// <c>spawn_on_multi_target</c> as "everybody" would make either several times harsher than retail.
    /// </remarks>
    private void SpawnLightning(int liveSeconds, int cap = 1)
    {
        foreach (Creature target in RandomValidTargets(cap))
            SpawnFor(Lightning, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0, liveSeconds);
    }

    /// <summary>Retail's <c>ORDERI_RANDOM</c> slice of the hate list, inside the valid distance.</summary>
    private List<Creature> RandomValidTargets(int cap)
    {
        List<Creature> valid = GetAggroList().StreamValidTargets(LightningReach).ToList();
        var picked = new List<Creature>();
        while (picked.Count < cap && valid.Count > 0)
        {
            int i = Rnd.NextInt(valid.Count);
            picked.Add(valid[i]);
            valid.RemoveAt(i);
        }

        return picked;
    }

    /// <summary><c>spawn_on_target target_obj=OBJI_CUR_TARGET</c>, inside the valid distance.</summary>
    private void SpawnOnCurrentTarget(int npcId, int liveSeconds, float spawnRange)
    {
        Creature? target = GetTarget() as Creature;
        if (target == null || !PositionUtil.IsInRange(GetOwner(), target, (int)LightningReach))
            return;

        Scatter(npcId, target, liveSeconds, spawnRange);
    }

    /// <summary><c>spawn_on_target_by_attacker_indicator ATTACKERI_RANDOM_ONE</c>.</summary>
    private void SpawnOnRandomAttacker(int npcId, int liveSeconds, float spawnRange)
    {
        foreach (Creature target in RandomValidTargets(1))
            Scatter(npcId, target, liveSeconds, spawnRange);
    }

    private void Scatter(int npcId, Creature target, int liveSeconds, float spawnRange)
    {
        float x = target.GetX();
        float y = target.GetY();
        if (spawnRange > 0f)
        {
            double angle = Rnd.NextFloat(360f) * System.Math.PI / 180.0;
            float distance = Rnd.NextFloat(spawnRange);
            x += (float)(System.Math.Cos(angle) * distance);
            y += (float)(System.Math.Sin(angle) * distance);
        }

        SpawnFor(npcId, x, y, target.GetZ(), (sbyte)0, liveSeconds);
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
        (int count, int life) = (IsHardMode ? HardBands : NormalBands)[band];
        for (int i = 0; i < count; i++)
        {
            int npcId = i % 2 == 0 ? SharpTwister : RootTwister;
            (float dx, float dy) = scatter ? Diagonals[i] : (0f, 0f);
            SpawnNear(npcId, dx, dy, life, paths[i]);
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

        bool bleed;
        bool flagWasSet;
        lock (bandLock)
        {
            // Retail's order of evaluation, and it matters: the bleed pair is tried first, so a tick
            // that rolls its seventy never reaches the root pair at all.
            bleed = Rnd.NextInt(100) < BleedChance;
            if (!bleed && Rnd.NextInt(100) >= RootChance)
                return;

            flagWasSet = escalationFlag;
            escalationFlag = !escalationFlag;
        }

        int npcId = bleed ? SharpTwisterElite : RootTwisterElite;
        int[] routes = RoutesFor(bleed, flagWasSet);
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), ThreshingWind, NpcSkillTargetAttribute.ME);
        foreach (int route in routes)
            SpawnNear(npcId, 0f, 0f, EscalationLife, "NPCPathPath_RudraWind_" + route);

        // Normal mode's root pair drops three lightnings alongside the elites; hard mode's does not.
        // Found only once the summariser started printing total_set_to_spawn -- the field that says
        // this is three and not one, and not everybody.
        if (!bleed && !IsHardMode)
            SpawnLightning(7, cap: 3);
    }

    /// <summary>
    /// Which quarter of the sixteen routes this branch takes. Hard mode splits them evens against odds
    /// and reuses both halves for both kinds; normal gives each of its four branches its own quarter.
    /// </summary>
    private int[] RoutesFor(bool bleed, bool flagWasSet)
    {
        if (IsHardMode)
            return flagWasSet ? HardOdd : HardEven;
        if (bleed)
            return flagWasSet ? NormalBleedB : NormalBleedA;
        return flagWasSet ? NormalRootB : NormalRootA;
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
        twister.GetSpawn()?.SetWalkerId(path);
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
            escalationFlag = false;
        }
    }

    private static void Cancel(ref ScheduledTask? task)
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
        task = null;
    }
}
