using System.Threading.Tasks;
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

    /// <summary>Diagonal offsets used on the scattering bands.</summary>
    private static readonly (float X, float Y)[] Diagonals =
    {
        (10f, 10f), (-10f, 10f), (-10f, -10f), (10f, -10f),
    };

    private readonly object bandLock = new object();
    private int bandsCrossed;
    private int escalationWave;

    private ScheduledTask? bandTask;
    private ScheduledTask? escalationTask;

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
        for (int i = 0; i < 4; i++)
        {
            int npcId = i % 2 == 0 ? SharpTwister : RootTwister;
            (float dx, float dy) = scatter ? Diagonals[i] : (0f, 0f);
            SpawnNear(npcId, dx, dy);
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
            SpawnNear(npcId, 0f, 0f);
    }

    private void SpawnNear(int npcId, float dx, float dy)
    {
        Npc owner = GetOwner();
        Spawn(npcId, owner.GetX() + dx, owner.GetY() + dy, owner.GetZ(), (sbyte)owner.GetHeading());
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
