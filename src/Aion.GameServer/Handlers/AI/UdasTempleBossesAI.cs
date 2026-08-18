using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Anvilface (215794), Lower Udas Temple. Retail pattern <c>IDTP_NepEx1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. On plain <c>aggressive</c> with no AI class, and
/// the one NPC his fight is made of — <b>shatter</b> (281424) — reachable by nobody. Found by
/// <c>tools/client-extract/audit_missing_ai.py</c>, in the same instance as
/// <see cref="KingspinAI"/>.
/// <para>
/// <b>Two one-shot calls, and both go to the third-most-hated.</b> At fifty percent and again at
/// thirty he calls a shatter onto whoever is <em>third</em> on his hate list, and it arrives already
/// fighting them. Not the tank, not a random player — third, both times, which in a party is the
/// second damage dealer or a healer who has been working.
/// </para>
/// <para>
/// They hang off <c>on_attacked</c> rather than a battle timer, so they land on the blow that crosses
/// the threshold rather than on the next tick after it.
/// </para>
/// <para>
/// <b>Not translated.</b> Four skill indices and the three timers that carry them — 0 and 1 are
/// straight cast loops, 2 is a two-branch cast ladder the fifty-percent call arms. Also out: the
/// <c>on_die</c> pair of invisible controllers, for the reason given on
/// <see cref="DebilkarimTheMakerAI"/>.
/// </para>
/// </remarks>
[AIName("anvilface")]
public class AnvilfaceAI : PatternAi
{
    /// <summary><c>BIDTP_Assistant_55_Ae</c>.</summary>
    private const int Shatter = 281424;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Called = 1;

    /// <summary>Retail's <c>hatepoints_to_add</c> with <c>attack_target_after_spawn</c>.</summary>
    private const int OnArrival = 1;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below50 = 1;
    private const int Below30 = 2;

    /// <summary>
    /// Retail's <c>on_die</c> on every boss in the temple: five invisible controllers, one at its
    /// feet and four scattered to twenty-five metres, each of which broadcasts the room clear. See
    /// <see cref="UdasTempleClearAI"/>.
    /// </summary>
    private const int ClearController = 281418;

    internal static PatternBranch DropTheClearControllers(int priority) =>
        Branch(priority, "", When.Always,
            Do.SpawnNear(ClearController, 0, count: 1, range: 1f),
            Do.SpawnNear(ClearController, 0, count: 4, range: 25f));

    private static PatternAction CallShatter =>
        Do.SpawnOnAttacker(AggroTarget.THIRD_MOST_HATED, Shatter, Called, attackHate: OnArrival);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timers 0 and 1 here; both are cast loops.
        OnAttacked = Of(
            Branch(10, "below 30", [When.HpBelow(30), When.FirstTime(Below30)],
                CallShatter),

            Branch(9, "below 50", [When.HpBelow(50), When.FirstTime(Below50)],
                CallShatter)),

        OnDie = Of(DropTheClearControllers(11)),
    };

    public AnvilfaceAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Debilkarim the Maker (215795), Lower Udas Temple. Retail pattern <c>IDTP_NepBoss1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He was on <c>summoner</c>, the generic table AI,
