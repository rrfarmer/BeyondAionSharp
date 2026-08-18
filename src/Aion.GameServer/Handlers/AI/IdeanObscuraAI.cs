using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Vengeful Modor's idean obscura (284379) and the two weakened kinds (284661, 856495). Retail
/// pattern <c>Rune_FrostNmd_MezSum_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All three were ELITEs on plain <c>aggressive</c>,
/// standing beside a boss they had no way of hearing.
/// <list type="table">
/// <item><term>on Modor's call</term><description>take whoever she is fighting</description></item>
/// <item><term>below half, once</term><description>two blows in five turn it onto a <b>random</b>
/// attacker instead</description></item>
/// </list>
/// <para>
/// <b>The call had no sender until now.</b> Retail's <c>444</c> comes from
/// <c>Rune_FrostNmd_N_65_Ah</c>, which binds to Vengeful Modor — and Modor runs a Java-parity class
/// rather than a pattern, so the message was written on both ends of a wire nobody was holding.
/// <see cref="Ai.CombatAlarm"/> on <see cref="CursedQueenModorAI"/> holds it now, the second time that
/// helper has closed a gap of exactly this shape after the Sauro Supply Base alarm.
/// </para>
/// <para>
/// <b>And it is a gap the message audit cannot see.</b> <c>audit_message_reach.py</c> counts a message
/// as sent when a live pattern contains the broadcast; it has no way to know that the npc bound to
/// that pattern is running a different class, so a listener whose only sender is a ported-elsewhere
/// boss reads as connected. Recorded rather than fixed, because the audit would have to know what
/// every C# class implements to know better.
/// </para>
/// <para>
/// <b>Not translated.</b> Eleven skill indices; the <c>goto_waypoint</c> they walk on waking; retail's
/// <c>on_spelled</c> copy of the below-half branch, which shares its flag with <c>on_attacked</c> and
/// so is the same one payment; the marker each of them drops at Modor's own spot when killed — an
/// invisible NPC (284528) that our data already spawns as Witch Queen Modor and whose sanctuary-release
/// meaning belongs to the instance rather than to this pattern; and message <c>104</c>, a fifteen-minute
/// timer whose only action here is an idle timer.
/// </para>
/// </remarks>
[AIName("idean_obscura")]
public class IdeanObscuraAI : PatternAi
{
	/// <summary>Retail's <c>444</c>: Modor naming the player she is fighting.</summary>
	public const int CallToArms = 444;

	/// <summary>Retail's <c>FLAGVARI_GAMMA_4</c>: the turn below half comes once.</summary>
	private const int Turned = 4;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(7, "", [When.Message(CallToArms)],
				Do.HateMessageParam(SummonOrder.OnePoint))),

		OnAttacked = Of(
			Branch(55, "two in five turn", [When.Chance(40), When.HpBelow(50), When.FirstTime(Turned)],
				Do.SwitchTarget(AggroTarget.RANDOM))),
	};

	public IdeanObscuraAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
