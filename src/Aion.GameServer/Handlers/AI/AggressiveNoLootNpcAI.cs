using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/AggressiveNoLootNpcAI (Estrayl).</summary>
/// <remarks>
/// <b>A generic variant, not a modelled encounter, and that is why it reads the pattern tables.</b>
/// The whole class is <c>AggressiveNpcAI</c> plus one answer about loot, so an npc bound here is
/// exactly as free to run its retail pattern as one on <c>aggressive</c> -- and 109 patterns were
/// refused solely because this name was not in the extractors' accepted set.
/// <para>
/// The base changes from <c>AggressiveNpcAI</c> to <see cref="PatternAi"/>, which <i>is</i> an
/// <c>AggressiveNpcAI</c>, so nothing about aggression moves. The <c>Ask</c> override is untouched.
/// </para>
/// <para>
/// <b>The distinction being drawn here is worth stating, because it does not generalise.</b> Of the
/// classes gating refused patterns, some model an encounter and must not be second-guessed --
/// <c>EnragedAgent</c> overrides four handlers, <c>StonespearAggressiveNpcAI</c> has its own spawn and
/// death behaviour, <c>NoActionAI</c> exists precisely so that its npcs do <i>not</i> react. Giving
/// any of those a pattern table would double up mechanics somebody has already written, or undo a
/// deliberate inaction. This one has nothing to double up.
/// </para>
/// </remarks>
[AIName("aggressive_no_loot")]
public class AggressiveNoLootNpcAI : PatternAi
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
        new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

    public AggressiveNoLootNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern =>
        ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => GeneratedPattern.For(id));

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}
