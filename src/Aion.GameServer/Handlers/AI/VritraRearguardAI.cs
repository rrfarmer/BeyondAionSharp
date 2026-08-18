using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Guard post rearguard (233487) and defense post rearguard (233477), Engulfed Ophidan Bridge. Retail
/// pattern <c>IDF5_U1_War_Vri_Def01_Ra_SN_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both were on plain <c>aggressive</c>, and both trap
/// types they lay were spawned by nothing anywhere: the drakan mine trap (284693) and the drakan net
/// trap (284692). The instance handler names these two npc ids, but only to award score — it lays no
/// traps.
/// <para>
/// Two chains of eight timers, one per side of 50%, structurally identical and each opening by putting
/// <b>three mine traps</b> on the current target:
/// </para>
/// <list type="bullet">
/// <item><b>above 50</b> — T1 → T2 → … → T8 → T1, at 10, 21, 10, 9, 7, 15, 15 and 9 seconds</item>
/// <item><b>below 50</b> — T9 → T10 → … → T16 → T9, on the same intervals</item>
/// </list>
/// <para>
/// Crossing 50 for the first time also drops <b>two net traps</b> on the current target and switches
/// to a random attacker. Every trap lands within five metres of the target and lives fifteen seconds.
/// </para>
/// <para>
/// <b>A flag pair that can strand it.</b> The branch that lays the net traps tests two one-shots in
/// order, its own latch and the never-again flag. On a <i>second</i> descent past 50 — only reachable
/// if something healed it back up — the latch passes and is spent, the never-again flag then fails,
/// and the branch beneath it, which exists precisely to re-arm the low chain without laying traps,
/// finds the latch already gone. The rearguard then has no chain at all below 50. Reproduced as
/// written; it is the same shape as Researcher Teselik's phase two eating its own flag.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Neither npc has an <c>npc_skills</c> entry at all, and the
/// branch comments are timer labels ("BT0", "BT14") rather than skill names. The chains are kept
/// regardless, as Gatekeeper Flox's were: they are what brings the trap-laying timers round.
/// </para>
/// <para>
/// <b>Not translated:</b> the waypoint return on waking and on leaving the fight, which our AI does on
/// its own; and message 4444444, which adds hate to whoever sent it — nothing in our server sends it,
/// and unlike the two below it carries no object to act on.
/// </para>
/// </remarks>
[AIName("vritra_rearguard")]
public class VritraRearguardAI : PatternAi
{
    private const int NetTrap = 284692;
    private const int MineTrap = 284693;

    /// <summary>Retail's <c>SPAWN_ID_1</c>, which holds the net traps.</summary>
    private const int Nets = 1;

    /// <summary>The mine traps go under <c>SPAWN_ID_NONE</c>: nothing ever clears them as a group.</summary>
    private const int Untracked = 0;

    private const int TrapLife = 15;
    private const float TrapSpread = 5f;

    /// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> — the latch saying "the low chain is running".</summary>
    private const int LowChainLatch = 1;

    /// <summary>Retail's <c>FLAGVARI_BETA_1</c> — the net traps are laid once and never again.</summary>
    private const int NetsLaid = 2;

    /// <summary>Told to stand down.</summary>
    public const int Dismiss = 21221;

    /// <summary>Told who to fight; the message carries the target.</summary>
    public const int Target = 21212;

    private static PatternAction Mines() =>
        Do.SpawnOnTarget(MineTrap, Untracked, count: 3, range: TrapSpread, liveSeconds: TrapLife);

    /// <summary>A link of a chain. The casts do not resolve, so this is the timing alone.</summary>
    private static PatternBranch Step(int priority, int on, int next, int delay)
        => Branch(priority, $"BT{on}", [When.Timer(on), When.HpBelow(50)], Do.ArmTimer(next, delay));

    private static PatternBranch HealthyStep(int priority, int on, int next, int delay)
        => Branch(priority, $"BT{on}", [When.Timer(on), When.HpBetween(51, 100)],
            Do.ArmTimer(next, delay));

    /// <summary>Retail's <c>point_to_add</c> on both 21212 branches.</summary>
    private const int Commit = 100;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(1, 10000))),

        OnBattleTimer = Of(
            // --- timer 0, which chooses the chain -----------------------------------------------------
            Branch(42, "BT0, HP<=50 1st",
                [When.Timer(0), When.HpBelow(50), When.FirstTime(LowChainLatch), When.FirstTime(NetsLaid)],
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(9, 5000),
                Do.SpawnOnTarget(NetTrap, Nets, count: 2, range: TrapSpread, liveSeconds: TrapLife),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(41, "BT0, HP<=50 again",
                [When.Timer(0), When.HpBelow(50), When.FirstTime(LowChainLatch)],
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(9, 5000)),

            Branch(40, "BT0, HP>50 again",
                [When.Timer(0), When.HpBetween(51, 100), When.Consuming(LowChainLatch)],
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(1, 5000)),

            // --- below 50 ------------------------------------------------------------------------------
            Step(38, on: 16, next: 9, delay: 9000),
            Step(37, on: 15, next: 16, delay: 15000),
            Step(36, on: 14, next: 15, delay: 15000),
            Step(35, on: 13, next: 14, delay: 7000),
            Step(34, on: 12, next: 13, delay: 9000),
            Step(33, on: 11, next: 12, delay: 10000),
            Step(32, on: 10, next: 11, delay: 21000),
            Branch(31, "BT9", [When.Timer(9), When.HpBelow(50)],
                Do.ArmTimer(10, 10000),
                Mines()),

            // --- above 50 ------------------------------------------------------------------------------
            HealthyStep(28, on: 8, next: 1, delay: 9000),
            HealthyStep(27, on: 7, next: 8, delay: 15000),
            HealthyStep(26, on: 6, next: 7, delay: 15000),
            HealthyStep(25, on: 5, next: 6, delay: 7000),
            HealthyStep(24, on: 4, next: 5, delay: 9000),
            HealthyStep(23, on: 3, next: 4, delay: 10000),
            HealthyStep(22, on: 2, next: 3, delay: 21000),
            Branch(21, "BT1", [When.Timer(1), When.HpBetween(51, 100)],
                Do.ArmTimer(2, 10000),
                Mines()),

            // Timer 0's own heartbeat. Every branch above it is guarded, so without this a tick that
            // matches none of them would stop it checking for the crossing at 50.
            Branch(1, "BT0, HP BaseBT", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),

        // Retail splits the "who to fight" message in two on whether it is already in combat, and the
        // out-of-combat copy adds an attack order on top. Adding the hate does that by itself here, so
        // the pair is written once.
        OnMessage = Of(
            Branch(100, "stand down", [When.Message(Dismiss)],
                Do.DespawnSelf()),

            // TWO BRANCHES for one number, and retail separates them by ORDER rather than by a pair of
            // state guards: 99 carries is_npc_state ATTACK and only notes the call, and 98 has no state
            // guard at all, so an idle rearguard falls through to it and joins the fight. A rearguard
            // already busy keeps its own target.
            Branch(99, "already fighting, so just note it", [When.Message(Target), When.Fighting],
                Do.HateMessageParam(Commit)),

            Branch(98, "otherwise join", [When.Message(Target)],
                Do.HateMessageParam(Commit),
                Do.SwitchTarget(Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED))),

        OnLeaveAttack = Of(
            Branch(90, "", When.Always, Do.Despawn(Nets))),

        OnDie = Of(
            Branch(90, "", When.Always, Do.Despawn(Nets))),
    };

    public VritraRearguardAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