/// and <b>his table is right</b> — someone had already matched the seven nuclei and their four rings
/// to the pattern. What the table cannot express is the other half of his fight.
/// <list type="bullet">
/// <item><b>Below 51</b> — seven <c>protection of aion</c> in four rings around him: two at five
/// metres, two at ten, two at fifteen, one at twenty. The summon table had this exactly.</item>
/// <item><b>Below 19, one hit in ten</b> — three <b>pyre souls</b> (281421) on whoever he is
/// fighting, five metres out. Nothing reached that NPC, and a percentage table has no way to say
/// "sometimes".</item>
/// </list>
/// <para>
/// Moving him onto the pattern runtime keeps the ring and adds the pyre souls. Both branches hang off
/// <c>on_attacked</c>, as retail writes them.
/// </para>
/// <para>
/// <b>The ten percent is retail's and is kept.</b> It cannot be pinned deterministically — the same
/// limitation recorded for the Conquest rotation's shugo odds — so what the pins check is that the
/// souls can arrive below nineteen and never above it.
/// </para>
/// <para>
/// <b>Not translated.</b> Nine skill indices across six timers and the six cast-only rungs of his
/// <c>on_attacked</c> ladder at 81, 71, 61, 41, 31 and 21. Also out: the small treasure box on dying,
/// which is loot rather than behaviour.
/// </para>
/// <para>
/// <b>And the pair of invisible controllers both bosses drop on dying.</b> They are one line —
/// broadcast 6956 to fifty metres and remove themselves — and the four patterns that listen for 6956
/// are <c>IDTP_Keeper2</c>, <c>IDTP_NepBoss2</c>, <c>IDTP_NepBoss3</c> and <c>IDTP_NepEx2</c>, none of
/// which is translated. A sender with no listener, the same shape as the fortress lords' despawn
/// helpers, so it waits for those four.
/// </para>
/// </remarks>
[AIName("debilkarim_the_maker")]
public class DebilkarimTheMakerAI : PatternAi
{
    /// <summary><c>BIDTP_OdNucleus_Summoned_53_n</c> — the ring he raises at half health.</summary>
    private const int Nucleus = 281420;

    /// <summary><c>BIDTP_FurnaceElemental_Summoned_54_n</c> — the pyre souls nothing reached.</summary>
    private const int PyreSoul = 281421;

    /// <summary>Retail files both under <c>SPAWN_ID_NONE</c>: nothing clears them as a group.</summary>
    private const int Untracked = 0;

    private const int SoulsPerCall = 3;
    private const float SoulRange = 5f;

    /// <summary>Retail's <c>test_probability</c> on the pyre souls — see the remarks.</summary>
    private const int SoulChance = 10;

    // Retail's ALPHA_4 on the ring. The pyre-soul branch carries no flag at all, which is what lets
    // it fire more than once.
    private const int Below51 = 4;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnAttacked = Of(
            Branch(10, "below 19", [When.Chance(SoulChance), When.HpBelow(19)],
                Do.SpawnOnTarget(PyreSoul, Untracked, count: SoulsPerCall, range: SoulRange)),

            Branch(6, "below 51", [When.HpBelow(51), When.FirstTime(Below51)],
                Do.SpawnNear(Nucleus, Untracked, count: 2, range: 5f),
                Do.SpawnNear(Nucleus, Untracked, count: 2, range: 10f),
                Do.SpawnNear(Nucleus, Untracked, count: 2, range: 15f),
                Do.SpawnNear(Nucleus, Untracked, count: 1, range: 20f))),

        // Retail also drops a small treasure box here, which is loot rather than behaviour.
        OnDie = Of(AnvilfaceAI.DropTheClearControllers(11)),
    };

    public DebilkarimTheMakerAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Bergrisar (215797), the gatekeeper of Lower Udas Temple. Retail pattern <c>IDTP_Keeper1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The last of the temple's four bosses without a
/// class, and the one with the least to give — <b>almost all of his pattern is blocked</b>, so what
/// is here is small on purpose.
/// <para>
/// <b>What he actually does.</b> A punishment chakra on entering the fight and five more as he is
/// worn down, at 80, 60, 40, 20 and 10 percent, each onto its own absolute mark by the gate. Every
/// one of the six carries a <c>pathname</c> — <c>Path_IDTemple_Low_AI01_1</c> through <c>_6</c> — and
/// those are server-side walk routes we do not have. A chakra is a thing that rolls at you; left
/// standing on its mark it is a different encounter rather than a partial one, which is the call this
/// work has made for the walk-path bucket since it was measured.
/// </para>
/// <para>
/// So what is translated is his <c>on_die</c>: the five clear controllers every boss in the temple
/// drops, which take the room's adds with them. That is the whole of what he has that is not blocked,
/// and it is worth having on its own — he is the boss whose chakras the clear-up exists to remove.
/// </para>
/// <para>
/// <b>Not translated, and worth stating separately from the blocked half:</b> four skill indices on
/// two timers, and the <c>broadcast_message</c> 6955 he sends on entering the fight. That number is
/// the one the temple's nuclei and pyre souls answer with a cast — so his half of that pairing is
/// here and theirs is not, and it stays out until the indices resolve.
/// </para>
/// </remarks>
[AIName("bergrisar")]
public class BergrisarAI : PatternAi
{
    /// <summary>Retail <c>BIDTP_BloodWheel_Summoned_55_n</c>.</summary>
    private const int BloodWheel = 281417;

