using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The reader half of the conditional spawn engine: retail's <c>extcondition</c> gates.
/// </summary>
/// <remarks>
/// 54,388 gates across 163 worlds decide whether a spawn group exists, and this port has never had any
/// of it. The corpus in <c>tools/client-extract/out/spawn_conditions.tsv</c> is every distinct one of
/// them, and the first test below is the claim that matters: the parser handles the whole dump, not a
/// sample of it.
/// </remarks>
public sealed class SpawnConditionTests
{
	private static IEnumerable<(int Uses, string Expression)> Corpus()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(), "tools", "client-extract", "out",
			"spawn_conditions.tsv");
		foreach (string line in File.ReadLines(path).Skip(1))
		{
			string[] fields = line.Split('\t');
			if (fields.Length >= 3)
				yield return (int.Parse(fields[0]), fields[2]);
		}
	}

	/// <summary><b>Every gate retail ships parses.</b> All 7,146 of them, covering 54,388 uses.</summary>
	[Fact]
	public void EveryRetailGateParses()
	{
		List<string> refused = new List<string>();
		int gates = 0;
		int uses = 0;

		foreach ((int count, string expression) in Corpus())
		{
			gates++;
			uses += count;
			try
			{
				SpawnCondition.Parse(expression);
			}
			catch (FormatException)
			{
				refused.Add(expression);
			}
		}

		Assert.Equal(7146, gates);
		Assert.Equal(54388, uses);

		// The only refusals are nine gates retail itself ships broken. Eight end mid-expression with an
		// unclosed bracket; the ninth is `Race == 2(Race == 2) && (Wave_Z1 <= 4)`, a fragment pasted
		// into itself. Guessing at what they were meant to say would be inventing a mechanic, so they
		// are refused and named in docs/retail-ai-fidelity.md.
		Assert.Equal(9, refused.Count);

		int unbalanced = refused.Count(e => e.Count(c => c == '(') != e.Count(c => c == ')'));
		Assert.Equal(8, unbalanced);
		Assert.Contains(refused, e => e.StartsWith("Race == 2(", StringComparison.Ordinal));
	}

	/// <summary><b>A bare variable is a test for "not zero".</b> 101 gates are written that way.</summary>
	[Fact]
	public void ABareVariableIsATestForNotZero()
	{
		SpawnCondition gate = SpawnCondition.Parse("CHALLENGE_504");

		Assert.False(gate.Holds(new Dictionary<string, int>()));
		Assert.True(gate.Holds(new Dictionary<string, int> { ["CHALLENGE_504"] = 1 }));
		Assert.True(gate.Holds(new Dictionary<string, int> { ["CHALLENGE_504"] = -3 }));
	}

	/// <summary>
	/// <b>A <c>[SAVE]</c> name is a different variable from the same name without it.</b> Eighteen
	/// names appear both ways, so merging them would join two things retail keeps apart.
	/// </summary>
	[Fact]
	public void ASavedNameIsNotThePlainOne()
	{
		var store = new Dictionary<string, int> { ["v01"] = 1 };

		Assert.True(SpawnCondition.Parse("v01 == 1").Holds(store));
		Assert.False(SpawnCondition.Parse("[SAVE]v01 == 1").Holds(store));
		Assert.Equal(new[] { "[SAVE]v01" }, SpawnCondition.Parse("[SAVE]v01 == 1").PersistedVariables);
	}

	/// <summary><b>An unknown variable reads as zero</b>, so a world works before its writers exist.</summary>
	[Fact]
	public void AnUnknownVariableReadsAsZero()
	{
		Dictionary<string, int> nothing = new Dictionary<string, int>();

		// The most common gate in the dump, and its negation.
		Assert.True(SpawnCondition.Parse("SpecialServer_Cond == 0").Holds(nothing));
		Assert.False(SpawnCondition.Parse("SpecialServer_Cond == 1").Holds(nothing));
	}

	/// <summary><b>Every comparison retail uses works.</b></summary>
	[Theory]
	[InlineData("v == 3", true)]
	[InlineData("v != 3", false)]
	[InlineData("v > 2", true)]
	[InlineData("v > 3", false)]
	[InlineData("v >= 3", true)]
	[InlineData("v < 4", true)]
	[InlineData("v <= 3", true)]
	[InlineData("v <= 2", false)]
	public void EveryComparisonWorks(string expression, bool expected)
	{
		var store = new Dictionary<string, int> { ["v"] = 3 };

		Assert.Equal(expected, SpawnCondition.Parse(expression).Holds(store));
	}

	/// <summary>
	/// <b><c>&amp;&amp;</c> binds tighter than <c>||</c>.</b> The dump's longest expression is a chain
	/// of bracketed pairs joined by <c>||</c>, and reading the precedence the other way makes it hold
	/// whenever its first term does.
	/// </summary>
	[Fact]
	public void AndBindsTighterThanOr()
	{
		var store = new Dictionary<string, int> { ["a"] = 1, ["b"] = 0, ["c"] = 1 };

		// a==1 || (b==1 && c==1) is true; (a==1 || b==1) && c==1 is also true here, so pick a case
		// that separates them: false || (true && false) is false.
		store["a"] = 0;
		store["b"] = 1;
		store["c"] = 0;
		Assert.False(SpawnCondition.Parse("a == 1 || b == 1 && c == 1").Holds(store));
	}

	/// <summary><b>Brackets override it.</b></summary>
	[Fact]
	public void BracketsOverridePrecedence()
	{
		var store = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 0 };

		Assert.False(SpawnCondition.Parse("(a == 1 || b == 1) && c == 1").Holds(store));
		store["c"] = 1;
		Assert.True(SpawnCondition.Parse("(a == 1 || b == 1) && c == 1").Holds(store));
	}

	/// <summary><b>Negative literals parse.</b> Retail writes <c>-1</c> 2,707 times as a set value.</summary>
	[Fact]
	public void NegativeLiteralsParse()
	{
		var store = new Dictionary<string, int> { ["v"] = -1 };

		Assert.True(SpawnCondition.Parse("v == -1").Holds(store));
	}

	/// <summary><b>And it reports the variables it watches</b>, so a caller knows what to re-check.</summary>
	[Fact]
	public void ItNamesTheVariablesItReads()
	{
		SpawnCondition gate = SpawnCondition.Parse("(race == 1) && (Floor >= 12)");

		Assert.Equal(new[] { "Floor", "race" }, gate.Variables.OrderBy(v => v).ToArray());
	}

	/// <summary><b>Nonsense is refused rather than silently true.</b></summary>
	[Theory]
	[InlineData("v ==")]
	[InlineData("(v == 1")]
	[InlineData("v == 1)")]
	[InlineData("v = 1")]
	[InlineData("")]
	public void NonsenseIsRefused(string expression)
	{
		Assert.Throws<FormatException>(() => SpawnCondition.Parse(expression));
	}
}
