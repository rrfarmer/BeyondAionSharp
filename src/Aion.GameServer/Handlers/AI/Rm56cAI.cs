using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// RM-56c (214802), Azoturan Fortress. Retail pattern <c>NLehpar_BhC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. On plain <c>aggressive</c>, and the complete traps
/// (281281, ELITE) it lays were spawned by nothing anywhere — their only reference in the whole server
/// was their own <c>npc_skills</c> entry.
/// <para>
/// The fight is a trap ladder that thickens as it weakens. Each band lays its own arrangement, once,
/// the first time timer 0 comes round inside that band, and lights the band's own timer:
/// </para>
/// <list type="table">
/// <item><term>61-80</term><description>one trap, underfoot — timer 5, every 25s</description></item>
/// <item><term>41-60</term><description>two, two metres either side — timer 4, every 30s then 25s</description></item>
/// <item><term>21-40</term><description>three, four metres out — timer 3, every 25s then 20s</description></item>
/// <item><term>below 20</term><description>four, on the corners of an eight-metre square — timer 2</description></item>
/// </list>
/// <para>
/// <b>The re-lay path.</b> Each band timer splits on a coin flip: half the time it casts, and half the
/// time it lights timer 9 one second out, whose branches lay that band's arrangement again. So the
/// traps come back roughly every other cycle rather than only once. They live twelve seconds.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Five indices are addressed and our <c>npc_skills</c> carries
/// exactly five skills, which is suggestive, but nothing anchors the mapping: this pattern has
/// <b>no branch comments at all</b>. The one hint on record is that skills 17910 and 17911 are named
/// <c>First Rune Carve</c> and <c>Second Rune Carve</c> — an ordered pair — and indices 0 and 1 are the
/// pair cast together by every trap-laying branch. That constrains their order relative to each other
/// and nothing else; indices 2, 3 and 4 have no anchor whatever. Same refusal as Icaronix, Lost Balor
/// and Prectaz. The five skills keep their 25% probabilities, so the boss still uses all of them.
/// </para>
/// <para>
/// <b>Not translated:</b> the shout, which has no numeric id in our data, and the message it
/// broadcasts to ten metres alongside every trap-laying branch (6681) — the traps run the generic
/// <c>trap</c> AI, which has no listener for it.
/// </para>
/// </remarks>
[AIName("rm_56c")]
public class Rm56cAI : PatternAi
{
    private const int CompleteTrap = 281281;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Traps = 1;

    /// <summary>Twelve seconds, which is why the re-lay path matters.</summary>
    private const int TrapLife = 12;

    // One flag per band: each lays its arrangement once, on first entering that band.
    private const int LaidBelow20 = 1;
    private const int Laid21To40 = 2;
    private const int Laid61To80 = 3;
    private const int Laid41To60 = 4;

    private static PatternAction Trap(float dx, float dy) =>
        Do.SpawnOffset(CompleteTrap, Traps, dx, dy, TrapLife);

    private static readonly PatternAction[] FourTraps =
        [Trap(4f, 4f), Trap(4f, -4f), Trap(-4f, 4f), Trap(-4f, -4f)];

    private static readonly PatternAction[] ThreeTraps =
        [Trap(4f, 0f), Trap(-4f, 4f), Trap(-4f, -4f)];

    private static readonly PatternAction[] TwoTraps = [Trap(-2f, 0f), Trap(2f, 0f)];

    private static readonly PatternAction[] OneTrap = [Trap(0f, 0f)];

    private static readonly PatternCondition Below20 = When.HpBelow(20);
    private static readonly PatternCondition Band21To40 = When.HpBetween(21, 40);
    private static readonly PatternCondition Band41To60 = When.HpBetween(41, 60);
    private static readonly PatternCondition Band61To80 = When.HpBetween(61, 80);

    /// <summary>The one-shot that opens a band: light its timer and lay its traps.</summary>
    private static PatternBranch Opens(int priority, PatternCondition band, int flag, int bandTimer,
        int delay, PatternAction[] traps)
        => Branch(priority, "lay traps", [When.Timer(0), band, When.FirstTime(flag)],
            [Do.ArmTimer(0, 5000), Do.ArmTimer(bandTimer, delay), .. traps]);

    /// <summary>
    /// A band's own timer. The coin flip decides between casting — which does not resolve, so this
    /// branch only re-arms — and lighting timer 9 to lay the traps again a second later.
    /// </summary>
    private static PatternBranch[] BandTimer(int castPriority, int relayPriority, int timer,
        PatternCondition band, int delay)
        => [
            Branch(castPriority, "cast", [When.Chance(50), When.Timer(timer), band],
                Do.ArmTimer(timer, delay)),
            Branch(relayPriority, "re-lay", [When.Timer(timer), band],
                Do.ArmTimer(timer, delay),
                Do.ArmTimer(9, 1000)),
        ];

    private static PatternBranch Relays(int priority, PatternCondition band, PatternAction[] traps)
        => Branch(priority, "re-lay", [When.Timer(9), band], traps);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(1, 20000))),

        OnBattleTimer = Of([
            Opens(18, Below20, LaidBelow20, bandTimer: 2, delay: 25000, FourTraps),
            Opens(17, Band21To40, Laid21To40, bandTimer: 3, delay: 25000, ThreeTraps),
            Opens(16, Band61To80, Laid61To80, bandTimer: 5, delay: 25000, OneTrap),
            Opens(15, Band41To60, Laid41To60, bandTimer: 4, delay: 30000, TwoTraps),

            .. BandTimer(14, 13, timer: 2, Below20, delay: 20000),
            .. BandTimer(12, 11, timer: 3, Band21To40, delay: 20000),
            .. BandTimer(10, 9, timer: 4, Band41To60, delay: 25000),
            .. BandTimer(8, 7, timer: 5, Band61To80, delay: 25000),

            // The healthy band has no trap arrangement and no coin flip — it only casts, so with the
            // casts gone this is a bare re-arm. It is kept because it holds timer 1's slower period.
            Branch(6, "", [When.Timer(1), When.HpBetween(81, 100)],
                Do.ArmTimer(1, 25000)),

            Relays(5, Below20, FourTraps),
            Relays(4, Band21To40, ThreeTraps),
            Relays(3, Band41To60, TwoTraps),
            Relays(2, Band61To80, OneTrap),

            // Every timer carries its own heartbeat. Without these a tick that lands in the seam at
            // exactly 20 — where no band matches — would end that chain for the rest of the fight.
            Branch(1, "", [When.Timer(0)], Do.ArmTimer(0, 5000)),
            Branch(1, "", [When.Timer(1)], Do.ArmTimer(1, 10000)),
            Branch(1, "", [When.Timer(2)], Do.ArmTimer(2, 10000)),
            Branch(1, "", [When.Timer(3)], Do.ArmTimer(3, 10000)),
            Branch(1, "", [When.Timer(4)], Do.ArmTimer(4, 10000)),
            Branch(1, "", [When.Timer(5)], Do.ArmTimer(5, 10000)),
        ]),

        OnLeaveAttack = Of(
            Branch(7, "", When.Always, Do.Despawn(Traps))),

        OnDie = Of(
            Branch(7, "", When.Always, Do.Despawn(Traps))),
    };

    public Rm56cAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
