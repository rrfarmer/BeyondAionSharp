using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Macunbello, Beshmundir Temple. Retail patterns IDCT_Boss_LichKing (216245) and
/// IDCTH_Boss_LichKing (216164, hard mode).
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. This deliberately diverges from the Java
/// reference, which has no AI class for this boss at all and leaves him casting four skills at
/// a flat 25% chance with no cadence, no adds, and no phases.
/// <para>
/// The fight is three concurrent battle timers plus a message hook:
/// a 10s Shockwave beat on the tank; a 15s self-cast of Tide of Darkness that yields to a
/// one-shot Absorb Energy of Darkness on a random attacker as each HP band is first crossed;
/// and a wave timer that adds soul reapers, two at a time above 50% HP and four below. Each
/// reaper curses a random player and reports it, and Macunbello answers by devouring that
/// exact player.
/// </para>
/// <para>
/// Skill indices resolved from our skill list order, which matches the pattern's indices for
/// the normal-mode boss: 0 Devour Soul, 1 Shockwave, 2 Tide of Darkness,
/// 3 Absorb Energy of Darkness, 4 the spawn-time shield buff.
/// </para>
/// </remarks>
[AIName("macunbello")]
public class MacunbelloAI : AggressiveNpcAI, INpcMessageListener
{
    private const int HardModeNpcId = 216164;

    /// <summary>Latched so the hard-only reaper arrives at most once, as retail's flag var does.</summary>
    private readonly AtomicBoolean hardReaperCalled = new AtomicBoolean();

    private const int DevourSoul = 19051;
    private const int Shockwave = 19052;
    private const int TideOfDarkness = 19053;
    private const int AbsorbEnergyOfDarkness = 19054;
    private const int ShieldBuff = 19049;

    private const int SoulReaper = 281698;
    private const int SoulReaperHard = 281775;

    /// <summary>Retail's <c>test_probability</c> and <c>spawn_range</c> on the hard-only reaper.</summary>
    private const int HardReaperPercent = 5;
    private const float HardReaperRange = 5f;

    private const int ShoutStart = 1500060;
    private const int ShoutWave = 1500061;
    private const int ShoutDevour = 1500062;
    private const int ShoutDie = 1500063;

    /// <summary>Normal mode crosses a band every 20% HP; hard mode every 10%.</summary>
    private static readonly int[] NormalBands = { 91, 71, 51, 31, 11 };
    private static readonly int[] HardBands = { 91, 81, 71, 61, 51, 41, 31, 21, 11 };

    /// <summary>
    /// Add wave positions: the first two are used above 50% HP, all four below. Facings are the
    /// retail values in degrees, converted to the engine's 120-unit heading on use.
    /// </summary>
    private static readonly (float X, float Y, float Z, int Degrees)[] WavePositions =
    {
        (955.54f, 160.44f, 242.3f, 275),
        (1004.12f, 159.32f, 242.3f, 225),
        (952.00f, 110.41f, 243.4f, 90),
        (1007.07f, 110.33f, 243.4f, 90),
    };

    private static sbyte HeadingFromDegrees(int degrees) => (sbyte)(degrees * 120 / 360);

