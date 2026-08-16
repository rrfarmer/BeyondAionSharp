using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Vanuka Infernus (215282), Dark Poeta. Retail pattern <c>Dragon_G3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He had **no AI at all** — plain
/// <c>aggressive</c> — in an instance where half the boss roster is implemented: Tahabata Pyrelord
/// and Calindi Flamelord have classes, he and Asaratu Bloodshade did not.
/// <para>
/// His fight is a four-step chain that runs at a different speed in each health band, dropping a
/// flame center at one of four fixed points as it goes, and a separate chain below 30% that summons
/// a lizard every cycle instead:
/// </para>
/// <list type="bullet">
/// <item>81-100 — four steps, 15s apart bar one at 12s, and a pair of flames on the last</item>
/// <item>61-80 — the same four steps faster, with a full ring of four flames on two of them</item>
/// <item>31-60 — slower again, 22s on the long steps, one ring</item>
/// <item>below 30 — timer 0 hands over to a second chain, T5 through T8, which summons a
/// faithful subordinate once per loop and never returns to the first</item>
/// </list>
/// <para>
/// Neither the flame center (281276) nor the subordinate (281275) was spawned by anything in our
/// server. Entering combat lights two flame centers straight away; leaving clears everything.
/// </para>
/// <para>
/// **His casts are not translated.** Ten skills, nine indices addressed, no branch comments to
/// corroborate a mapping — the same position taken on Icaronix and Lost Balor. The chain itself is
/// index-free, so the timing and the spawns are faithful and his npc_skills probabilities still
/// drive what he casts.
/// </para>
/// </remarks>
[AIName("vanuka_infernus")]
public class VanukaInfernusAI : PatternAi
{
    private const int FlameCenter = 281276;
    private const int FaithfulSubordinate = 281275;

    /// <summary>Everything he places goes under one id, and leaving the fight clears it.</summary>
    private const int Adds = 1;

    private const int FlameLife = 10;

    /// <summary>Pattern <c>dir</c> is degrees; the engine's own converter turns it into a heading.</summary>
    private static sbyte Facing(int degrees) =>
        (sbyte)PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    // The four points a flame center occupies. Three of the four branches that drop them use all
    // four at once; the opener and the 81-100 step use two.
    private static readonly SpawnSpot NorthWest35 = new SpawnSpot(1177f, 1241f, 143.322f, Facing(35));
    private static readonly SpawnSpot NorthWest28 = new SpawnSpot(1177f, 1241f, 143.322f, Facing(-28));
    private static readonly SpawnSpot West = new SpawnSpot(1173f, 1231f, 144.788f, Facing(126));
    private static readonly SpawnSpot South = new SpawnSpot(1187f, 1229f, 143.8f, Facing(-138));
    private static readonly SpawnSpot East = new SpawnSpot(1190f, 1238f, 142.651f, Facing(-59));

    /// <summary>A full ring of four, which is what every mid-fight flame step drops.</summary>
    private static PatternAction Ring(SpawnSpot northWest) =>
        Do.SpawnAt(FlameCenter, Adds, FlameLife, northWest, West, South, East);

    /// <summary>The pair the opener and the healthiest step drop.</summary>
    private static PatternAction Pair(SpawnSpot northWest) =>
        Do.SpawnAt(FlameCenter, Adds, FlameLife, northWest, South);

    /// <summary>One link of a chain: arm the next slot, and optionally drop a flame.</summary>
    private static PatternBranch Step(int priority, PatternCondition[] guards, int next, int delay,
        PatternAction? extra = null)
    {
        PatternAction[] actions = extra is null
            ? [Do.ArmTimer(next, delay)]
            : [Do.ArmTimer(next, delay), extra];
        return Branch(priority, "", guards, actions);
    }

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(30, "", When.Always,
                Do.ArmTimer(0, 6000),
                Do.ArmTimer(1, 15000),
                Pair(NorthWest35))),

        OnBattleTimer = Of(
            // The low-health chain, which outranks everything: once timer 0 hands over to timer 5
            // below 30% the fight never returns to the banded steps above.
            Step(29, [When.Timer(8)], next: 5, delay: 9000),
            Step(28, [When.Timer(7)], next: 8, delay: 18000,
                extra: Do.SpawnNear(FaithfulSubordinate, Adds, count: 1, range: 3f)),
            Step(27, [When.Timer(6)], next: 7, delay: 18000),
            Step(26, [When.Timer(5)], next: 6, delay: 18000),

            Step(25, [When.Timer(0), When.HpBelow(30)], next: 5, delay: 9000),

            Step(24, [When.Timer(4), When.HpBetween(31, 60)], next: 1, delay: 22000),
            Step(23, [When.Timer(3), When.HpBetween(31, 60)], next: 4, delay: 15000),
            Step(22, [When.Timer(2), When.HpBetween(31, 60)], next: 3, delay: 15000, extra: Ring(NorthWest35)),
            Step(21, [When.Timer(1), When.HpBetween(31, 60)], next: 2, delay: 15000),
            Step(20, [When.Timer(0), When.HpBetween(31, 60)], next: 1, delay: 22000),

            Step(19, [When.Timer(4), When.HpBetween(61, 80)], next: 1, delay: 12000, extra: Ring(NorthWest28)),
            Step(18, [When.Timer(3), When.HpBetween(61, 80)], next: 4, delay: 15000),
            Step(17, [When.Timer(2), When.HpBetween(61, 80)], next: 0, delay: 7000),
            Step(16, [When.Timer(1), When.HpBetween(61, 80)], next: 2, delay: 15000),
            Step(15, [When.Timer(0), When.HpBetween(61, 80)], next: 1, delay: 12000, extra: Ring(NorthWest35)),

            Step(14, [When.Timer(4), When.HpBetween(81, 100)], next: 1, delay: 15000, extra: Pair(NorthWest28)),
            Step(13, [When.Timer(3), When.HpBetween(81, 100)], next: 4, delay: 15000),
            Step(12, [When.Timer(2), When.HpBetween(81, 100)], next: 3, delay: 15000),
            Step(11, [When.Timer(1), When.HpBetween(81, 100)], next: 2, delay: 12000),

            // The heartbeat. Every banded step above is guarded, so without this a tick that lands
            // between bands would end the chain for the rest of the fight.
            Step(1, [When.Timer(0)], next: 0, delay: 6000)),

        OnEnterIdle = Of(
            Branch(31, "", When.Always, Do.Despawn(Adds))),

        OnDie = Of(
            Branch(31, "", When.Always, Do.Despawn(Adds))),
    };

    public VanukaInfernusAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
