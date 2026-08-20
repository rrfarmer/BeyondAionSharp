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
/// <b>There is a second call family, and this estimate of it was badly wrong.</b> It used to read
/// "message 30002 ... sent by fifty-three patterns and answered by four, of which our data spawns
/// eight npcs". Measured with <c>tools/client-extract/audit_npc_call_family.py</c>: <b>88 patterns and
/// 807 of our npcs</b> — 487 artifact protectors, 158 base protectors, 38 abyss guards and 96 on plain
/// <c>aggressive</c>.
/// </para>
/// <para>
/// It is a different mechanic from this one, not a variant. <c>30001</c> and <c>30002</c> name the
/// <em>sender</em> and carry <c>points_to_add=1000000</c>, where <c>23000</c> names a player and
/// carries 1. A million is not a nudge — whoever hears it drops what it is doing and goes for the
/// caller, because these are npc-versus-npc: an artifact guard shouts 30002 and the fortress killer
/// comes for it; the killer shouts 30001 on waking and every guard within fifty metres turns on the
/// killer. <c>30003</c> is a despawn order. <b>None of the three is implemented anywhere in this
/// port</b>, so a fortress currently changes hands without any of it happening.
/// </para>
/// </remarks>
[AIName("abyss_guard_call")]
public class AbyssGuardCallAI : PatternAi
{
	/// <summary>Retail's <c>23000</c>: "this one is on me".</summary>
	public const int CallForHelp = 23000;

	/// <summary>
	/// Retail's <c>point_to_add</c> on the idle answer, and it is 1 in all 85 of them. A nudge: enough
	/// to put the player on the list, not enough to outrank whoever the guard is already owed.
	/// </summary>
	private const int JustEnoughToJoin = 1;

	/// <summary>
	/// Retail's <c>point_to_add</c> on the busy answer, and it is 100 in all 85 of them. The old code
	/// switched target and added <b>no</b> hate at all, so the guard turned to face a player it had no
	/// standing quarrel with and drifted back the moment anything else scored a hit.
	/// </summary>
	private const int EnoughToOutrank = 100;

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
					// Already fighting (retail guards this rung with is_npc_state NPC_STATE_ATTACK): it
					// turns, and carries a hundred points with it so the switch survives the next hit.
					Branch(2, "", [When.MessageParamIsEnemy, When.Message(CallForHelp), When.Fighting],
						Do.HateMessageTarget(EnoughToOutrank)),

					// Idle: one point of hate, then go for whoever it now hates most. That is retail's pair
					// -- add_hate_point followed by attack_most_hating, in all 85 -- and the difference
					// from the rung above is real: the guard joins on the hate list rather than being
					// dragged round to face the named player, so anyone it already owed keeps its place.
					Branch(1, "", [When.MessageParamIsEnemy, When.Message(CallForHelp)],
						Do.HateMessageParam(JustEnoughToJoin),
						Do.AttackMostHating())),
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
