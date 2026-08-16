using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The krall trap-layers of Beluslan and Morheim. Retail patterns <c>NKrall_ReA</c>,
/// <c>NKrall_ReB</c>, <c>NKrall_ReC</c> and <c>Nkrall_RhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Twenty-five spawned npcs</b> across four
/// patterns, every one of them on plain <c>aggressive</c> — the largest single group of world NPCs
/// this work has found with a mechanic and no class. They lay traps, and the traps already had an AI
/// (<see cref="NTrapAI"/>); nothing had ever placed one.
/// <list type="table">
/// <item><term>on engaging</term><description>a trap goes down at its own feet, two metres out</description></item>
/// <item><term>every twenty seconds</term><description>another one</description></item>
/// <item><term>the heavy trappers, once, below 35%</term><description>a <b>powerful</b> trap — but only
/// if something is inside six metres</description></item>
/// </list>
/// <para>
/// <b>The escape rung is melee-only.</b> Retail guards it on
/// <c>is_distance_shorter_than OBJI_CUR_TARGET distance=6</c>, so a group killing the krall at range
/// never sees the powerful trap at all. That half is pinned.
/// </para>
/// <para>
/// <b>Retail limits that rung to one firing twice over, and neither guard is observable here.</b> It
/// carries a flag var <em>and</em> declines to re-arm timer 0, so the branch can only ever run once —
/// and because the only other branch on that slot is a bare re-arm, removing either guard on its own
/// changes nothing we can measure. Both are carried, and the mutation sweep leaves the flag var as a
/// deliberate survivor rather than pretending a pin covers it. The redundancy is presumably for
/// retail's benefit: there the krall runs after laying it, and the dead clock is what stops it turning
/// round to try again.
/// </para>
/// <para>
/// <b>The three variants differ in the trap tier and in which trap goes where.</b> The lv38–41
/// trappers lay the level-38 ordinary trap on both the opening and the loop and keep the powerful one
/// for the escape; the lv28–36 scouts open ordinary and loop <em>powerful</em>; Chieftain Kurka and the
/// kishar seeker do the same with the level-38 pair. Which npc is which is in
/// <c>npc_templates.xml</c> rather than in a table here, because that is where a reader looks for it.
/// </para>
/// <para>
/// <b>Not translated, and the reason is worth the space.</b> Every one of these patterns ends its
/// escape or its trap loop with <c>flee_from</c>, and answers <c>on_stop_to_flee</c> when it stops
/// running. We have neither. <c>flee_from</c> appears <b>353 times across 226 patterns</b> in the 5.8
/// dump and <c>on_stop_to_flee</c> 138 times — after <c>goto_waypoint</c> it is the largest single
/// piece of vocabulary we are missing, and it needs movement work our pattern harness cannot pin
/// (no geodata, no visibility). Recorded with its size so the next attempt knows what it buys.
/// </para>
/// <para>
/// Also not translated: two skill indices per pattern; the <c>say_to_all</c> lines, which have no
/// <c>npc_shouts.xml</c> row; the <c>1001</c> broadcast on stopping fleeing, which is unreachable for
/// the same reason as the flee itself; and the <c>6199</c> listener on the scouts and Kurka. That last
/// one is a trap telling the krall who tripped it — a real mechanic, and its only retail sender is
/// pattern <c>D2_Trap</c>, which binds to no npc our world places. A listener with no speaker, and it
/// stays that way until the trap npcs' own binding is resolved.
/// </para>
/// </remarks>
[AIName("krall_trapper")]
public class KrallTrapperAI : PatternAi
{
    /// <summary><c>BDF2_Monster_trapA_38_An</c> and <c>trapB</c> — the level-38 pair.</summary>
    private const int HeavyTrap = 280451;
    private const int HeavyPowerfulTrap = 280452;

    /// <summary>Retail's <c>SPAWN_ID_1</c> and its <c>spawn_range</c>.</summary>
    internal const int Laid = 1;
    internal const float Reach = 2f;

    /// <summary>Retail's <c>live_time</c>: the opener has none, the loop's sixty seconds, the heavy one fifty minutes.</summary>
    private const int LoopLife = 60;
    private const int PowerfulLife = 3000;

    private const int Escape = 0;
    private const int Loop = 1;

    /// <summary>Retail's ALPHA_1: the escape is once a fight.</summary>
    private const int Escaped = 1;