    /// <summary>Retail's <c>SPAWN_ID_NONE</c> for the wheels; ours needs a group to track them.</summary>
    private const int WheelGroup = 3;

    private const float WheelZ = 154f;

    /// <summary>Retail's <c>FLAGVARI_ALPHA_3</c> on the last band — a <i>world</i> flag, not a per-npc one.</summary>
    private const int LastWheelInWorld = 3;

    /// <summary>
    /// Retail's five <c>on_attacked</c> bands, each placing one blood wheel on its own mark and its own
    /// walk path, each firing once.
    /// </summary>
    /// <remarks>
    /// <b>Retail-sourced; see docs/retail-ai-fidelity.md.</b> This class had only its death step, so the
    /// gatekeeper summoned nothing across a whole fight.
    /// <para>
    /// <b>Written once before and reverted.</b> An earlier pass built these bands, and the suite's own
    /// <c>BergrisarPlacesNoChakrasHimself</c> caught it: a deliberate decision, recorded in this class,
    /// not to place wheels that could not walk their <c>Path_IDTemple_Low_AI01_*</c> routes. That entry
    /// said the decision should be revisited if the routes were ever recovered. <b>They have been</b> —
    /// all five are in <c>npc_walker/retail_pattern_paths.xml</c> — so the wheels walk and the decision
    /// no longer applies.
    /// </para>
    /// <para>
    /// <b>Not translated:</b> the three skill indices on these branches, for the reason this class's
    /// remarks already give, and the <c>say_to_all</c> each band carries, whose string id is unresolved.
    /// </para>
    /// </remarks>
    private static PatternAction PlaceWheel(float x, float y, string path) => ai =>
    {
        ai.SpawnAt(BloodWheel, WheelGroup, 0, new SpawnSpot(x, y, WheelZ, 0));

        // The wheel walks the route retail gives it. Taking the newest of the group is safe because the
        // bands fire once each and a band places exactly one.
        IReadOnlyList<Npc> placed = ai.Spawned(WheelGroup);
        if (placed.Count == 0)
            return;

        Npc wheel = placed[placed.Count - 1];
        wheel.GetSpawn().SetWalkerId(path);
        Aion.GameServer.Ai.Manager.WalkManager.StartWalking((NpcAI)wheel.GetAi());
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnAttacked = Of(
            Branch(9, "80-61", [When.HpBetween(61, 80), When.FirstTime(1)],
                PlaceWheel(1050f, 1647f, "Path_IDTemple_Low_AI01_2")),

            Branch(8, "60-41", [When.HpBetween(41, 60), When.FirstTime(2)],
                PlaceWheel(1066f, 1642f, "Path_IDTemple_Low_AI01_5")),

            Branch(7, "40-21", [When.HpBetween(21, 40), When.FirstTime(3)],
                PlaceWheel(1055f, 1617f, "Path_IDTemple_Low_AI01_3")),

            Branch(6, "20-11", [When.HpBetween(11, 20), When.FirstTime(4)],
                PlaceWheel(1047f, 1647f, "Path_IDTemple_Low_AI01_6")),

            // Retail guards the last one with a world flag rather than a per-npc one, so it fires once
            // for the instance rather than once for this gatekeeper.
            Branch(5, "below 10", [When.HpBelow(10), When.FirstTimeInWorld(LastWheelInWorld)],
                PlaceWheel(1071f, 1645f, "Path_IDTemple_Low_AI01_4"))),

        OnDie = Of(AnvilfaceAI.DropTheClearControllers(11)),
    };

    public BergrisarAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
