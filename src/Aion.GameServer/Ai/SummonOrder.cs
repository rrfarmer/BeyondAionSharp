using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai;

/// <summary>
/// Retail's <c>add_hate_point</c> followed by <c>attack_most_hating</c>: a summon told which player
/// its master wants it on.
/// </summary>
/// <remarks>
/// A boss that places a wave and then broadcasts, naming its current target, is a shape that recurs
/// across unrelated encounters — Queen Modor's pillar trio, Frostmane Lestin's air elementals — and
/// the listener branch is identical every time. See docs/retail-ai-fidelity.md.
/// <para>
/// <b>The pair is not a target switch, and collapsing it would be a stronger mechanic than retail
/// ships.</b> The hate added is a single point, and what follows attacks whoever is <em>then</em>
/// most-hated rather than the named player. On a summon that has just appeared and holds no hate
/// those are the same thing, which is the whole design: the order assigns an unassigned add. On one
/// that has built real hate on somebody else they are not, and it correctly does nothing.
/// </para>
/// <para>
/// It lives outside <see cref="Pattern.PatternAi"/> because the listeners are not pattern-driven:
/// they run plain <c>aggressive</c> with cast rotations this work cannot resolve, and only this one
/// branch of their pattern is index-free.
/// </para>
/// </remarks>
public static class SummonOrder
{
    /// <summary>Retail's bare <c>add_hate_point</c>, which carries no value.</summary>
    public const int OnePoint = 1;

    /// <summary>Takes the order, if there is anyone to take it for.</summary>
    /// <returns>Whoever the summon is now fighting, or null if the order was dropped.</returns>
    /// <remarks>
    /// Takes the <see cref="Npc"/> rather than its AI, as <see cref="Pattern.AttackAfterSpawn"/> does:
    /// the aggro list and the state flip are reachable from the owner and protected on the AI, and
    /// one op that works for any listener is worth more than one tied to a base class.
    /// </remarks>
    public static Creature? Take(Npc summon, VisibleObject? named, int hate = OnePoint)
    {
        if (summon.IsDead() || named is not Creature player || player.IsDead())
            return null;

        summon.GetAggroList().AddHate(player, hate);

        if (summon.GetAggroList().GetTarget(AggroTarget.MOST_HATED) is not Creature mostHated)
            return null;

        summon.GetAi().SetStateIfNot(AIState.FIGHT);
        summon.SetTarget(mostHated);
        summon.GetAi().OnCreatureEvent(AiEventType.Attack, mostHated);
        return mostHated;
    }
}
