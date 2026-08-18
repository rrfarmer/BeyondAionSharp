using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Unstable Triroan (214669), Theobomos Lab's elemental king. Retail pattern
/// <c>IDLF2A_ElementalKingNmd</c>.
/// </summary>
/// <remarks>
/// Retail-sourced, and it <b>replaces</b> a Java class (<c>ai/instance/theobomosLab/UnstableTriroanAI</c>,
/// @author Ritsu). This is the sanctioned exception to Java-is-spec; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Java gave him eleven fixed health phases; retail gives him a clock that speeds up.</b> aionemu
/// stepped at 99, 90, 80, 70, 60, 50, 40, 30, 20, 10 and 5 percent, each spawning a hard-coded list of
/// elementals itself. Retail has one summon slot, timer 9, whose branch is chosen by the band he is in
/// — and each branch re-arms it at that band's own interval:
/// </para>
/// <list type="table">
/// <item><term>61–80</term><description><b>one</b> elemental</description></item>
/// <item><term>41–60</term><description>one</description></item>
/// <item><term>21–40</term><description><b>two</b></description></item>
/// <item><term>below 20</term><description><b>three</b>, and the fastest clock</description></item>
/// </list>
/// <para>
/// So the pressure rises twice over — more of them, and faster — where the Java reading gave eleven
/// one-off steps and nothing in between.
/// </para>
/// <para>
/// <b>The interval is not the one written on the summon branch, and the difference is worth stating.</b>
/// Retail arms that slot two ways: the branch re-arms it at its own band's figure (thirty seconds in
/// 61–80, twenty-five in the middle, fifteen deep), and the band timer pokes it three seconds after
/// every one of its own twenty-second ticks. <b>The poke always lands first</b>, so in the upper bands
/// the real cadence is about twenty seconds and the branch's own re-arm never gets to fire — removing
/// it changes nothing measurable, and it is left as a deliberate mutation survivor. Reading the branch
/// delays on their own gives the wrong answer; the sweep is what caught it.
/// </para>
/// <para>
/// <b>And he does not summon them himself.</b> He broadcasts a number a hundred metres and the room's
/// controller (<see cref="BabyElementalControllerAI"/>, npc 280983) decides which elements and places
/// them. Our spawn file stands that controller at 602.17/488.805 and the Java class hard-coded its own
/// spawn point as 601.966/488.853 — the same spot, which is what confirms the reading.
/// </para>
/// <para>
/// <b>A correction to what the Java class was doing.</b> It set each elemental's walker to
/// <c>"3101100002"</c>–<c>"3101100005"</c> and started it walking. Those route ids are not in our
/// <c>npc_walker</c> data — Theobomos Lab has eighteen templates and every one is a SHA-style id — so
/// the walk never began, the elementals never arrived, and <see cref="TriroansSummonAI"/>'s
/// helper-skill mechanic, which fires on arriving at a numbered step, has never run in this port.
/// Routing the spawns through the controller therefore loses nothing that worked.
/// </para>
/// <para>
/// <b>Not translated.</b> Nine skill indices and the branches carrying nothing else — timers 1, 3 and
/// 11, and the cast halves of every rung kept here. The treasure box on <c>on_killed_by_user</c>, which
/// is instance reward scripting. And retail's <c>p16</c>, a timer-2 branch below twenty that switches
/// to a random attacker: it is <b>unreachable in retail's own data</b>, because the branch above it
/// carries identical guards, wins first-match, and does not re-arm timer 2. Recorded rather than
/// ported, so nobody restores it as a missing mechanic.
/// </para>
/// </remarks>
[AIName("triroan")]
public class UnstableTriroanAI : PatternAi
{
    /// <summary>
    /// Retail's <c>use_skill(OBJI_SELF, SKILLI_INDEX_8)</c> on entering combat. The Java class had
    /// resolved this index — it fired 16699 at ninety-nine percent — so it is one we can carry.
    /// </summary>
    private const int OpeningWard = 16699;

    /// <summary>Retail's <c>range_as_meter</c> on every call: a hundred metres, room-wide.</summary>
    private const float CallReach = 100f;