    /// <summary>Queues one of the boss's skills at the level its npc_skills entry defines.</summary>
    private void Cast(int skillId, NpcSkillTargetAttribute target) =>
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), skillId, target);

    private readonly object bandLock = new object();
    private int bandsCrossed;

    private ScheduledTask? phaseTask;
    private ScheduledTask? shockwaveTask;
    private ScheduledTask? waveTask;

    public MacunbelloAI(Npc owner)
        : base(owner)
    {
    }

    private bool IsHardMode => GetNpcId() == HardModeNpcId;

    private int[] Bands => IsHardMode ? HardBands : NormalBands;

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        // Retail casts the shield buff on itself the moment it wakes up, not mid-fight.
        Cast(ShieldBuff, NpcSkillTargetAttribute.ME);
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        StartRotation();
        TryHardModeReaper();
    }

    /// <summary>
    /// Hard mode's own reaper: a five-percent roll on every hit, once per fight.
    /// </summary>
    /// <remarks>
    /// Retail's <c>IDCTH_Boss_LichKing</c> puts this on <c>on_attacked</c> behind
    /// <c>test_probability 5</c> and a <c>set_flag_var</c>, which is its idiom for "rare, and only
    /// the once". It is <b>additional</b> to the wave rather than a substitute: the timed waves spawn
    /// the normal reaper (281698) in both modes, and this hard-only variant (281775) arrives on top of
    /// them at most once.
    /// <para>
    /// The constant for it had been declared and never used, with a note saying so — the two ids
    /// differ by a <c>H</c> in the devname (<c>BIDCT_SumLich</c> against <c>BIDCTH_SumLich</c>) and
    /// read as the same summon, which is exactly the trap that put normal-mode sand in hard-mode
    /// Tiamat.
    /// </para>
    /// </remarks>
    private void TryHardModeReaper()
    {
        if (!IsHardMode || !hardReaperCalled.CompareAndSet(false, true))
            return;

        if (Rnd.NextInt(100) >= HardReaperPercent)
        {
            // The flag is only spent on a successful roll: retail sets it inside the branch the
            // probability guards, so a failed roll leaves the chance open for the next hit.
            hardReaperCalled.Set(false);
            return;
        }

        RndSpawnInRange(SoulReaperHard, HardReaperRange);
    }

    private void StartRotation()
    {
        if (phaseTask != null)
            return;

        PacketSendUtility.BroadcastMessage(GetOwner(), ShoutStart);
        Cast(TideOfDarkness, NpcSkillTargetAttribute.MOST_HATED);

        // Timer 0: the phase beat. Ticks every 10s while a band is pending, otherwise settles
        // into the 15s self-cast of Tide of Darkness.
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnPhaseTick(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(10000));

        // Timer 2: Shockwave on the current target every 10s.
        shockwaveTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnShockwaveTick(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(10000));

        // Timer 1: add waves. The interval changes with HP, so this re-schedules itself.
        ScheduleWave(20000);
    }

    private void OnPhaseTick()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;

        if (TryCrossBand())
        {
            // A band was just crossed: pull a random attacker in and drain them.
            Cast(AbsorbEnergyOfDarkness, NpcSkillTargetAttribute.RANDOM);
            return;
        }

        Cast(TideOfDarkness, NpcSkillTargetAttribute.ME);
    }

    /// <summary>
    /// Returns true the first time each HP band is crossed. Retail latches each band with its
    /// own flag variable, so falling past several at once still only fires one per tick.
    /// </summary>
    private bool TryCrossBand()
    {
        int hp = GetLifeStats().GetHpPercentage();
        lock (bandLock)
        {
            if (bandsCrossed >= Bands.Length || hp >= Bands[bandsCrossed])
                return false;
            bandsCrossed++;
            return true;
        }
    }

    private void OnShockwaveTick()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;
        Cast(Shockwave, NpcSkillTargetAttribute.MOST_HATED);
    }

    private void ScheduleWave(long delay)
    {
        waveTask = ThreadPoolManager.GetInstance().Schedule(
            _ => { OnWaveTick(); return ValueTask.CompletedTask; }, delay);
    }

    private void OnWaveTick()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;

        bool desperate = GetLifeStats().GetHpPercentage() <= 50;
        int count = desperate ? WavePositions.Length : 2;
        PacketSendUtility.BroadcastMessage(GetOwner(), ShoutWave);
        // Both modes spawn the normal-mode reaper here. Hard mode's own variant is the rare on-hit
        // proc in TryHardModeReaper, which is additional to this wave rather than a substitute.
        for (int i = 0; i < count; i++)
        {
            (float x, float y, float z, int degrees) = WavePositions[i];
            Spawn(SoulReaper, x, y, z, HeadingFromDegrees(degrees));
        }

        ScheduleWave(desperate ? 40000L : 30000L);
    }

    /// <summary>
    /// A soul reaper reports the player it just cursed; Macunbello devours that exact player.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        // Retail's on_message p100 DIRECT, ahead of everything else in the handler: the decoy call
        // takes precedence over the curse report, and a lich that is about to vanish does not first
        // devour somebody. See DecoyLichMarkerAI.
        if (messageType == DecoyLichMarkerAI.TheRealOneIsHere)
        {
            AIActions.DeleteOwner(this);
            return;
        }

        if (messageType != MacunbelloSoulReaperAI.CursedMessage || IsDead() || !IsInState(AIState.FIGHT))
            return;
        if (param is not Creature cursed || cursed.IsDead())
            return;

        PacketSendUtility.BroadcastMessage(GetOwner(), ShoutDevour);
        AIActions.TargetCreature(this, cursed);
        Cast(DevourSoul, NpcSkillTargetAttribute.MOST_HATED);
    }

    protected override void HandleDied()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), ShoutDie);
        CancelRotation();
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelRotation();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CancelRotation();
        base.HandleDespawned();
    }

    private void CancelRotation()
    {
        CancelTask(ref phaseTask);
        CancelTask(ref shockwaveTask);
        CancelTask(ref waveTask);
        lock (bandLock)
        {
            bandsCrossed = 0;
        }
    }

    private static void CancelTask(ref ScheduledTask? task)
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
        task = null;
    }
}
