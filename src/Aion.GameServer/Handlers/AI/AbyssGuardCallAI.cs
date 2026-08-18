using System.Collections.Concurrent;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The abyss and fortress guards' call for help. Retail message <c>23000</c>, across fifty-two
/// pattern variants — the <c>[DL]Guard_*</c>, <c>DirectPortal_*</c> and <c>*_Artifact_Killer</c>
/// families among them.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>This is the largest single mechanic in the 5.8
/// dump by npc count</b>: three hundred and ninety live guards, fifty of whom cry out as they are
/// pulled and three hundred and eighty-five of whom answer.
/// <list type="table">
/// <item><term>on being pulled</term><description>broadcast at the guard's own range — twenty, twenty-five
/// or fifty metres — naming the player that pulled it</description></item>
/// <item><term>on hearing it, already fighting</term><description>turn to that player, and nothing
/// else</description></item>
/// <item><term>on hearing it, standing about</term><description>one hate point on that player, and
/// go</description></item>
/// </list>
/// <para>
/// <b>The answer is uniform to a degree nothing else in this project has been.</b> Forty-seven patterns
/// carry the fighting half and forty-seven the idle half; there is no third shape, and the hate value
/// is <c>1</c> in every one of them. One point is not a claim on the player — it is enough to enter
/// combat and no more, so the guard joins the fight and the raid's own threat decides the rest. A class
/// that used a larger number would make every guard in a fortress stick to whoever pulled the first
/// one.
/// </para>
/// <para>
/// <b>The two halves are retail's own split on npc state</b>, and the second one this log has been able
/// to port rather than collapse — the Nochsana teleporter was the first. A guard already in a fight
/// turns without taking hate, so its own attacker keeps it; a guard standing about takes the hate and
/// commits.
/// </para>
/// <para>
/// <b>Most guards only listen.</b> Fifty senders against three hundred and eighty-five listeners is a
/// fortress with a few criers and a great many answerers, and it is why pulling one guard in the abyss
/// has always felt different from pulling one monster.
/// </para>
/// <para>
/// <b>Not translated.</b> The rest of these patterns, which is a great deal: every guard's own cast
/// ladder, the <c>goto_waypoint</c> that walks it back to its post, and three further <c>23000</c>
/// broadcasts that sit on battle timers inside cast chains rather than on the pull. Also retail's
/// <c>is_enemy</c> guard on both halves — our message bus reaches NPCs and the parameter is a player,
/// so the check has nothing to exclude here; it would matter the day a guard broadcasts about another
/// NPC.
/// </para>
/// <para>
/// Message <c>30002</c> — the same pair again but about the <em>sender</em> rather than its target, so
/// one guard sets another on the thing attacking it — is sent by fifty-three patterns and answered by
/// four, of which our data spawns eight npcs. Left for its own pass, with the count recorded so it is
/// not mistaken for the same size as this.
/// </para>
/// </remarks>
[AIName("abyss_guard_call")]
public class AbyssGuardCallAI : PatternAi
{
	/// <summary>Retail's <c>23000</c>: "this one is on me".</summary>
	public const int CallForHelp = 23000;

	/// <summary>Retail's <c>point_to_add</c>, and it is 1 in every one of the forty-seven patterns.</summary>
	private const int JustEnoughToJoin = 1;

	/// <summary>One pattern per guard, because the send range differs and a fortress holds hundreds.</summary>
	private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();

	/// <summary>A guard whose id is not in the table does nothing beyond being aggressive.</summary>
	private static readonly AiPattern Nothing = new AiPattern();

	private static AiPattern Build(int npcId)
	{
		if (!GuardCalls.ByGuard.TryGetValue(npcId, out GuardCalls.Call call))
			return Nothing;

		return new AiPattern
		{
			OnEnterAttack = call.SendRange <= 0
				? Of()
				: Of(Branch(7, "", When.Always,
					Do.Broadcast(CallForHelp, call.SendRange, aboutTarget: true))),

			OnMessage = !call.Answers
				? Of()
				: Of(
					// Already fighting: it turns, and its own attacker keeps its hate.
					Branch(2, "", [When.MessageParamIsEnemy, When.Message(CallForHelp), When.Fighting],
						Do.TargetMessageParam()),

					Branch(1, "", [When.MessageParamIsEnemy, When.Message(CallForHelp)],
						Do.HateMessageTarget(JustEnoughToJoin))),
		};
	}

	private readonly AiPattern pattern;

	public AbyssGuardCallAI(Npc owner)
		: base(owner)
	{
		pattern = ByNpcId.GetOrAdd(owner.GetNpcId(), Build);
	}

	protected override AiPattern Pattern => pattern;
}
