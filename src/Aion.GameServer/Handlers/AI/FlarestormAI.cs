using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Flarestorm (216249), the Catacombs fire elemental. Retail pattern <c>IDCT_Boss_ElementalFire</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO boss on plain <c>aggressive</c> with no AI
/// class, found by <c>tools/client-extract/audit_missing_ai.py</c>.
/// <para>
/// <b>His ladder runs the other way, and it is the first one that does.</b> Every threshold pattern
/// translated before this writes its branches deepest-first, so a boss burned down quickly skips to
/// the rung it deserves. Flarestorm's priorities descend with depth instead — <c>p4</c> guards 80,
/// <c>p3</c> guards 60, <c>p2</c> guards 40, <c>p1</c> guards 20 — so the <em>shallowest unconsumed</em>
/// rung is always the one that fires.
/// </para>
/// <para>
/// The consequence is worth spelling out. A raid that drops him from full health to ten percent in
/// one burst does not get the twenty-percent wave: it gets the eighty-percent one on the next hit,
/// the sixty on the one after, and so on. He <b>works up the ladder a hit at a time</b> rather than
/// skipping, so every wave lands however fast he dies — which is the opposite of what deepest-first
/// buys the raid everywhere else.
/// </para>
/// <para>
/// And the waves grow: <b>three</b> calamities at eighty, four at sixty, five at forty, six at
/// twenty, each on the most-hated of that many players and five metres out. Every rung carries a flag
/// var, so each fires once.
/// </para>
/// <para>
/// <b>Not translated.</b> Three skill indices across three timers — 0 loops every twenty seconds and
/// lights 1, 2 loops every seven — and the <c>on_attacked</c> branch above the ladder that reads
/// <c>is_user_class</c> and adds a hate point. We have no vocabulary for either half of that: not the
/// class test, and not a bare hate bump.
/// </para>
/// </remarks>
[AIName("flarestorm")]
public class FlarestormAI : PatternAi
{
    /// <summary><c>IDCatacombs_ElementalFire_sum</c> — "calamity".</summary>
    private const int Calamity = 281646;

    /// <summary>Retail's <c>SPAWN_ID_1</c>. Nothing in the pattern ever clears it.</summary>
    private const int Called = 1;

    /// <summary>Retail's <c>valid_distance</c> and <c>spawn_range</c>.</summary>
    private const float Reach = 50f;
    private const float OnThem = 5f;

    // Retail's ALPHA_1..4, one per rung, shallowest first.
    private const int Below80 = 1;
    private const int Below60 = 2;
    private const int Below40 = 3;
    private const int Below20 = 4;

    /// <summary>One rung: a wave on the most-hated of <paramref name="cap"/> players, once.</summary>
    private static PatternBranch Rung(int priority, int below, int flag, int cap)
        => Branch(priority, $"below {below}", [When.HpBelow(below), When.FirstTime(flag)],
            Do.SpawnOnEachTarget(Calamity, Called, Reach, maxTargets: cap,
                MultiTargetOrder.Descending, range: OnThem));

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Priorities as retail writes them: shallowest first, which is what makes him walk up the
        // ladder rather than skip down it. See the remarks -- this ordering is the mechanic.
        OnAttacked = Of(
            Rung(4, below: 80, flag: Below80, cap: 3),
            Rung(3, below: 60, flag: Below60, cap: 4),
            Rung(2, below: 40, flag: Below40, cap: 5),
            Rung(1, below: 20, flag: Below20, cap: 6)),
    };

    public FlarestormAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
