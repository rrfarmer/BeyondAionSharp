using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The battle-timer chain both Dragon Lord's Refuge drakan share — retail patterns
/// <c>Dragon_G1SlaveDrakan</c> and <c>Dragon_G2SlaveDrakan</c>, whose timer halves are identical
/// branch for branch and delay for delay.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Above half health it is a plain fighter; below it, it starts rounding on people.</b> Timer 0
/// carries a once-only rung below fifty percent that turns it onto a random attacker and opens a
/// four-stage relay — timer 2 hands to 3, 3 to 4, 4 to 5 — and the far end of that relay turns it
/// again and hands back to timer 2. So the peel comes about every fifty-three seconds, and only
/// after the drakan has been worn down.
/// </para>
/// <para>
/// <b>The relay's middle rungs are casts, and they are kept anyway.</b> Each one exists to arm the
/// next; drop them and the far end never fires, so the peel would happen once and never again. Same
/// for the timer-2 rung above fifty percent, which does nothing but keep that slot ticking so the
/// crossing is answered promptly rather than up to seventeen seconds late.
/// </para>
/// <para>
/// <b>Not translated:</b> four skill indices — the cast on engaging, the self-buff each peel opens
/// with, and the two attack skills the relay carries.
/// </para>
/// </remarks>
internal static class SlaveDrakanPattern
{
    // Retail's battle timer indices, kept as its own numbers so the relay reads against the dump.
    private const int Heartbeat = 0;
    private const int Opening = 1;
    private const int Relay0 = 2;
    private const int Relay1 = 3;
    private const int Relay2 = 4;
    private const int Relay3 = 5;

    /// <summary>Retail's ALPHA_1.</summary>
    private const int BelowHalf = 1;

    internal static PatternBranch[] EnterAttack => Of(
        Branch(9, "", When.Always,
            Do.ArmTimer(Heartbeat, 7000),
            Do.ArmTimer(Opening, 8000)));

    internal static PatternBranch[] BattleTimers => Of(
        Branch(8, "the relay turns it again", [When.Timer(Relay3)],
            Do.ArmTimer(Relay0, 17000),
            Do.SwitchTarget(AggroTarget.RANDOM)),

        Branch(7, "", [When.Timer(Relay2)],
            Do.ArmTimer(Relay3, 12000)),

        Branch(6, "", [When.Timer(Relay1)],
            Do.ArmTimer(Relay2, 12000)),

        Branch(5, "", [When.HpBelow(50), When.Timer(Relay0)],
            Do.ArmTimer(Relay1, 12000)),

        Branch(4, "below half it rounds on somebody", [When.Timer(Heartbeat), When.HpBelow(50), When.FirstTime(BelowHalf)],
            Do.ArmTimer(Heartbeat, 8000),
            Do.ArmTimer(Relay0, 17000),
            Do.SwitchTarget(AggroTarget.RANDOM)),

        Branch(3, "", [When.Timer(Relay0), When.HpBetween(51, 100)],
            Do.ArmTimer(Relay0, 15000)),

        Branch(2, "", [When.Timer(Opening)],
            Do.ArmTimer(Relay0, 15000)),

        Branch(1, "", [When.Timer(Heartbeat)],
            Do.ArmTimer(Heartbeat, 6000)));
}

/// <summary>
/// Tahabata's drakan (281259). Retail pattern <c>Dragon_G1SlaveDrakan</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It steps off the drakan summon spot Tahabata puts
/// out below forty-five percent (<see cref="TahabataSummonSpotAI"/>) and was on plain
/// <c>aggressive</c>, so it fought as an ordinary monster and left nothing behind.
/// <para>
/// <b>It explodes twice over, and differently depending on how it went.</b> Killed by a player it
/// leaves one exploder; removed any other way it leaves the other. Both stand for ten seconds.
/// </para>
/// <para>
/// <b>And retail's two exploders are not the ones you would guess.</b> The despawn branch leaves
/// <c>281260</c>, this drakan's own — but the death branch leaves <c>281269</c>, which belongs to
/// <em>Calindi's</em> drakan. Both npcs are called "exploder" and are the same level and rating, so
/// nothing in play distinguishes them; it reads like a copy-paste in NCSoft's own data. Ported as
/// written, and recorded here so nobody later "corrects" it into consistency.
/// </para>
/// <para>
/// The timer half is <see cref="SlaveDrakanPattern"/>, shared with Calindi's.
/// </para>
/// </remarks>
[AIName("tahabata_drakan")]
public class TahabataDrakanAI : PatternAi
{
    /// <summary><c>BIDLF1_Dragon_G1SlaveDrakanSu_50_An</c> — its own exploder.</summary>
    private const int OwnExploder = 281260;

    /// <summary><c>BIDLF1_Dragon_G2SlaveDrakanSu_50_An</c> — Calindi's, which retail leaves on death.</summary>
    private const int OtherExploder = 281269;

    /// <summary>Retail's <c>SPAWN_ID_1</c> on both branches.</summary>
    private const int Blast = 1;

    private const int BlastLife = 10;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = SlaveDrakanPattern.EnterAttack,
        OnBattleTimer = SlaveDrakanPattern.BattleTimers,

        OnDie = Of(
            Branch(10, "", When.Always,
                Do.SpawnNear(OtherExploder, Blast, count: 1, liveSeconds: BlastLife))),

        OnDespawn = Of(
            Branch(11, "", When.Always,
                Do.SpawnNear(OwnExploder, Blast, count: 1, liveSeconds: BlastLife))),
    };

    public TahabataDrakanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Chramati Firetail (215284), the fifth grade. Retail pattern <c>Dragon_G5</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The only one of the five Dragon Lord's Refuge
/// grades still on plain <c>aggressive</c>, and the whole of its pattern that is not a cast is one
/// thing: <b>ten seconds after something engages it, it turns on whoever is closest to dying</b>, and
/// then again every thirty-five seconds — retail alternates two timer slots, fifteen seconds out and
/// twenty back, which is what makes the gap thirty-five rather than either number.
/// <para>
/// <b>Not translated:</b> three skill indices and the shout on engaging, whose
/// <c>say_to_all_str</c> string has no <c>npc_shouts.xml</c> row.
/// </para>
/// </remarks>
[AIName("chramati_firetail")]
public class ChramatiFiretailAI : PatternAi
{
    private const int Hunt = 0;
    private const int Rest = 1;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(3, "", When.Always,
                Do.ArmTimer(Hunt, 10000))),

        OnBattleTimer = Of(
            Branch(2, "", [When.Timer(Rest)],
                Do.ArmTimer(Hunt, 20000)),

            Branch(1, "the weakest", [When.Timer(Hunt)],
                Do.ArmTimer(Rest, 15000),
                Do.SwitchTarget(AggroTarget.LOWEST_HP))),
    };

    public ChramatiFiretailAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
