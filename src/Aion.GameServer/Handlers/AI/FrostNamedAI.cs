using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Machine Spirit Tottal (235971, Cygnea) and Arcticore Aizenka (219933, Enshar). Retail pattern
/// <c>DF5_ItemNamed_12_SSH</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two HERO world bosses on one pattern with identical
/// skill lists, both on plain <c>aggressive</c>. Three health regimes, each a five-step timer loop,
/// and below 40% a second chain that carpets the ground with frost bombs.
/// <list type="bullet">
/// <item><b>80-100</b> — T0 → T1 → T2 → T3 → T4 → T0 at 10s, 14s, 10.5s, 10s, 14s</item>
/// <item><b>40-80</b> — the same five slots at 11s, 14s, 14s, 10s, 14s, opening with a random-target hit</item>
/// <item><b>below 40</b> — timer 0 becomes the summon, and hands to T5 → T6 → T7 for three more waves</item>
/// </list>
/// <para>
/// <b>The summon is the mechanic.</b> Below 40 each of the four waves puts out <b>six</b> frost bombs
/// (855913) at once — two within five metres, two within ten, two within twenty — eight seconds apart,
/// while timer 1 restarts the ordinary rotation on a 36-second fuse. The bombs run
/// <c>useSkillAndDie</c>, so each detonates five seconds after it lands and removes itself.
/// </para>
/// <para>
/// <b>The doubled summon, resolved.</b> Our <c>npc_skills</c> hung a <c>spawn_npc</c> of three to six
/// bombs off skill 21852 for both bosses — aionemu's stand-in for a summon mechanic it could not
/// otherwise express. With the pattern in place that stand-in double-counts, so it has been removed
/// from both entries and the pattern now owns the summoning: six per wave, four waves, at retail's
/// distances and timings. Same class of change as Teselik's commented-out summon-control skill.
/// </para>
/// <para>
/// <b>Skill indices.</b> Anchored twice on structure rather than on names. Index 4 is the only skill
/// in either list with <c>target="RANDOM"</c>, and every branch that uses it is a random-target branch;
/// index 5 is the only one carrying <c>spawn_npc</c>, is marked <c>max_hp="40"</c>, and is used by
/// exactly the branches guarded below 40. Both land on the identity mapping, which fixes the rest.
/// </para>
/// <para>
/// <b>Three variants collapse to one.</b> Retail writes the paired area attack three ways — centred
/// plus donut at 28%, centred twice at 40%, donut twice as the fallback — and our data resolves both
/// index 2 and index 3 to the <i>same</i> skill, 21850, because aionemu stores it as a chain rather
/// than as two skills. All three variants therefore have identical effect, so they are written once.
/// If the donut is ever separated out, the three branches come back.
/// </para>
/// <para>
/// <b>Not translated:</b> index 0, which the pattern self-casts on waking and on returning to spawn —
/// our index 0 is <c>Sever</c>, an attack, and self-casting an attack on waking is far more likely to
/// be a slot aionemu filled differently than something retail meant; timer 15, which only broadcasts
/// message 60000 to bombs that already detonate on their own clock, so it is not armed; and timer 8,
/// which the last summon wave arms and no branch answers.
/// </para>
/// </remarks>
[AIName("frost_named")]
public class FrostNamedAI : PatternAi
{
    private const int EarthCleave = 21849;    // index 1 — the single hit
    private const int TectonicShift = 21850;  // indices 2 and 3 — centred and donut, one skill here
    private const int GelidImpel = 21851;     // index 4 — the only RANDOM-target skill
    private const int ShiverWrath = 21852;    // index 5 — the only one that summons

    private const int FrostBomb = 855913;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Bombs = 1;

    /// <summary>One wave: six bombs in three rings around him.</summary>
    private static readonly PatternAction[] Wave =
    [
        Do.SpawnNear(FrostBomb, Bombs, count: 2, range: 5f),
        Do.SpawnNear(FrostBomb, Bombs, count: 2, range: 10f),
        Do.SpawnNear(FrostBomb, Bombs, count: 2, range: 20f),
        Do.SkillOnSelf(ShiverWrath),
    ];

    /// <summary>A summon step: arm the next wave, then put one out.</summary>
    private static PatternBranch SummonStep(int priority, int on, int next)
        => Branch(priority, "summon", [When.Timer(on), When.HpBelow(40)],
            [next >= 0 ? Do.ArmTimer(next, 8000) : Do.Custom(_ => { }), .. Wave]);

