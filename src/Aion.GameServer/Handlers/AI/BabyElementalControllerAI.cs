using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The summon controller standing in the Unstable Triroan's room (280983). Retail pattern
/// <c>ND2_FhXSum2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by <c>tools/client-extract/audit_translatable.py</c>,
/// which ranks unported patterns by how much of them we could actually write: this one scored
/// <b>twenty-eight translatable actions and nothing blocked at all</b>, the only pattern in the dump
/// with a clean sheet, and it was sitting on an npc our spawn data already places.
/// <para>
/// <b>It is the room's summoner, and the boss only tells it how many.</b> The Triroan broadcasts one
/// of three numbers a hundred metres — <c>6610</c> for one elemental, <c>6611</c> for two,
/// <c>6612</c> for three — and this picks <em>which</em>, from fire, water, earth and air, and puts
/// them down where it stands for thirty seconds. Every combination is a separate branch: four for the
/// single, all six unordered pairs, all four triples.
/// </para>
/// <para>
/// <b>The chains look uniform and are not, and that is worth stating before somebody tidies it.</b>
/// Retail evaluates <c>test_probability</c> <em>before</em> <c>is_message</c> and takes the first
/// branch that passes, so a chain of 25/25/25/fallback is not four equal quarters — it is 25%, 19%,
/// 14% and <b>42%</b> for the last one. The six-way pair chain runs 17% five times over and leaves
/// <b>39%</b> on air+earth. Written in retail's order, with the guards in retail's order, so the
/// weighting comes out of the structure rather than being asserted.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>pathname</c> on all twenty-eight spawns. Retail walks each elemental
/// from here to its station on one of four routes; our <c>npc_walker</c> data has eighteen templates
/// for this instance and not one of them is these. See docs/retail-ai-fidelity.md — the same gap makes
/// <see cref="TriroansSummonAI"/>'s helper skill unreachable, and it already was.
/// </para>
/// </remarks>
[AIName("baby_elemental_controller")]
public class BabyElementalControllerAI : PatternAi
{
    /// <summary>Retail's messages: how many to call, not which.</summary>
    public const int CallOne = 6610;
    public const int CallTwo = 6611;
    public const int CallThree = 6612;

    private const int Fire = 280975;
    private const int Water = 280976;
    private const int Earth = 280977;
    private const int Air = 280978;

    /// <summary>Retail's <c>SPAWN_ID_1</c> and its <c>live_time</c>.</summary>
    private const int Called = 1;
    private const int Life = 30;

    private static PatternAction Call(int npcId) =>
        Do.SpawnNear(npcId, Called, count: 1, liveSeconds: Life);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            // One. The roll is taken before the message is looked at, which is what tilts the chain.
            Branch(14, "", [When.Chance(25), When.Message(CallOne)], Call(Fire)),
            Branch(13, "", [When.Chance(25), When.Message(CallOne)], Call(Air)),
            Branch(12, "", [When.Chance(25), When.Message(CallOne)], Call(Earth)),
            Branch(11, "", [When.Message(CallOne)], Call(Water)),

            // Two: all six unordered pairs, in retail's order.
            Branch(10, "", [When.Chance(17), When.Message(CallTwo)], Call(Fire), Call(Earth)),
            Branch(9, "", [When.Chance(17), When.Message(CallTwo)], Call(Fire), Call(Water)),
            Branch(8, "", [When.Chance(17), When.Message(CallTwo)], Call(Fire), Call(Air)),
            Branch(7, "", [When.Chance(17), When.Message(CallTwo)], Call(Water), Call(Air)),
            Branch(6, "", [When.Chance(17), When.Message(CallTwo)], Call(Water), Call(Earth)),
            Branch(5, "", [When.Message(CallTwo)], Call(Air), Call(Earth)),

            // Three: all four triples, which is the same as naming the one left out.
            Branch(4, "", [When.Chance(25), When.Message(CallThree)], Call(Air), Call(Earth), Call(Fire)),
            Branch(3, "", [When.Chance(25), When.Message(CallThree)], Call(Water), Call(Earth), Call(Fire)),
            Branch(2, "", [When.Chance(25), When.Message(CallThree)], Call(Water), Call(Air), Call(Fire)),
            Branch(1, "", [When.Message(CallThree)], Call(Air), Call(Water), Call(Earth))),
    };

    public BabyElementalControllerAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