    /// <summary>Retail's <c>distance</c> on the escape rung.</summary>
    private const int MeleeRange = 6;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(5, "", When.Always,
                Do.ArmTimer(Escape, 6000),
                Do.ArmTimer(Loop, 20000),
                Do.SpawnNear(HeavyTrap, Laid, count: 1, range: Reach))),

        OnBattleTimer = Of(
            // Does not re-arm timer 0, so the escape watch is over once it fires. Retail runs from
            // here, which we cannot express.
            Branch(3, "lay the heavy one and run", [When.Timer(Escape), When.HpBelow(35),
                    When.TargetWithin(MeleeRange), When.FirstTime(Escaped)],
                Do.SpawnNear(HeavyPowerfulTrap, Laid, count: 1, range: Reach, liveSeconds: PowerfulLife)),

            Branch(2, "another one", [When.Timer(Loop)],
                Do.ArmTimer(Loop, 20000),
                Do.SpawnNear(HeavyTrap, Laid, count: 1, range: Reach, liveSeconds: LoopLife)),

            Branch(1, "", [When.Timer(Escape)],
                Do.ArmTimer(Escape, 6000))),
    };

    public KrallTrapperAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The krall scouts of the low-level camps (lv28–36). Retail pattern <c>NKrall_ReC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The same idea as <see cref="KrallTrapperAI"/> with
/// no escape rung and the two traps the other way round: the ordinary one opens the fight and a
/// <b>powerful</b> one goes down every twenty seconds after it.
/// <para>
/// <b>Retail's <c>live_time</c> on these is a ceiling, not a duration, and that is worth knowing
/// before reading anything into it.</b> The numbers differ wildly across the four patterns — none at
/// all, sixty seconds, fifty minutes — but <see cref="NTrapAI"/> fires the trap's one skill on waking
/// and removes it when that lands, measured at about five seconds. So a trap is a one-shot area
/// effect wherever it comes from, and the lifetimes only ever mattered for a trap nobody triggered.
/// Carried as written; recorded so the differences are not mistaken for a mechanic.
/// </para>
/// </remarks>
[AIName("krall_scout_trapper")]
public class KrallScoutTrapperAI : PatternAi
{
    /// <summary><c>BDF2_Monster_trapA_29_An</c> and <c>trapB</c> — the level-29 pair.</summary>
    private const int Trap = 280449;
    private const int PowerfulTrap = 280450;

    private const int Loop = 0;
    private const int OpenerLife = 3000;
    private const int LoopLife = 60;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(2, "", When.Always,
                Do.ArmTimer(Loop, 20000),
                Do.SpawnNear(Trap, KrallTrapperAI.Laid, count: 1, range: KrallTrapperAI.Reach,
                    liveSeconds: OpenerLife))),

        OnBattleTimer = Of(
            Branch(3, "another one", [When.Timer(Loop)],
                Do.ArmTimer(Loop, 20000),
                Do.SpawnNear(PowerfulTrap, KrallTrapperAI.Laid, count: 1, range: KrallTrapperAI.Reach,
                    liveSeconds: LoopLife))),
    };

    public KrallScoutTrapperAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Chieftain Kurka (211039) and the crack kishar seeker (212263). Retail pattern <c>Nkrall_RhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The scouts' shape with the level-38 traps — the
/// trap tier is the only thing separating the two patterns once <c>live_time</c> is understood as the
/// ceiling it is (see <see cref="KrallScoutTrapperAI"/>).
/// </remarks>
[AIName("krall_hunter_trapper")]
public class KrallHunterTrapperAI : PatternAi
{
    private const int Trap = 280451;
    private const int PowerfulTrap = 280452;

    private const int Loop = 0;
    private const int Life = 60;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(2, "", When.Always,
                Do.ArmTimer(Loop, 20000),
                Do.SpawnNear(Trap, KrallTrapperAI.Laid, count: 1, range: KrallTrapperAI.Reach,
                    liveSeconds: Life))),

        OnBattleTimer = Of(
            Branch(3, "another one", [When.Timer(Loop)],
                Do.ArmTimer(Loop, 20000),
                Do.SpawnNear(PowerfulTrap, KrallTrapperAI.Laid, count: 1, range: KrallTrapperAI.Reach,
                    liveSeconds: Life))),
    };

    public KrallHunterTrapperAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