    // Retail's battle timer indices.
    private const int Ladder = 0;
    private const int Deep = 2;
    private const int Band21 = 4;
    private const int Band41 = 5;
    private const int Band61 = 6;
    private const int Peel = 8;
    private const int Summon = 9;

    // Retail's ALPHA_1..4, one per band step.
    private const int Below20 = 1;
    private const int Below40 = 2;
    private const int Below60 = 3;
    private const int Below80 = 4;

    /// <summary>Retail's <c>test_probability</c> on the three summon bands and the deep band.</summary>
    private const int HalfTheTime = 50;

    /// <summary>And on the target peel, which retail rolls a third of the time.</summary>
    private const int AThirdOfTheTime = 33;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(25, "", When.Always,
                Do.ArmTimer(Ladder, 5000),
                Do.SkillOnSelf(OpeningWard))),

        OnBattleTimer = Of(
            // ---- the band steps, each once ------------------------------------------------------
            // Retail writes this rung twice, as a 50% variant and a fallback, and the two differ only
            // in which skill they cast. One branch carries everything either of them does that we can.
            Branch(23, "below 20 opens", [When.Timer(Ladder), When.HpBelow(20), When.FirstTime(Below20)],
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(Deep, 25000),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(22, "21-40 opens", [When.Timer(Ladder), When.HpBetween(21, 40), When.FirstTime(Below40)],
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(Band21, 25000)),

            Branch(21, "41-60 opens", [When.Timer(Ladder), When.HpBetween(41, 60), When.FirstTime(Below60)],
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(Band41, 22500)),

            Branch(20, "61-80 opens the whole thing", [When.Timer(Ladder), When.HpBetween(61, 80),
                    When.FirstTime(Below80)],
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(Band61, 22500),
                Do.ArmTimer(Summon, 35000),
                Do.Broadcast(BabyElementalControllerAI.CallOne, CallReach)),

            // ---- the band timers, which exist to keep the summon slot poked ----------------------
            Branch(19, "below 20 turns on somebody, half the time",
                [When.Chance(HalfTheTime), When.Timer(Deep), When.HpBelow(20)],
                Do.ArmTimer(Peel, 1000),
                Do.ArmTimer(Summon, 3000)),

            Branch(18, "a third of the time", [When.Chance(AThirdOfTheTime), When.Timer(Peel)],
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(13, "half the time", [When.Chance(HalfTheTime), When.Timer(Band21), When.HpBetween(21, 40)],
                Do.ArmTimer(Band21, 20000),
                Do.ArmTimer(Summon, 3000)),

            Branch(11, "half the time", [When.Chance(HalfTheTime), When.Timer(Band41), When.HpBetween(41, 60)],
                Do.ArmTimer(Band41, 20000),
                Do.ArmTimer(Summon, 3000)),

            Branch(9, "half the time", [When.Chance(HalfTheTime), When.Timer(Band61), When.HpBetween(61, 80)],
                Do.ArmTimer(Band61, 20000),
                Do.ArmTimer(Summon, 3000)),

            // ---- the summon slot ----------------------------------------------------------------
            Branch(5, "three, every fifteen seconds", [When.Timer(Summon), When.HpBelow(20)],
                Do.ArmTimer(Summon, 15000),
                Do.Broadcast(BabyElementalControllerAI.CallThree, CallReach)),

            Branch(4, "two, every twenty-five", [When.Timer(Summon), When.HpBetween(21, 40)],
                Do.ArmTimer(Summon, 25000),
                Do.Broadcast(BabyElementalControllerAI.CallTwo, CallReach)),

            Branch(3, "one, every twenty-five", [When.Timer(Summon), When.HpBetween(41, 60)],
                Do.ArmTimer(Summon, 25000),
                Do.Broadcast(BabyElementalControllerAI.CallOne, CallReach)),

            Branch(2, "one, every thirty", [When.Timer(Summon), When.HpBetween(61, 80)],
                Do.ArmTimer(Summon, 30000),
                Do.Broadcast(BabyElementalControllerAI.CallOne, CallReach)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 5000))),
    };

    public UnstableTriroanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}


