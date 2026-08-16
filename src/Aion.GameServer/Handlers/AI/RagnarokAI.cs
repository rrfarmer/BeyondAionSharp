using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Ragnarok (216576), the Gelkmaros field raid boss. Retail pattern <c>DF4_FieldRaid</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A LEGENDARY world boss on a twenty-hour respawn,
/// on plain <c>aggressive</c> — he auto-attacked and nothing else, and both of the NPCs his fight is
/// made of were reachable by nobody. His Elyos counterpart <c>LF4_FieldRaid</c> has been ported for a
/// while as <see cref="OmegaAI"/>; this is the other side of the same content.
/// <para>
/// <b>A five-rung ladder on a five-second heartbeat</b>, one-shot at each threshold and deepest
/// first, so a raid that burns him fast skips to the rung it deserves. What each rung does is the
/// same two things in different measures:
/// </para>
/// <list type="table">
/// <item><term>below 85</term><description>five parasites on the tank, and one on each of up to
/// twenty-five others</description></item>
/// <item><term>below 65</term><description>the same again, into its own group</description></item>
/// <item><term>below 45</term><description>the same, <b>and</b> a slime on up to five</description></item>
/// <item><term>below 35</term><description>a slime on up to five</description></item>
/// <item><term>below 30</term><description>a slime on up to five, again</description></item>
/// <item><term>below 25</term><description>five parasites on the tank at a lighter hate, and one on
/// each of up to twenty-five</description></item>
/// </list>
/// <para>
/// Everything he calls lives <b>five minutes</b> and arrives already fighting whoever it landed on.
/// The parasites on the tank at the deepest rung carry <b>fifty</b> hate where every other spawn in
/// the pattern carries a hundred — the one asymmetry in the whole ladder, and it is kept.
/// </para>
/// <para>
/// <b>Two rungs that look like a copy-paste error are not one.</b> Below 35 and below 30 do exactly
/// the same thing, into the same spawn group, behind two different flag vars. That is retail giving
/// the slime step twice on the way down rather than a mistake, and translating it as one step would
/// halve it.
/// </para>
/// <para>
/// <b>Not translated.</b> Fourteen skill indices: the opening cast, three or four on most rungs, and
/// the whole of timer 1 — eight health-banded branches that cast and re-arm and carry nothing else.
/// Timer 2 is armed at 145 seconds and has no branch in the pattern at all, which is retail's own
/// loose end rather than ours. The timer-1 chain is not reproduced as bare re-arms because it is a
/// separate slot and cannot shift the ladder, the same reasoning as
/// <see cref="GatewayTrapGuardAI"/>'s.
/// </para>
/// </remarks>
[AIName("ragnarok")]
public class RagnarokAI : PatternAi
{
    /// <summary><c>DF4_parasite</c> and <c>DF4_slimeFluid</c>.</summary>
    private const int Parasite = 281950;
    private const int Slime = 281951;

    // Retail's SPAWN_ID_1..5, one group per rung and a shared one for the slime.
    private const int Rung25 = 1;
    private const int Rung45 = 2;
    private const int Rung65 = 3;
    private const int Rung85 = 4;
    private const int Slimes = 5;

    private const int AddLife = 300;

    /// <summary>Retail's <c>valid_distance</c> — a hundred metres, which is the whole field.</summary>
    private const float Reach = 100f;

    /// <summary>Retail's <c>total_set_to_spawn</c>: twenty-five for the parasites, five for the slime.</summary>
    private const int ParasiteCap = 25;
    private const int SlimeCap = 5;

    private const int OnTankCount = 5;

    /// <summary>The one asymmetry in the ladder — see the remarks.</summary>
    private const int DeepestTankHate = 50;
    private const int NormalHate = 100;

    // Retail's ALPHA_2..5 and BETA_1..2, one per rung.
    private const int Below25 = 2;
    private const int Below30 = 3;
    private const int Below35 = 4;
    private const int Below45 = 5;
    private const int Below65 = 6;
    private const int Below85 = 7;

    private const int HeartbeatMillis = 5000;

    /// <summary>Five parasites on whoever he is fighting.</summary>
    private static PatternAction OnTheTank(int group, int hate) =>
        Do.SpawnOnTarget(Parasite, group, count: OnTankCount, liveSeconds: AddLife, attackHate: hate);

    /// <summary>And one on everybody else, up to retail's cap.</summary>
    private static PatternAction OnEveryone(int group) =>
        Do.SpawnOnEachTarget(Parasite, group, Reach, maxTargets: ParasiteCap, MultiTargetOrder.Random,
            liveSeconds: AddLife, attackHate: NormalHate);

    private static PatternAction Slimes5 =>
        Do.SpawnOnEachTarget(Slime, Slimes, Reach, maxTargets: SlimeCap, MultiTargetOrder.Random,
            liveSeconds: AddLife, attackHate: NormalHate);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 1 at twenty seconds and timer 2 at a hundred and forty-five; both
        // are casts, and timer 2 has no branch at all.
        OnEnterAttack = Of(
            Branch(20, "", When.Always,
                Do.ArmTimer(0, HeartbeatMillis))),

        OnBattleTimer = Of(
            Branch(19, "below 25", [When.Timer(0), When.HpBelow(25), When.FirstTime(Below25)],
                Do.ArmTimer(0, HeartbeatMillis),
                OnTheTank(Rung25, DeepestTankHate),
                OnEveryone(Rung25)),

            Branch(18, "below 30", [When.Timer(0), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(0, HeartbeatMillis),
                Slimes5),

            Branch(17, "below 35", [When.Timer(0), When.HpBelow(35), When.FirstTime(Below35)],
                Do.ArmTimer(0, HeartbeatMillis),
                Slimes5),

            Branch(16, "below 45", [When.Timer(0), When.HpBelow(45), When.FirstTime(Below45)],
                Do.ArmTimer(0, HeartbeatMillis),
                OnTheTank(Rung45, NormalHate),
                OnEveryone(Rung45),
                Slimes5),

            Branch(15, "below 65", [When.Timer(0), When.HpBelow(65), When.FirstTime(Below65)],
                Do.ArmTimer(0, HeartbeatMillis),
                OnTheTank(Rung65, NormalHate),
                OnEveryone(Rung65)),

            Branch(14, "below 85", [When.Timer(0), When.HpBelow(85), When.FirstTime(Below85)],
                Do.ArmTimer(0, HeartbeatMillis),
                OnTheTank(Rung85, NormalHate),
                OnEveryone(Rung85)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),
    };

    public RagnarokAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
