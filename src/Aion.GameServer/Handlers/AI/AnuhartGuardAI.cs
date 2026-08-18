using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Anuhart legionaries standing around Dark Poeta's marabata chamber — eight npcs across eight
/// identical retail patterns, the <c>Lizardman_*_IDLF1</c> family.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Their <b>entire</b> pattern is one answer, to the
/// call a marabata booster raises when a player attacks it
/// (<see cref="MarabataControllerAI.BoosterUnderAttack"/>), and the answer is split on what the guard
/// was doing:
/// <list type="table">
/// <item><term>standing idle</term><description><b>three hundred</b> hate on whoever struck the
/// booster, and go</description></item>
/// <item><term>already in a fight</term><description><b>five hundred</b>, and switch to them</description></item>
/// </list>
/// <para>
/// <b>The larger number is the one for a guard that is already busy</b>, which is the whole point of
/// the split: three hundred on an empty aggro list makes the caller's attacker the most hated by
/// default, while a guard mid-fight has to be outbid. Retail writes the first as
/// <c>add_hate_point</c> + <c>attack_most_hating</c> and the second as <c>switch_target</c> with
/// <c>points_to_add</c>; <see cref="Do.HateMessageTarget"/> is both, because for an idle guard
/// "most hating" and "the one just named" are the same creature.
/// </para>
/// <para>
/// <b>Sixteen of their sixty-four spawn spots stand inside the fifty-metre reach</b>, so this is not
/// decoration: pulling a booster in the marabata chamber can drag most of a room. Two of the eight
/// (214848 anuhart spotter, 215230 anuhart breeder) have no spot in reach at all and retail gives them
/// the pattern anyway — recorded, and left as retail has it.
/// </para>
/// </remarks>
[AIName("anuhart_guard")]
public class AnuhartGuardAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> for a guard that was standing idle.</summary>
	private const int Rouse = 300;

	/// <summary>Retail's <c>points_to_add</c> for a guard that was already fighting.</summary>
	private const int Outbid = 500;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(9, "standing about when the booster is struck",
				[When.Message(MarabataControllerAI.BoosterUnderAttack), When.Idle],
				Do.HateMessageTarget(Rouse)),

			Branch(8, "already fighting when the booster is struck",
				[When.Message(MarabataControllerAI.BoosterUnderAttack), When.Fighting],
				Do.HateMessageTarget(Outbid))),
	};

	public AnuhartGuardAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
