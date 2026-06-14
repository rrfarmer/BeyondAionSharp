using System.Text.Json;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// Phase A2 (formula half) of the Port Fidelity &amp; Remediation Plan.
///
/// Reads the SHARED formula fixtures produced by the Java harness
/// (game-server GoldenFormulaFixtureGeneratorTest -> parity-artifacts/golden/formulas/*.json)
/// and asserts the C# formula methods return values identical to real Java. The Java result
/// is the single source of truth, so exact Java semantics (e.g. int/double truncation,
/// Math.round, enum branch selection) are pinned.
///
/// Only pure primitive/enum-arg methods are covered here (the body reads ONLY its args).
/// Enum args are passed by NAME; enum-typed results are compared by NAME.
///
/// To add a formula: capture it in the Java generator, then add a dispatch case in Evaluate
/// (numeric result) or EvaluateName (enum result), and an [InlineData] entry below.
/// </summary>
public sealed class GoldenFormulaFixtureTests
{
	[Theory]
	[InlineData("StatFunctions.adjustPvpDpGained.json")]
	[InlineData("StatFunctions.calculateRatingMultiplier.json")]
	[InlineData("StatFunctions.getApNpcRating.json")]
	[InlineData("XPRewardEnum.xpRewardFrom.json")]
	[InlineData("DropRewardEnum.dropRewardFrom.json")]
	[InlineData("XPLossEnum.getExpLoss.json")]
	[InlineData("AbyssRankEnum.getRankForPoints.json")]
	[InlineData("AbyssRankEnum.getRankById.json")]
	[InlineData("StatFunctions.limit.json")]
	public void CsharpFormulaMatchesJavaGoldenFixture(string fixtureFile)
	{
		using var fixture = LoadFixture(fixtureFile);
		var formula = fixture.RootElement.GetProperty("formula").GetString()!;

		foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var inputs = caseElement.GetProperty("inputs");
			var result = caseElement.GetProperty("result");

			if (result.ValueKind == JsonValueKind.Object)
			{
				// float-typed result: compared BIT-EXACT via raw IEEE-754 bits (no decimal rounding).
				var expectedBits = result.GetProperty("floatBits").GetInt32();
				var actualBits = BitConverter.SingleToInt32Bits(EvaluateFloat(formula, inputs));
				Assert.True(expectedBits == actualBits,
					$"{formula}({DescribeInputs(inputs)}): C# diverged from Java golden (float bits). " +
					$"Java={expectedBits} ({BitConverter.Int32BitsToSingle(expectedBits)}), " +
					$"C#={actualBits} ({BitConverter.Int32BitsToSingle(actualBits)})");
			}
			else if (result.ValueKind == JsonValueKind.String)
			{
				// enum-typed result: compare by NAME (avoids ordinal-vs-value traps).
				var expected = result.GetString()!;
				var actual = EvaluateName(formula, inputs);
				Assert.True(expected == actual,
					$"{formula}({DescribeInputs(inputs)}): C# diverged from Java golden. " +
					$"Java={expected}, C#={actual}");
			}
			else
			{
				var expected = result.GetInt64();
				var actual = Evaluate(formula, inputs);
				Assert.True(expected == actual,
					$"{formula}({DescribeInputs(inputs)}): C# diverged from Java golden. " +
					$"Java={expected}, C#={actual}");
			}
		}
	}

	private static long Evaluate(string formula, JsonElement inputs) => formula switch
	{
		// Faithful target: Utils/Stats/StatFunctions (NOT the slop PvpDpRewardService).
		"StatFunctions.adjustPvpDpGained" => StatFunctions.AdjustPvpDpGained(
			inputs.GetProperty("points").GetInt32(),
			inputs.GetProperty("defeatedLvl").GetInt32(),
			inputs.GetProperty("killerLvl").GetInt32()),
		"StatFunctions.calculateRatingMultiplier" => StatFunctions.CalculateRatingMultiplier(
			ParseNpcRating(inputs.GetProperty("npcRating").GetString()!)),
		"StatFunctions.getApNpcRating" => StatFunctions.GetApNpcRating(
			ParseNpcRating(inputs.GetProperty("npcRating").GetString()!)),
		"XPRewardEnum.xpRewardFrom" => XPRewardEnumExtensions.XpRewardFrom(
			inputs.GetProperty("levelDifference").GetInt32()),
		"DropRewardEnum.dropRewardFrom" => DropRewardEnumExtensions.DropRewardFrom(
			inputs.GetProperty("levelDifference").GetInt32()),
		"XPLossEnum.getExpLoss" => XPLossEnumExtensions.GetExpLoss(
			inputs.GetProperty("level").GetInt32(),
			inputs.GetProperty("expNeed").GetInt64()),
		_ => throw new NotSupportedException($"No C# numeric dispatch registered for formula {formula}"),
	};

	private static float EvaluateFloat(string formula, JsonElement inputs) => formula switch
	{
		// Faithful target: Utils/Stats/StatFunctions.Limit -> Math.Min(StatCapUtil.GetDifferenceLimit, value).
		// The float input is carried as raw IEEE-754 bits to stay bit-exact across languages.
		"StatFunctions.limit" => StatFunctions.Limit(
			Enum.Parse<StatEnum>(inputs.GetProperty("statEnum").GetString()!),
			BitConverter.Int32BitsToSingle(inputs.GetProperty("value").GetInt32())),
		_ => throw new NotSupportedException($"No C# float dispatch registered for formula {formula}"),
	};

	private static string EvaluateName(string formula, JsonElement inputs) => formula switch
	{
		"AbyssRankEnum.getRankForPoints" => AbyssRankEnumExtensions.GetRankForPoints(
			inputs.GetProperty("ap").GetInt32(),
			inputs.GetProperty("gp").GetInt32()).ToString(),
		"AbyssRankEnum.getRankById" => AbyssRankEnumExtensions.GetRankById(
			inputs.GetProperty("id").GetInt32()).ToString(),
		_ => throw new NotSupportedException($"No C# enum dispatch registered for formula {formula}"),
	};

	private static NpcRating ParseNpcRating(string name) => Enum.Parse<NpcRating>(name);

	private static string DescribeInputs(JsonElement inputs) =>
		string.Join(", ", inputs.EnumerateObject().Select(p => $"{p.Name}={p.Value.GetRawText()}"));

	private static JsonDocument LoadFixture(string fileName)
	{
		var path = Path.Combine(FixtureRoot(), fileName);
		Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}. " +
			"Regenerate with: mvn -pl game-server -am test -Dtest=GoldenFormulaFixtureGeneratorTest " +
			"-Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false");
		return JsonDocument.Parse(File.ReadAllText(path));
	}

	private static string FixtureRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var candidate = Path.Combine(dir.FullName, "parity-artifacts", "golden", "formulas");
			if (Directory.Exists(candidate))
				return candidate;
			dir = dir.Parent;
		}
		throw new DirectoryNotFoundException(
			"Could not locate parity-artifacts/golden/formulas above " + AppContext.BaseDirectory);
	}
}
