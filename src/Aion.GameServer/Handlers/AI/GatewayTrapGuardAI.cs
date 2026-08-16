using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The gateway garrisons' priests and mages. Retail patterns <c>GwLGuard_PhA</c>, <c>GwLGuard_WhA</c>,
/// <c>GwDGuard_PhA</c> and <c>GwDGuard_WhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Twelve guards — six a side, "royal combat priest"
/// and "royal combat mage" — all on plain <c>aggressive</c>, and <b>every trap the four patterns lay
/// was spawned by nothing anywhere</b>. <see cref="GatewayGuardAI"/> covers the named
/// <c>Gw*Guard_FlA</c> fighters, which are a different, longer ladder; these are the rank and file.
/// <para>
/// <b>Two roles, and the difference is where the trap goes.</b> A priest lays its traps <b>at its own
/// feet</b>, so they are ground it is defending; a mage lays them <b>on whoever it is fighting</b>,
/// which follows the player instead. Same three-rung shape either way:
/// </para>
/// <list type="table">
/// <item><term>on engaging</term><description>priest: a net trap. mage: a sleep trap on its
/// target.</description></item>
/// <item><term>below 50</term><description>priest: a flash trap. mage: an acid trap.</description></item>
/// <item><term>below 30</term><description>a rune trap, both roles.</description></item>
/// </list>
/// <para>
/// The two rungs are one-shots and the deeper one outranks the shallower, so a guard that is burned
/// down fast lays the rune trap first and never gets to the flash. Everything lasts a minute and
/// lands within two metres — with two per-pattern quirks kept literal: the <b>Elyos priest's opening
/// net trap lives fifty seconds rather than sixty</b>, and the <b>Asmodian priest lays its opening
/// trap within one metre rather than two</b>.
/// </para>
/// <para>
/// <b>The casts are not translated</b> — eleven indices are addressed across the four patterns and no
/// branch names a skill. That takes with it the whole of timer 1, which is a health-banded cast ladder
/// carrying nothing else, and the <c>on_message</c> surface: four message types, each answered with a
/// cast at whoever the message named. Timer 1 is <b>not</b> reproduced as a bare re-arm the way
/// <see cref="GatewayGuardAI"/>'s empty rungs are, because it is a separate timer slot — it cannot
/// shift the trap ladder's timing, so an empty version of it would be a branch that does nothing at
/// all.
/// </para>
/// </remarks>
[AIName("gateway_trap_guard")]
public class GatewayTrapGuardAI : PatternAi
{
    /// <summary>What one guard lays, and where.</summary>
    /// <param name="OnTarget">
    /// True for the mages: retail uses <c>spawn_on_target</c> for every one of their three, so the
    /// trap lands on the player rather than on the guard.
    /// </param>
    /// <param name="OpeningRange">Retail's <c>spawn_range</c> on the opening trap alone — see remarks.</param>
    /// <param name="OpeningLife">Retail's <c>live_time</c> on the opening trap alone — see remarks.</param>
    private readonly record struct Kit(
        bool OnTarget, int Opening, float OpeningRange, int OpeningLife, int Below50, int Below30);

    // Elyos: net 281477, flash 281478, acid 281479, rune 281480, tranquilizing cloud 281481.
    private static readonly Kit ElyosPriest = new Kit(false, 281477, 2f, 50, 281478, 281480);
    private static readonly Kit ElyosMage = new Kit(true, 281481, 2f, 60, 281479, 281480);

    // Asmodian: archon's net 281487, flash 281488, erosion 281489, archon's magic 281490, sleepdust 281491.
    private static readonly Kit AsmodianPriest = new Kit(false, 281487, 1f, 60, 281488, 281490);
    private static readonly Kit AsmodianMage = new Kit(true, 281491, 2f, 60, 281489, 281490);

    private static readonly Dictionary<int, Kit> ByGuard = new Dictionary<int, Kit>
    {
        // GwLGuard_PhA / GwDGuard_PhA — the priests, one mobile and one rooted to its post.
        [296449] = ElyosPriest,
        [296450] = ElyosPriest,
        [296458] = AsmodianPriest,
        [296459] = AsmodianPriest,

        // GwLGuard_WhA / GwDGuard_WhA — the mages. Four a side: mobile, rooted, and two trap posts.
        [296451] = ElyosMage,
        [296452] = ElyosMage,
        [296894] = ElyosMage,
        [296896] = ElyosMage,
        [296460] = AsmodianMage,
        [296461] = AsmodianMage,
        [296902] = AsmodianMage,
        [296904] = AsmodianMage,
    };

    /// <summary>Retail's <c>SPAWN_ID_NONE</c>: nothing clears them as a group, the minute does.</summary>
    private const int Untracked = 0;

    private const int TrapLife = 60;
    private const float NearIt = 2f;
    private const float Reach = 50f;

    // Retail's ALPHA_1 and ALPHA_2, one per rung.
    private const int Below30Flag = 1;
    private const int Below50Flag = 2;

    private const int HeartbeatMillis = 5000;

    private static void Place(PatternAi ai, Kit kit, int npcId, float range, int liveSeconds)
    {
        if (kit.OnTarget)
            ai.SpawnOnTarget(npcId, Untracked, count: 1, range: range, liveSeconds: liveSeconds);
        else
            ai.SpawnNear(npcId, Untracked, count: 1, range: range, liveSeconds: liveSeconds);
    }

    private static PatternAction LayOpening => ai =>
    {
        if (ByGuard.TryGetValue(ai.GetOwner().GetNpcId(), out Kit kit))
            Place(ai, kit, kit.Opening, kit.OpeningRange, kit.OpeningLife);
    };

    private static PatternAction Lay(System.Func<Kit, int> which) => ai =>
    {
        if (ByGuard.TryGetValue(ai.GetOwner().GetNpcId(), out Kit kit))
            Place(ai, kit, which(kit), NearIt, TrapLife);
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 1 at fifteen seconds here. It is the cast ladder; see the remarks.
        OnEnterAttack = Of(
            Branch(1, "", When.Always,
                Do.ArmTimer(0, HeartbeatMillis),
                LayOpening)),

        OnBattleTimer = Of(
            // Deepest first, so a guard that drops fast reaches the rune trap without walking the rung
            // above it.
            Branch(6, "below 30", [When.Timer(0), When.HpBelow(30), When.FirstTime(Below30Flag)],
                Do.ArmTimer(0, HeartbeatMillis),
                Lay(k => k.Below30)),

            Branch(5, "below 50", [When.Timer(0), When.HpBelow(50), When.FirstTime(Below50Flag)],
                Do.ArmTimer(0, HeartbeatMillis),
                Lay(k => k.Below50)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),
    };

    /// <summary>Whether this npc id is one of the twelve, for the template repoint to be checkable.</summary>
    internal static bool Covers(int npcId) => ByGuard.ContainsKey(npcId);

    public GatewayTrapGuardAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
