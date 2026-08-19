using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Researcher Teselik (230850), Sauro Supply Base. Retail pattern <c>IDVritra_Base_Drakan_Wi_Nmd</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He was plain <c>aggressive</c>, and his fight is
/// built around a mechanic our server had no way to express: he keeps a running count of how many of
/// his summoned hands are still alive, and every branch point in the rotation asks that count a
/// question.
/// <list type="bullet">
/// <item>all of them dead — summon a fresh wave, of three or two on a coin flip</item>
/// <item>any still alive — order them to blow up, which clears the count back to zero</item>
/// </list>
/// The count lives in retail's <c>INTVARI_FIRST</c>; each hand decrements it as it dies by
/// broadcasting message 22260, which <see cref="ShebanMysticalTyrhundAI"/> sends and this class's
/// <c>OnMessage</c> answers. See <c>When.CountBelow</c> for the test-and-set semantics.
/// <para>
/// <b>The self-destruct order.</b> Retail casts skill index 5 and the hands act on message 22261, but
/// nothing in the pattern sends 22261 — retail routes it through the skill itself. Our skill engine
/// has no AI-message effect, so the branch broadcasts it alongside the cast. Same observable
/// behaviour, different plumbing.
/// </para>
/// <para>
/// <b>Skill indices.</b> All seven resolve, and our <c>npc_skills</c> list has exactly seven entries —
/// but they are not the same seven. Index 5 is <c>20708 Self-destruct Command</c>, which our data
/// carries only as a comment ("we have no real handling for NPC summon control"), and in its place the
/// list repeats <c>21135</c> twice. Six of seven match by name or by unique attribute:
/// index 4 is the only skill with a <c>spawn_npc</c>; index 1's branch comment is 피의 축복, exactly
/// <c>Blessing of Blood</c>; index 5's is 자폭명령, exactly <c>Self-destruct Command</c>; indices 0 and
/// 6 are the two buffs he self-casts on resetting; and index 2 (불꽃화살, four branches) against index 3
/// (불꽃뿜기, two branches) matches Flame Bolt's 33% against Fire Burst's 23%.
/// </para>
/// <para>
/// <b>Two retail quirks reproduced rather than tidied.</b> Phase two is guarded by a one-shot flag
/// that sits <i>ahead</i> of the count test, so if the hands all happen to be dead at the tick he
/// crosses 65% the flag is spent by the branch that then fails, and the summoning variant below it can
/// never match — phase two is skipped entirely for that fight. And the pair of branches on timer 2
/// alternate through a flag, so his flame bolt switches target every other cast.
/// </para>
/// <para>
/// <b>Already handled elsewhere:</b> most of his death tail. <c>SauroSupplyBaseInstance.OnDie</c>
/// opens the door and sends <c>STR_MSG_IDVritra_Base_DoorOpen_04</c> for this npc id already, which is
/// the Java-parity place for it — an instance's doors belong to the instance handler, not to a
/// monster's AI. Retail expresses it inside the pattern; we do not need to.
/// <para>
/// <b>Not translated:</b> his three shouts (<c>STR_CHAT_IDVritra_Base_Nmd3_01/02/03</c>), which have
/// no numeric id in our data. <b>The four bonus hands (284457) are no longer missing</b>: the death tail
/// places them on <c>NPCPath_Bboss_Hand_01</c>..<c>04</c>, which this repo does have. What is still owed
/// <b>The in-combat hands walk in too now.</b> All six summoning rungs name their own paths — 01+03,
/// 01+02+03, 01+04, 02+03, 02+04, 01+03 — and one rung genuinely places its first hand at his feet, which
/// is kept.
/// </para>
/// </remarks>
[AIName("researcher_teselik")]
public class ResearcherTeselikAI : PatternAi
{
    private const int MidnightRobe = 20700;        // index 0
    private const int BlessingOfBlood = 20701;     // index 1
    private const int FlameBolt = 17335;           // index 2
    private const int FireBurst = 21288;           // index 3
    private const int SummoningRitual = 20657;     // index 4
    private const int SelfDestructCommand = 20708; // index 5
    private const int BeritrasFavor = 21135;       // index 6

    private const int Tyrhund = 284455;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: every hand he places, and the only group he clears.</summary>
    private const int Hands = 1;

    /// <summary>Retail's <c>INTVARI_FIRST</c> — how many hands he believes are standing.</summary>
    private const int LiveHands = 0;

    private const int PhaseTwo = 1;        // FLAGVARI_ALPHA_1
    private const int AlternateBolt = 2;   // FLAGVARI_ALPHA_2

    /// <summary>Sent by a dying hand; the boss takes one off the count.</summary>
    public const int HandDied = 22260;

    /// <summary>Sent by the boss; every hand in range blows up.</summary>
    public const int SelfDestructOrder = 22261;

    private const float MessageRange = 50f;

