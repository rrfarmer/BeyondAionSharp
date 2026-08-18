using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The stoneskin stoffu (210617), which sheds a piece of itself twice as it is worn down. Retail
/// pattern <c>D2_SouST_Su</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It splits, and three seconds later it points the
/// piece at whoever it is fighting.</b> Once between sixty-five and thirty-five percent and once below
/// thirty-five, it drops an angolem fragment at its feet and arms a timer; when the timer runs out it
/// calls at forty metres naming its current target, and the fragment goes.
/// <para>
/// <b>The delay is the mechanic.</b> A fragment that arrived already fighting would be an add; three
/// seconds of it standing inert is a window to kill it in, and the call is what closes the window.
/// </para>
/// <para>
/// <b>The two provocations share one flag apiece.</b> Retail writes the same band twice — once on
/// <c>on_attacked</c> and once on <c>on_spelled</c> — with the same <c>FLAGVARI_ALPHA</c> across both,
/// so a band pays out once whether the stoffu was hit or cast at. The caster half is guarded on
/// <c>is_enemy</c> and the melee half is not, which is retail's asymmetry and is kept.
/// </para>
/// <para>
/// <b>A retail quirk kept as written:</b> the melee branch for the upper band arms
/// <c>BTIMERI_INDEX_1</c> while every other branch arms <c>INDEX_0</c>, and only <c>INDEX_0</c> has a
/// handler. So a stoffu first provoked into the upper band <em>by a melee blow</em> drops its fragment
/// and never calls it — the piece stands there until the fight ends. Translated as written; a tidied
/// version would quietly make the upper band work.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. This pattern is complete.
/// </para>
/// </remarks>
[AIName("stoneskin_stoffu")]
public class StoneskinStoffuAI : PatternAi
{
	/// <summary>Retail's <c>BDF1_souledstoneMINI_Su</c> — the angolem fragment.</summary>
	private const int Fragment = 280100;

	/// <summary>Retail's <c>2006</c>: that one, go.</summary>
	public const int PointIt = 2006;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, <c>spawn_range</c> and <c>live_time</c>.</summary>
	private const int Pieces = 1;
	private const float AtItsFeet = 5f;
	private const int SixMinutes = 360;

	/// <summary>Retail's two timer slots. Only <c>INDEX_0</c> has a handler — see the remarks.</summary>
	private const int Point = 0;
	private const int Orphaned = 1;
	private const int AfterThreeSeconds = 3000;

	/// <summary>Retail's <c>range_as_meter</c> on the call.</summary>
	private const float CallReach = 40f;

	/// <summary>Retail's two flag vars, one per band, shared across both provocations.</summary>
	private const int UpperBand = 1;
	private const int LowerBand = 2;

	private static PatternAction[] Shed(int timer) =>
	[
		Do.ArmTimer(timer, AfterThreeSeconds),
		Do.SpawnNear(Fragment, Pieces, count: 1, range: AtItsFeet, liveSeconds: SixMinutes),
	];

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(
			Branch(4, "hit, in the upper band", [When.HpBetween(35, 65), When.FirstTime(UpperBand)],
				Shed(Orphaned)),
			Branch(2, "hit, in the lower band", [When.HpBelow(35), When.FirstTime(LowerBand)],
				Shed(Point))),

		OnSpelled = Of(
			Branch(3, "cast at, in the upper band",
				[When.HpBetween(35, 65), When.CasterIsEnemy, When.FirstTime(UpperBand)],
				Shed(Point)),
			Branch(1, "cast at, in the lower band",
				[When.HpBelow(35), When.CasterIsEnemy, When.FirstTime(LowerBand)],
				Shed(Point))),

		OnBattleTimer = Of(Branch(6, "and point it", [When.Timer(Point)],
			Do.Broadcast(PointIt, CallReach, aboutTarget: true))),

		OnDie = Of(Branch(5, "and take the pieces", [], Do.Despawn(Pieces))),
		OnLeaveAttack = Of(Branch(7, "and take the pieces", [], Do.Despawn(Pieces))),
	};

	public StoneskinStoffuAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The angolem fragments a stoneskin stoffu sheds (280100). Retail pattern <c>D2_FnG_D1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its whole pattern is the answer to its parent's
/// call: <b>a hundred hate on the player it names, and go.</b> A hundred is the klaw nest's claim
/// rather than the vasharti watch's glance — a fragment commits to the target it is given.
/// </remarks>
[AIName("angolem_fragment")]
public class AngolemFragmentAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> and <c>points_to_add</c>, both a hundred.</summary>
	private const int Claim = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "that one, go",
			[When.Message(StoneskinStoffuAI.PointIt)],
			Do.HateMessageTarget(Claim))),
	};

	public AngolemFragmentAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
