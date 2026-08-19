using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The arena saam (217737), which does not fight you — it splits and runs.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Retail's <c>IDArena_S2_Bonus_1</c> has
/// <b>no health bands at all</b>. Its whole mechanic is one <c>on_attacked</c> rung:
/// <code>
/// ? test_probability percent=50
/// ! increase_intvar INTVARI_FIRST
/// &gt; say_to_all ...
/// &gt; spawn S2_SAAM2_55_n num_to_spawn=1 spawn_range=10
/// &gt; flee_from from=OBJI_ATTACKER seconds=5 push_state=TRUE
/// </code>
/// <para>
/// So it is a coin flip on every hit: half the time it sheds one "cut saam" ten metres away and
/// <b>bolts for five seconds</b>. The counter it increments is the score — this is a bonus stage, and
/// the number of pieces you cut off it is the point of the round.
/// </para>
/// <para>
/// <b>What was here instead was four health bands, each placing two.</b> Nothing in retail's pattern
/// resembles that: not the trigger, not the count, not the fleeing, and not the roll. A boss that runs
/// from you while multiplying is a different round from a boss that stands still and summons at
/// thresholds, and the audit could only see the count.
/// </para>
/// <para>
/// <b>Not translated:</b> the two shouts, <c>change_world_scene_status</c> on waking (no vocabulary
/// here), and <c>set_condition_spawn_variable STAGE2_OVER</c> on death and despawn — the stage's own
/// bookkeeping, which belongs to an instance handler rather than to this npc.
/// </para>
/// </remarks>
[AIName("arena_saam")]
public class ArenaSaamAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c>: the pieces, cleared when it dies or is dismissed.</summary>
	private const int Pieces = 1;

	/// <summary>Retail <c>S2_SAAM2_55_n</c> — "cut saam".</summary>
	private const int CutSaam = 217738;

	/// <summary>Retail <c>S2_SAAM_CTRL</c>, its controller, placed on waking.</summary>
	private const int Controller = 217739;

	/// <summary>Retail's <c>INTVARI_FIRST</c>: how many pieces have been cut off it.</summary>
	private const int PiecesCut = 0;

	/// <summary>
	/// Bounds on the score counter. Retail's <c>increase_intvar</c> carries no bound at all; ours needs
	/// one, so the ceiling is set far above anything a bonus round could reach rather than at a number
	/// that would silently cap the score.
	/// </summary>
	private const int NoRealCeiling = 1000;

	private const int SplitChance = 50;
	private const float PieceRange = 10f;
	private const int FleeSeconds = 5;

	/// <summary>Retail's <c>on_message</c> 7153, which dismisses it.</summary>
	public const int Dismiss = 7153;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(
			Branch(5, "its controller", When.Always,
				Do.SpawnNear(Controller, Pieces))),

		OnAttacked = Of(
			Branch(7, "half the time it sheds a piece and runs",
				[When.Chance(SplitChance)],
				Do.Increment(PiecesCut, 0, NoRealCeiling),
				Do.SpawnNear(CutSaam, Pieces, count: 1, range: PieceRange),
				Do.Flee(FleeSeconds))),

		OnDie = Of(
			Branch(7, "the pieces go with it", When.Always,
				Do.Despawn(Pieces))),

		OnMessage = Of(
			Branch(7, "dismissed", [When.Message(Dismiss)],
				Do.Despawn(Pieces),
				Do.DespawnSelf())),
	};

	public ArenaSaamAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
