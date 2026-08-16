using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Gatekeeper Flox (235975), the Cygnea world boss. Retail pattern <c>LF5_ItemNamed_24_KJS</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. LEGENDARY, on an eight-hour respawn, and on plain
/// <c>aggressive</c>. The watching eye (855728) it calls up is <b>HERO</b>-rated and was spawned by
/// nothing anywhere in the server.
/// <para>
/// Four phases, each a T0 → T1 → T2 → T3 → T0 loop at its own speed. Two of them open by putting an
/// eye out — once between 51 and 75, once below 25:
/// </para>
/// <list type="bullet">
/// <item>76-100 — 15s, 10s, 10s</item>
/// <item>51-75 — an eye, then 10s, 15s, 10s</item>
/// <item>26-50 — 15s, 7s, 15s</item>
/// <item>0-25 — an eye, then 7s, 15s, 5s</item>
/// </list>
/// <para>
/// <b>One eye per phase, not four.</b> Retail writes four branches for each eye, one per cardinal
/// point twenty metres out, and they all share a single one-shot flag. Branches are first-match-wins,
/// so the first of the four whose 25% roll passes spawns its eye and spends the flag; the fourth
/// carries no probability at all and catches the case where the other three miss. The effect is one
/// eye, at one of four places, once per phase — a table that spawned all four would put out eight eyes
/// a fight where retail puts out two.
/// </para>
/// <para>
/// <b>The casts are not translated</b> — twelve skills, indices 0 through 9, and the branch comments
/// are phase labels ("1P", "2P") rather than skill names, so there is nothing to map them onto. Unlike
/// the Golden Tatars, the cast-only chain is <b>kept</b> rather than dropped: T1, T2 and T3 are what
/// bring timer 0 back round, and timer 0 is where the eyes come from. Dropping them would leave the
/// second eye unreachable.
/// </para>
/// <para>
/// <b>Not translated:</b> timer 25 (broadcasts message 550020 and casts index 8 — the message is
/// presumably for the eye, whose own pattern is not ported, and the cast is unresolvable), so it is
/// not armed; <c>on_message</c> 44022; <c>on_see_friend_killed_by_user</c>, which has no counterpart
/// event in our AI; the hate reset and target switch that ride along with several casts; and his four
/// shouts, which have no numeric id in our data.
/// </para>
/// </remarks>
[AIName("gatekeeper_flox")]
public class GatekeeperFloxAI : PatternAi
{
    private const int WatchingEye = 855728;

    /// <summary>Retail's <c>SPAWN_ID_1</c> — the eyes, cleared when the fight ends.</summary>
    private const int Eyes = 1;

    private const int SpawnedMidEye = 2;   // FLAGVARI_ALPHA_2
    private const int SpawnedLowEye = 3;   // FLAGVARI_ALPHA_3

    /// <summary>Twenty metres out, on one of the four cardinal points.</summary>
    private const float Out = 20f;

    /// <summary>
    /// One of the four placements. The first three roll a quarter each and the fourth is the fallback,
    /// so exactly one of them lands — see the class remarks.
    /// </summary>
    private static PatternBranch EyeAt(int priority, int low, int high, int flag, float dx, float dy,
        bool rolls = true)
    {
        PatternCondition[] guards = rolls
            ? [When.Chance(25), When.Timer(0), When.HpBetween(low, high), When.FirstTime(flag)]
            : [When.Timer(0), When.HpBetween(low, high), When.FirstTime(flag)];
        return Branch(priority, "eye", guards,
            Do.ArmTimer(1, 5000),
            Do.SpawnOffset(WatchingEye, Eyes, dx, dy));
    }

    /// <summary>A link of a phase loop: nothing but the timing, since the casts do not resolve.</summary>
    private static PatternBranch Step(int priority, int on, int low, int high, int next, int delay)
        => Branch(priority, $"{low}-{high}", [When.Timer(on), When.HpBetween(low, high)],
            Do.ArmTimer(next, delay));

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(999, "1P", When.Always, Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            // --- 76-100 ------------------------------------------------------------------------------
            Step(998, on: 1, 76, 100, next: 2, delay: 15000),
            Step(997, on: 2, 76, 100, next: 3, delay: 10000),
            Step(996, on: 3, 76, 100, next: 0, delay: 10000),

            // --- 51-75, which opens with an eye ------------------------------------------------------
            EyeAt(899, 51, 75, SpawnedMidEye, 0f, Out),
            EyeAt(898, 51, 75, SpawnedMidEye, Out, 0f),
            EyeAt(897, 51, 75, SpawnedMidEye, -Out, 0f),
            EyeAt(896, 51, 75, SpawnedMidEye, 0f, -Out, rolls: false),
            Step(895, on: 1, 51, 75, next: 2, delay: 10000),
            Step(894, on: 2, 51, 75, next: 3, delay: 15000),
            Step(893, on: 3, 51, 75, next: 0, delay: 10000),

            // --- 26-50 -------------------------------------------------------------------------------
            // Retail's timer-0 branch here is a one-shot that only casts, so it is left out: with the
            // cast gone it would do exactly what the catch-all below already does.
            Step(798, on: 1, 26, 50, next: 2, delay: 15000),
            Step(797, on: 2, 26, 50, next: 3, delay: 7000),
            Step(796, on: 3, 26, 50, next: 0, delay: 15000),

            // --- 0-25, which opens with the second eye ------------------------------------------------
            EyeAt(699, 0, 25, SpawnedLowEye, 0f, -Out),
            EyeAt(698, 0, 25, SpawnedLowEye, -Out, 0f),
            EyeAt(697, 0, 25, SpawnedLowEye, 0f, Out),
            EyeAt(696, 0, 25, SpawnedLowEye, Out, 0f, rolls: false),
            Step(692, on: 1, 0, 25, next: 2, delay: 7000),
            Step(691, on: 2, 0, 25, next: 3, delay: 15000),
            Step(690, on: 3, 0, 25, next: 0, delay: 5000),

            // Timer 0 always leads back to timer 1, whichever band he is in and whether or not an eye
            // went out. Without this the loop would end the moment both flags were spent.
            Branch(1, "BT1 round", [When.Timer(0)],
                Do.ArmTimer(1, 5000))),

        OnLeaveAttack = Of(
            Branch(7, "", When.Always, Do.Despawn(Eyes))),

        OnDie = Of(
            Branch(7, "", When.Always, Do.Despawn(Eyes))),
    };

    public GatekeeperFloxAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