    /// <summary>
    /// Retail anchors every hand on one of four named server paths.
    /// </summary>
    /// <remarks>
    /// <b>This used to say we did not have them.</b> We do — all four are in
    /// <c>npc_walker/retail_pattern_paths.xml</c>, keyed by retail's own name, and were added after this
    /// remark was written. The stand-in below survived because nothing re-checked the claim; the death
    /// tail now uses the real paths, and the in-combat summons are a separate correction still owed
    /// (see docs/retail-ai-fidelity.md).
    /// </remarks>
    internal static readonly string[] HandPaths =
    [
        "NPCPath_Bboss_Hand_01", "NPCPath_Bboss_Hand_02",
        "NPCPath_Bboss_Hand_03", "NPCPath_Bboss_Hand_04",
    ];

    /// <summary>The four he leaves behind — a different npc from the hands he fights with.</summary>
    internal const int BonusHand = 284457;

    /// <summary>Retail's <c>SPAWN_ID_NONE</c>: the bonus hands belong to nobody and are never cleared.</summary>
    private const int Unowned = 0;

    /// <summary>Retail's <c>spawn_range</c> on all four: how far off the line each one starts.</summary>
    private const float BonusSpread = 5f;

    /// <summary>Retail's <c>spawn_range</c> on the walked-in hands: how far off the line each starts.</summary>
    private const float HandSpread = 0f;

    /// <summary>Where the one hand retail places at his feet arrives.</summary>
    private const float NearHim = 3f;

    /// <summary>
    /// Summons the hands retail's rung summons, each down the path retail names.
    /// </summary>
    /// <remarks>
    /// <b>Every rung names its own paths and they are not interchangeable.</b> Retail's six summoning
    /// rungs use, in priority order: 01+03 on entering combat, 01+02+03 and 01+04 on the low chain,
    /// 02+03 in the phase-two handover, and 02+04 and 01+03 on the healthy chain. A count alone loses
    /// all of that — which is what this class did, placing every hand three metres from his feet.
    /// <para>
    /// One rung is genuinely at his feet: the phase-two handover places its first hand on
    /// <c>SPAWN_LOCATION_MY_POINT</c> and the other two on paths. That asymmetry is retail's, so
    /// <see cref="SummonAtFeetThen"/> keeps it rather than tidying all three onto lines.
    /// </para>
    /// </remarks>
    private static PatternAction Summon(params int[] paths) => ai =>
    {
        foreach (int index in paths)
            ai.SpawnOnPath(Tyrhund, Hands, HandPaths[index - 1], HandSpread, 0);
    };

    /// <summary>The phase-two shape: one at his feet, then the rest walking in.</summary>
    /// <remarks>
    /// <b>Not covered by a pin, and the mutation that proves it survives.</b> Putting all three on paths
    /// instead — the tidier reading, and the one a translator would reach for — is caught by nothing.
    /// Reaching this rung needs his live-hand counter at zero, which only moves when a hand dies and
    /// reports in, plus the phase flag unspent and timer 0 coming round below 65; two attempts to set
    /// that up in the harness produced no summon at all. Recorded rather than left as a green suite that
    /// implies more than it checks. See docs/retail-ai-fidelity.md.
    /// </remarks>
    private static PatternAction SummonAtFeetThen(params int[] paths) => ai =>
    {
        ai.SpawnNear(Tyrhund, Hands, 1, NearHim, 0);
        foreach (int index in paths)
            ai.SpawnOnPath(Tyrhund, Hands, HandPaths[index - 1], HandSpread, 0);
    };

