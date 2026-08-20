using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <c>increase_intvar</c>: retail's counter condition, which increments as it tests.
/// </summary>
/// <remarks>
/// 1,409 uses in the dump and <b>every one is a condition</b>, not an action — the element bumps one of
/// four counters and asks where it landed. It is what blocks the patterns holding the conditional spawn
/// engine's writers.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CountingConditionTests
{
	private const int AnyMap = 300520000;

	private const int AnyPatternNpc = 282240;

	private static PatternAi Ai(BossAiHarness harness) =>
		(PatternAi)harness.Spawn(AnyPatternNpc, 300f, 300f, 200f).GetAi();

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			.WithAi(typeof(IdleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary><b>Evaluating it increments.</b> The side effect is the point, as with the flag idiom.</summary>
	[Fact]
	public void EvaluatingItIncrements()
	{
		using BossAiHarness harness = NewHarness();
		PatternAi ai = Ai(harness);

		Assert.Equal(0, ai.IntVar(0));
		ai.IncreaseIntVar(0, 0, 99, onlyAtBound: false);
		ai.IncreaseIntVar(0, 0, 99, onlyAtBound: false);

		Assert.Equal(2, ai.IntVar(0));
	}

	/// <summary>
	/// <b>Consecutive ranges fire on successive passes.</b> Retail writes a sequence as 0..1, then 1..2,
	/// then 2..3, which only works if the bound flag means "on the pass that reaches the top".
	/// </summary>
	[Fact]
	public void ConsecutiveRangesFireInTurn()
	{
		using BossAiHarness harness = NewHarness();
		PatternAi ai = Ai(harness);

		// First pass: the 0..1 rung takes it.
		Assert.True(ai.IncreaseIntVar(0, 0, 1, onlyAtBound: true));

		// Second pass: 0..1 no longer matches, 1..2 does. Both are evaluated, so both increment --
		// which is why retail's rungs are written as a ladder and not as equality tests.
		Assert.False(ai.IncreaseIntVar(0, 0, 1, onlyAtBound: true));
		Assert.True(ai.IncreaseIntVar(0, 1, 3, onlyAtBound: true));
	}

	/// <summary><b>Without the flag it is true anywhere inside the range.</b> 264 of the 1,409.</summary>
	[Fact]
	public void WithoutTheFlagTheWholeRangeCounts()
	{
		using BossAiHarness harness = NewHarness();
		PatternAi ai = Ai(harness);

		Assert.True(ai.IncreaseIntVar(1, 1, 3, onlyAtBound: false));
		Assert.True(ai.IncreaseIntVar(1, 1, 3, onlyAtBound: false));
		Assert.True(ai.IncreaseIntVar(1, 1, 3, onlyAtBound: false));
		Assert.False(ai.IncreaseIntVar(1, 1, 3, onlyAtBound: false));
	}

	/// <summary><b>The four counters are separate.</b> Retail names FIRST through FOURTH.</summary>
	[Fact]
	public void TheFourCountersAreSeparate()
	{
		using BossAiHarness harness = NewHarness();
		PatternAi ai = Ai(harness);

		ai.IncreaseIntVar(2, 0, 99, onlyAtBound: false);
		ai.IncreaseIntVar(2, 0, 99, onlyAtBound: false);

		Assert.Equal(2, ai.IntVar(2));
		Assert.Equal(0, ai.IntVar(3));
	}
}
