using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Naga sorcerer (290126) and Captain Lahbri (256115), Reshanta. Retail pattern <c>Naga_WrF</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both HERO, both on plain <c>aggressive</c>, and the
/// naga slave (290127, ELITE) they call up was spawned by nothing anywhere — its only trace in the
/// server was its own <c>npc_skills</c> entry.
/// <para>
/// <b>Four slaves, on the current target.</b> They arrive once on first dropping into 41-60, and then
/// every ninety seconds for as long as the fight stays in that band. Each lands within ten metres of
/// whoever the captain was facing. Below 41 the ninety-second timer stops matching and no more come.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Ten indices are addressed; Captain Lahbri has ten skills and
/// the naga sorcerer has <b>no <c>npc_skills</c> entry at all</b>, so one of the two could not cast
/// anything even with a mapping — and this pattern has no branch comments to build one from. Same
/// refusal as Icaronix, Prectaz, RM-56c and Karemiwen.
/// </para>
/// <para>
/// What that leaves out is the rest of the ladder: a one-shot per health band on timer 1, each
/// lighting a cast-only ping-pong pair — T2/T3 at 76-90 and again at 61-75, T5/T6 at 21-40, T7/T8
/// below 20 — plus a broadcast of message 3315 at 21-40 and three shouts. None of them spawns or
/// moves anything, so arming them would schedule work forever to do nothing. Only the band that
/// summons is kept, together with the timer-1 heartbeat that carries the fight into it.
/// </para>
/// <para>
/// <b>One deliberate divergence.</b> Retail clears the slaves only on death, and relies on
/// <c>despawn_at_attack_state</c> to tidy them otherwise — a flag our runtime does not model. Their
/// <c>live_time</c> is fifty minutes, so without something standing in for it a reset would strand
/// four ELITE adds in the abyss for the best part of an hour. They are cleared on leaving the fight
/// as well.
/// </para>
/// </remarks>
[AIName("naga_captain")]
public class NagaCaptainAI : PatternAi
{
    private const int NagaSlave = 290127;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Slaves = 1;

    /// <summary>Fifty minutes, which is why leaving the fight has to clear them.</summary>
    private const int SlaveLife = 3000;

    private const float AroundTheTarget = 10f;

    /// <summary>Retail's <c>FLAGVARI_BETA_1</c> — the band is entered once.</summary>
    private const int EnteredSummonBand = 1;

    /// <summary>Retail's <c>FLAGVARI_GAMMA_1</c>: the dismissal happens once, at 21-40.</summary>
    private const int Dismissed = 2;

    /// <summary>
    /// The call that detonates the slaves. They answer it themselves — see <see cref="NagaSlaveAI"/>.
    /// </summary>
    public const int Dismiss = 3315;
    private const float DismissRange = 50f;

    private static readonly PatternCondition SummonBand = When.HpBetween(41, 60);

    private static PatternAction Slaves4() =>
        Do.SpawnOnTarget(NagaSlave, Slaves, count: 4, range: AroundTheTarget, liveSeconds: SlaveLife);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(17, "", When.Always, Do.ArmTimer(0, 7000))),

        OnBattleTimer = Of(
            Branch(16, "", [When.Timer(0)],
                Do.ArmTimer(1, 7000)),

            // The repeating call, once the band has been entered.
            Branch(9, "summon", [When.HpBetween(41, 60), When.Timer(4), SummonBand],
                Do.ArmTimer(4, 90000),
                Slaves4()),

            // Entering 41-60 for the first time: light the ninety-second timer and call at once.
            Branch(8, "summon", [When.HpBetween(41, 60), When.Timer(1), SummonBand, When.FirstTime(EnteredSummonBand)],
                Do.ArmTimer(1, 6000),
                Do.ArmTimer(4, 90000),
                Slaves4()),

            // Dropping to 21-40, it detonates whatever it called up. One-shot: the slaves are gone
            // afterwards and the band is only entered once.
            Branch(10, "detonate the slaves",
                [When.Timer(1), When.HpBetween(21, 40), When.FirstTime(Dismissed)],
                Do.ArmTimer(1, 12000),
                Do.Broadcast(Dismiss, DismissRange)),

            // Timer 1's heartbeat. Every band branch above it is guarded, so this is what carries the
            // fight down through the bands that were not translated and into the one that was.
            Branch(1, "", [When.Timer(1)],
                Do.ArmTimer(1, 6000))),

        OnLeaveAttack = Of(
            Branch(18, "", When.Always, Do.Despawn(Slaves))),

        OnDie = Of(
            Branch(18, "", When.Always, Do.Despawn(Slaves))),
    };

    public NagaCaptainAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
