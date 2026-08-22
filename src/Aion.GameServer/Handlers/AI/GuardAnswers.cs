using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Who answers a guard's call for help, and with what weight.
/// </summary>
/// <remarks>
/// <b>The 3,700 rows this class used to carry are in
/// <c>game-server/data/static_data/ai/guard_answers.xml</c> now.</b> They were data written as C#: a
/// dictionary literal that made the file the third-largest in the port and taught the fidelity gate
/// to call it a god-class. The logic below is not data and stays here.
/// <para>
/// The table is read through <see cref="DataManager.GUARD_ANSWER_DATA"/>, the same way every other
/// static_data holder is read, so a change to the data is a data change and does not need a rebuild.
/// </para>
/// </remarks>
internal static class GuardAnswers
{
    /// <summary>Every npc that answers a call, and the calls it answers.</summary>
    internal static IReadOnlyDictionary<int, GuardAnswerRow[]> ByNpc => DataManager.GUARD_ANSWER_DATA.Rows;

    /// <summary>
    /// The rungs one npc answers with, highest priority first, or empty for an npc that answers nothing.
    /// </summary>
    /// <remarks>
    /// The fighting rung is emitted <b>before</b> the idle one for the same call, because branch lists
    /// are first-match-wins and the idle rung's conditions are a subset of the fighting rung's -- emitted
    /// the other way round, a guard already in combat would take the idle answer and never switch.
    /// </remarks>
    internal static PatternBranch[] RungsFor(int npcId)
    {
        if (!ByNpc.TryGetValue(npcId, out GuardAnswerRow[]? answers))
            return [];

        List<PatternBranch> rungs = new List<PatternBranch>(answers.Length * 2);
        int priority = answers.Length * 2;
        foreach (GuardAnswerRow answer in answers)
        {
            // Sender-targeted answers are deliberately not emitted as pattern rungs. 30001 and 30002
            // name the caller rather than a player, and the classes that answer them already carry
            // their own actions; this table bounds WHICH npcs may, through Answers below.
            if (answer.AimsAtSender)
                continue;

            if (answer.Busy >= 0)
            {
                rungs.Add(AiPattern.Branch(priority--, "a call, and I am already fighting",
                    [When.MessageParamIsEnemy, When.Message(answer.Call), When.Fighting],
                    Do.HateMessageTarget(answer.Busy)));
            }

            if (answer.Idle >= 0)
            {
                rungs.Add(AiPattern.Branch(priority--, "a call, and I am not",
                    [When.MessageParamIsEnemy, When.Message(answer.Call)],
                    Do.HateMessageParam(answer.Idle),
                    Do.AttackMostHating()));
            }
        }

        return rungs.ToArray();
    }

    /// <summary>
    /// Whether the table carries this npc at all — distinct from whether it produces any rung.
    /// </summary>
    /// <remarks>
    /// An npc whose retail answer is <c>do_nothing</c> is in the table and produces <b>no</b> rung, and
    /// that is not the same as an npc the table has never heard of. Classes fall back to their own
    /// constants only for the second kind; falling back on an empty rung list would answer for exactly
    /// the guards retail tells to stand still.
    /// </remarks>
    internal static bool Knows(int npcId) => ByNpc.ContainsKey(npcId);

    /// <summary>Whether retail gives this npc an answer to this message at all.</summary>
    /// <remarks>
    /// The gate for answers whose actions live in a class rather than in a rung here. It exists because
    /// <c>AbstractSiegeProtectorAI</c> answered <c>30001</c> for every npc on the class where retail
    /// names a subset, so protectors retail leaves standing dropped everything and charged a waking
    /// killer.
    /// </remarks>
    internal static bool Answers(int npcId, int messageType)
        => ByNpc.TryGetValue(npcId, out GuardAnswerRow[]? answers)
            && Array.Exists(answers, answer => answer.Call == messageType);

    /// <summary>
    /// The same two rungs for a listener that is <b>not</b> pattern-driven, applied directly.
    /// </summary>
    /// <remarks>
    /// Some npcs answer on classes that run plain <c>aggressive</c> with cast rotations this work
    /// cannot resolve, so there is no pattern to fold the rungs into. This is the same shape as
    /// <see cref="PullCalls"/>.<c>Shout</c>, which exists for the sending half and for the same reason.
    /// <para>
    /// The idle rung goes through <see cref="SummonOrder"/>, which <b>is</b> <c>add_hate_point</c>
    /// followed by <c>attack_most_hating</c> -- the same pair, already written and already reasoned
    /// about. The fighting rung is a <c>switch_target</c> and cannot use it: that one turns the npc
    /// whether or not the named player is the one it now hates most.
    /// </para>
    /// </remarks>
    /// <returns>true if this npc had an answer for the message, whether or not it landed.</returns>
    internal static bool AnswerCall(Npc listener, Npc sender, int messageType, VisibleObject? param)
    {
        if (listener.IsDead() || ReferenceEquals(sender, listener))
            return false;

        if (!ByNpc.TryGetValue(listener.GetNpcId(), out GuardAnswerRow[]? answers))
            return false;

        foreach (GuardAnswerRow answer in answers)
        {
            if (answer.Call != messageType)
                continue;

            // 23xxx names a player in the message parameter; 3000x names the caller itself.
            Creature? aim = answer.AimsAtSender ? sender : param as Creature;

            // is_enemy, in the direction the pattern conditions use.
            if (aim is not { } named || named.IsDead() || !named.IsEnemy(listener))
                return true;

            if (answer.Busy >= 0 && listener.GetAi().IsInState(AIState.FIGHT))
            {
                listener.GetAggroList().AddHate(named, answer.Busy);
                listener.SetTarget(named);
                return true;
            }

            if (answer.Idle >= 0)
                SummonOrder.Take(listener, named, answer.Idle);

            return true;
        }

        return false;
    }
}
