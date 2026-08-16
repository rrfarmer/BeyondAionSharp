using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four elemental lords of Theobomos Lab — Torch Spirit Iprita (214663), Wistful Syripne (214664),
/// Soul Spirit Nomura (214665) and Water Spirit Undine (214666). Retail patterns <c>ND2_FeJ</c>,
/// <c>ND2_AeD</c>, <c>ND2_PeD</c> and <c>ND2_WeH</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All four were on plain <c>aggressive</c> with no
/// class, and between them they account for <b>eight</b> of the adds our server never spawned. They
/// share one shape, which is why they share one builder:
/// <list type="table">
/// <item><term>on engaging</term><description>one <b>lesser</b> elemental, five metres out, five
/// minutes</description></item>
/// <item><term>below 75</term><description>one <b>greater</b> elemental, once</description></item>
/// <item><term>below 30</term><description>one of <b>each</b>, once — so a fight that reaches the last
/// third has four standing</description></item>
/// <item><term>on losing the fight</term><description>every one of them goes</description></item>
/// </list>
/// <para>
/// <b>They peel to the second-most-hated player, and Iprita is the exception.</b> Three of the four
/// turn off the tank at each band crossing and then keep turning every fifteen seconds below thirty;
/// Iprita turns only once, at the thirty-percent crossing, and her equivalent rungs are casts. Two
/// deliberate differences in an otherwise identical family, and the sort of thing that vanishes if you
/// translate one and copy it three times.
/// </para>
/// <para>
/// <b>The despawn on leaving the fight is retail's own, not our reading of a flag.</b>
/// <c>on_leave_attack_state</c> despawns the summon group outright, so a reset really does clear the
/// room — unlike the Drakan camp summons, where retail declares no handler and we leave it to
/// <c>live_time</c>.
/// </para>
/// <para>
/// <b>Not translated.</b> Between four and six skill indices each, and the branches that carry nothing
/// else: Iprita's timer 2, a thirty-second cast loop, and the cast halves of every rung kept here. The
/// summons' own patterns (<c>ND2_FeJSum</c> and its three siblings) are a single cast on a ten- or
/// fifteen-second timer and have nothing else in them at all, so those four npcs stay on
/// <c>aggressive</c> — recorded so the gap is not re-opened.
/// </para>
/// </remarks>
[AIName("elemental_lord")]
public class ElementalLordAI : PatternAi
{
    /// <summary>Retail's <c>SPAWN_ID_1</c>: everything lands in one group so one despawn clears it.</summary>
    private const int Wave = 1;

    /// <summary>Retail's <c>spawn_range</c> and <c>live_time</c>, identical on all eight spawns.</summary>
    private const float Ring = 5f;
    private const int Life = 300;

    // Retail's battle timer indices.
    private const int Ladder = 0;
    private const int Peel = 1;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below30 = 1;
    private const int Below75 = 2;

    /// <summary>
    /// Which lord calls which pair, and whether it peels the way three of the four do. Keyed by the
    /// boss npc id; the lesser elemental is retail's <c>SumA</c> and the greater its <c>SumB</c>.
    /// </summary>
    private static readonly Dictionary<int, (int Lesser, int Greater, bool Peels)> Lords = new()
    {
        [214663] = (280986, 280987, false),   // Iprita: fire, flame
        [214664] = (280992, 280993, true),    // Syripne: breeze, wind
        [214665] = (280990, 280991, true),    // Nomura: earth, mud
        [214666] = (280988, 280989, true),    // Undine: waterdrop, dew
    };

    private static readonly Dictionary<int, AiPattern> Patterns =
        Lords.ToDictionary(e => e.Key, e => Build(e.Value.Lesser, e.Value.Greater, e.Value.Peels));

    private static AiPattern Build(int lesser, int greater, bool peels) => new AiPattern
    {
        OnEnterAttack = Of(
            Branch(8, "", When.Always,
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(Peel, 20000),
                Do.SpawnNear(lesser, Wave, count: 1, range: Ring, liveSeconds: Life))),

        OnLeaveAttack = Of(
            Branch(7, "", When.Always,
                Do.Despawn(Wave))),

        OnBattleTimer = Of(
            Branch(6, "below 30 calls both", [When.Timer(Ladder), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(Ladder, 5000),
                Do.SpawnNear(lesser, Wave, count: 1, range: Ring, liveSeconds: Life),
                Do.SpawnNear(greater, Wave, count: 1, range: Ring, liveSeconds: Life),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Rung31To75(greater, peels),
            DeepPeel(peels),

            Branch(3, "", [When.Timer(Peel), When.HpBetween(31, 75)],
                Do.ArmTimer(Peel, 15000)),

            Branch(2, "", [When.Timer(Peel)],
                Do.ArmTimer(Peel, 20000)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 5000))),
    };

    /// <summary>The 31–75 rung. Iprita calls her greater elemental without coming off the tank.</summary>
    private static PatternBranch Rung31To75(int greater, bool peels)
    {
        PatternCondition[] guards = [When.Timer(Ladder), When.HpBetween(31, 75), When.FirstTime(Below75)];

        return peels
            ? Branch(5, "31-75 calls the greater", guards,
                Do.ArmTimer(Ladder, 5000),
                Do.SpawnNear(greater, Wave, count: 1, range: Ring, liveSeconds: Life),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED))
            : Branch(5, "31-75 calls the greater", guards,
                Do.ArmTimer(Ladder, 5000),
                Do.SpawnNear(greater, Wave, count: 1, range: Ring, liveSeconds: Life));
    }

    /// <summary>
    /// The fifteen-second peel below thirty. Iprita's rung is a cast, but it still re-arms the slot at
    /// fifteen seconds rather than the twenty the fallback gives, so it is kept for the cadence.
    /// </summary>
    private static PatternBranch DeepPeel(bool peels)
    {
        PatternCondition[] guards = [When.Timer(Peel), When.HpBelow(30)];

        return peels
            ? Branch(4, "and keeps peeling", guards,
                Do.ArmTimer(Peel, 15000),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED))
            : Branch(4, "", guards,
                Do.ArmTimer(Peel, 15000));
    }

    private readonly AiPattern pattern;

    public ElementalLordAI(Npc owner)
        : base(owner)
    {
        pattern = Patterns[owner.GetNpcId()];
    }

    protected override AiPattern Pattern => pattern;
}