    /// <summary>The order: cast it, and tell the hands, because the skill cannot tell them itself.</summary>
    private static readonly PatternAction[] Detonate =
    [
        Do.SkillOnSelf(SelfDestructCommand),
        Do.Broadcast(SelfDestructOrder, MessageRange),
    ];

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(3, "1. combat starts > summon", [When.CountBelow(LiveHands, 1, 2)],
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(1, 6000),
                Do.SkillOnSelf(SummoningRitual),
                Summon(1, 3))),

        OnBattleTimer = Of(
            // --- the low chain, below 66: T5 -> T6 -> T7 -> T5 -------------------------------------
            Branch(18, "8-2. hands all dead > summon three", [When.Chance(50), When.Timer(7), When.CountBelow(LiveHands, 1, 3)],
                Do.ArmTimer(5, 8000),
                Do.SkillOnSelf(SummoningRitual),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Summon(1, 2, 3)),

            Branch(17, "8-2. hands all dead > summon two", [When.Timer(7), When.CountBelow(LiveHands, 1, 2)],
                Do.ArmTimer(5, 8000),
                Do.SkillOnSelf(SummoningRitual),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Summon(1, 4)),

            Branch(16, "8-1. hands alive > self-destruct order", [When.Timer(7), When.CountAbove(LiveHands, 0, 0)],
                Do.ArmTimer(5, 8000),
                Detonate[0],
                Detonate[1]),

            Branch(15, "7. fire burst", [When.Timer(6)],
                Do.ArmTimer(7, 8000),
                Do.SkillOnTarget(FireBurst)),

            // He hits the current target, pulls a random attacker to the top, then hits that one too.
            Branch(14, "6. flame bolt, twice around a target switch", [When.Timer(5)],
                Do.ArmTimer(6, 8000),
                Do.SkillOnTarget(FlameBolt),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(FlameBolt)),

            // --- phase two, the one-shot handover at 65 -------------------------------------------
            // The flag is tested before the count, and spending it is what makes the pair below
            // mutually exclusive for the whole fight rather than for this tick. See the class remarks.
            Branch(13, "5-2. phase two > hands alive > blessing / self-destruct",
                [When.Timer(0), When.HpBelow(65), When.FirstTime(PhaseTwo), When.CountAbove(LiveHands, 0, 0)],
                Do.ArmTimer(5, 10000),
                Do.SkillOnSelf(BlessingOfBlood),
                Detonate[0],
                Detonate[1]),

            Branch(12, "5-1. phase two > hands all dead > blessing / summon",
                [When.Timer(0), When.HpBelow(65), When.FirstTime(PhaseTwo), When.CountBelow(LiveHands, 1, 3)],
                Do.ArmTimer(5, 10000),
                Do.SkillOnSelf(BlessingOfBlood),
                Do.SkillOnSelf(SummoningRitual),
                SummonAtFeetThen(2, 3)),

            // --- the healthy chain, 66-100: T1 -> T2 -> T3 -> T4 -> T1 ------------------------------
            Branch(11, "8-2. hands all dead > summon two", [When.Chance(50), When.HpBetween(66, 100), When.Timer(4), When.CountBelow(LiveHands, 1, 2)],
                Do.ArmTimer(1, 8000),
                Do.SkillOnSelf(SummoningRitual),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Summon(2, 4)),

            Branch(10, "8-2. hands all dead > summon two", [When.HpBetween(66, 100), When.Timer(4), When.CountBelow(LiveHands, 1, 2)],
                Do.ArmTimer(1, 8000),
                Do.SkillOnSelf(SummoningRitual),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Summon(1, 3)),

            Branch(9, "8-1. hands alive > self-destruct order", [When.HpBetween(66, 100), When.Timer(4), When.CountAbove(LiveHands, 0, 0)],
                Do.ArmTimer(1, 8000),
                Detonate[0],
                Detonate[1]),

            // The one place he breathes fire on himself rather than at the target.
            Branch(8, "4. fire burst", [When.Timer(3), When.HpBetween(66, 100)],
                Do.ArmTimer(4, 8000),
                Do.SkillOnSelf(FireBurst)),

            // These two alternate through one flag: the first tick sets it and switches target, the
            // next consumes it and does not, and so on for as long as the chain runs.
            Branch(7, "3-2. flame bolt", [When.Timer(2), When.HpBetween(66, 100), When.Consuming(AlternateBolt)],
                Do.ArmTimer(3, 8000),
                Do.SkillOnTarget(FlameBolt)),

            Branch(6, "3-1. flame bolt / switch target", [When.Timer(2), When.HpBetween(66, 100), When.FirstTime(AlternateBolt)],
                Do.ArmTimer(3, 8000),
                Do.SkillOnTarget(FlameBolt),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(5, "2. flame bolt", [When.Timer(1), When.HpBetween(66, 100)],
                Do.ArmTimer(2, 8000),
                Do.SkillOnTarget(FlameBolt)),

            // The heartbeat. Timer 0 carries the phase-two handover and nothing else, so without this
            // it would stop ticking at full health and phase two would never arrive.
            Branch(4, "HP recheck", [When.Timer(0)],
                Do.ArmTimer(0, 3000))),

        // A hand died: take one off the count. All the work is in the guard, which is where retail
        // puts it too — the branch itself does nothing.
        OnMessage = Of(
            Branch(2, "summoned-hand death check", [When.Message(HandDied), When.Decrement(LiveHands, 0, 3)])),

        OnEnterIdle = Of(
            Branch(1, "tribe buff", When.Always,
                Do.SkillOnSelf(BeritrasFavor),
                Do.SkillOnSelf(MidnightRobe),
                Do.Despawn(Hands))),

        // Retail hangs this off on_killed_by_user, together with the door and the bonus hands.
        OnDie = Of(
            Branch(18, "clear the hands, then leave four walking in", [When.KilledByPlayer],
                Do.Despawn(Hands),
                Do.SpawnOnPath(BonusHand, Unowned, HandPaths[0], BonusSpread),
                Do.SpawnOnPath(BonusHand, Unowned, HandPaths[1], BonusSpread),
                Do.SpawnOnPath(BonusHand, Unowned, HandPaths[2], BonusSpread),
                Do.SpawnOnPath(BonusHand, Unowned, HandPaths[3], BonusSpread)),

            // A death that no player earned still clears his own hands; it just leaves no bonus.
            Branch(17, "clear the hands", When.Always,
                Do.Despawn(Hands))),
    };

    public ResearcherTeselikAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