    /// <summary>The paired area attack, which our data casts as one skill twice. See the remarks.</summary>
    private static PatternBranch AreaPair(int priority, int on, PatternCondition band, int delay)
        => Branch(priority, "centred and donut", [When.Timer(on), band],
            Do.ArmTimer(on + 1, delay),
            Do.SkillOnSelf(TectonicShift),
            Do.SkillOnSelf(TectonicShift));

    private static readonly PatternCondition Healthy = When.HpBetween(80, 100);
    private static readonly PatternCondition Middle = When.HpBetween(40, 80);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always, Do.ArmTimer(0, 3000))),

        OnBattleTimer = Of(
            // --- 80-100 ------------------------------------------------------------------------------
            Branch(50, "single hit", [When.HpBetween(80, 100), When.Timer(0), Healthy],
                Do.ArmTimer(1, 10000),
                Do.SkillOnTarget(EarthCleave)),
            AreaPair(49, on: 1, Healthy, delay: 14000),
            Branch(46, "single hit", [When.HpBetween(80, 100), When.Timer(2), Healthy],
                Do.ArmTimer(3, 10500),
                Do.SkillOnTarget(EarthCleave)),
            Branch(45, "switch target, single hit", [When.HpBetween(80, 100), When.Timer(3), Healthy],
                Do.ArmTimer(4, 10000),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(EarthCleave)),
            Branch(44, "centred and donut", [When.HpBetween(80, 100), When.Timer(4), Healthy],
                Do.ArmTimer(0, 14000),
                Do.SkillOnSelf(TectonicShift),
                Do.SkillOnSelf(TectonicShift)),

            // --- 40-80 -------------------------------------------------------------------------------
            Branch(40, "random-target donut", [When.HpBetween(40, 80), When.Timer(0), Middle],
                Do.ArmTimer(1, 11000),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(GelidImpel)),
            // Retail casts index 1 on itself here where every other band aims it at the target.
            Branch(39, "single hit", [When.HpBetween(40, 80), When.Timer(1), Middle],
                Do.ArmTimer(2, 14000),
                Do.SkillOnSelf(EarthCleave)),
            AreaPair(38, on: 2, Middle, delay: 14000),
            Branch(35, "single hit", [When.HpBetween(40, 80), When.Timer(3), Middle],
                Do.ArmTimer(4, 10000),
                Do.SkillOnTarget(EarthCleave)),
            Branch(34, "random-target donut, twice", [When.HpBetween(40, 80), When.Timer(4), Middle],
                Do.ArmTimer(0, 14000),
                Do.SkillOn(NpcSkillTargetAttribute.RANDOM, GelidImpel),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(GelidImpel)),

            // --- below 40: the summon chain, which outranks the rotation on timer 0 ------------------
            // The first wave also lights timer 1 on a 36-second fuse, so the ordinary rotation runs
            // again alongside the waves rather than being replaced by them.
            Branch(30, "summon", [When.Timer(0), When.HpBelow(40)],
                [Do.ArmTimer(1, 36000), Do.ArmTimer(5, 8000), .. Wave]),
            SummonStep(29, on: 5, next: 6),
            SummonStep(28, on: 6, next: 7),
            // Retail's last wave arms timer 8, which no branch answers; the chain ends here either way.
            SummonStep(27, on: 7, next: -1),

            // --- below 40: the rotation --------------------------------------------------------------
            Branch(20, "single hit", [When.Timer(1), When.HpBelow(40)],
                Do.ArmTimer(2, 10000),
                Do.SkillOnTarget(EarthCleave)),
            // Retail guards this one below 50, not below 40 — the 40-80 branch above claims the band
            // in between by outranking it, which is how the two tile.
            AreaPair(19, on: 2, When.HpBelow(50), delay: 14000),
            Branch(16, "single hit", [When.Timer(3), When.HpBelow(40)],
                Do.ArmTimer(4, 10000),
                Do.SkillOnTarget(EarthCleave)),
            Branch(15, "random-target donut, twice", [When.Timer(4), When.HpBelow(40)],
                Do.ArmTimer(0, 14000),
                Do.SkillOn(NpcSkillTargetAttribute.RANDOM, GelidImpel),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(GelidImpel))),

        OnLeaveAttack = Of(
            Branch(7, "", When.Always, Do.Despawn(Bombs))),

        OnDie = Of(
            Branch(99, "", When.Always, Do.Despawn(Bombs))),
    };

    public FrostNamedAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
