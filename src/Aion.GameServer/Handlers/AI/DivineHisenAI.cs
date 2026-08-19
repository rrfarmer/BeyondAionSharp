using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Divine hisen (216968), Kromede's Trial. Retail pattern <c>Cromede_Hierarch</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He ran plain <c>aggressive</c>, so the two stones his
/// fight is built around never appeared.
/// <para>
/// <b>The data for them was already right and had never run.</b> The <c>spawn_helpers.xml</c> block for
/// this npc names 282103 and 282104 at retail's own absolute coordinates, to two decimal places —
/// somebody read the pattern and wrote it down correctly. Nothing read the file, because his <c>ai</c>
/// was <c>aggressive</c>, and no test could have noticed since a summoner that summons nothing looks
/// exactly like a boss with no summons.
/// </para>
/// <para>
/// <b>What he does.</b> Entering combat places a red stone and a blue stone at two fixed points in the
/// room, filed under separate spawn ids, and <b>both are cleared when he dies and when he resets</b>.
/// The stones are the encounter: his <c>on_message</c> pair answers 6401 and 6402 with a cast apiece,
/// once each, which is how the room tells him a stone has been dealt with.
/// </para>
/// <para>
/// <b>Not translated:</b> every action on both message rungs and on all seven battle-timer rungs is a
/// <c>use_skill</c> against an index this port cannot resolve, so the two heartbeats (timer 0 at five
/// seconds, timer 1 at twenty) would arm and do nothing. They are left unarmed rather than ticking on
/// empty branches — the same call as <see cref="TiamatDragonHardAI"/>'s idle chain before its rush was
/// recovered. Also untranslated: five shouts, and the one-shot flags at 75 and 35 that guard casts only.
/// </para>
/// </remarks>
[AIName("divine_hisen")]
public class DivineHisenAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c> and <c>SPAWN_ID_2</c>: one stone each, cleared together.</summary>
	private const int RedGroup = 1;
	private const int BlueGroup = 2;

	private const int RedStone = 282103;
	private const int BlueStone = 282104;

	/// <summary>Retail's absolute marks, to the decimal place it gives them.</summary>
	internal static readonly SpawnSpot RedMark = new SpawnSpot(358.44f, 172.71f, 147.38f);
	internal static readonly SpawnSpot BlueMark = new SpawnSpot(354.7f, 176.5f, 147.38f);

	/// <summary>Retail's <c>on_message</c> pair, sent when a stone is dealt with.</summary>
	public const int RedStoneDone = 6401;
	public const int BlueStoneDone = 6402;

	private static readonly PatternAction[] ClearBothStones =
	[
		Do.Despawn(RedGroup),
		Do.Despawn(BlueGroup),
	];

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(7, "a stone at each mark", When.Always,
				Do.SpawnAt(RedStone, RedGroup, 0, RedMark),
				Do.SpawnAt(BlueStone, BlueGroup, 0, BlueMark))),

		OnLeaveAttack = Of(
			Branch(7, "", When.Always, ClearBothStones)),

		OnDie = Of(
			Branch(7, "", When.Always, ClearBothStones)),
	};

	public DivineHisenAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
