using System.Text.Json;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// Phase A2 (formula half) of the Port Fidelity &amp; Remediation Plan.
///
/// Reads the SHARED formula fixtures produced by the Java harness
/// (game-server GoldenFormulaFixtureGeneratorTest -> parity-artifacts/golden/formulas/*.json)
/// and asserts the C# formula methods return values identical to real Java. The Java result
/// is the single source of truth, so exact Java semantics (e.g. int/double truncation) are pinned.
///
/// To add a formula: capture it in the Java generator, then add a dispatch case in Evaluate.
/// </summary>
public sealed class GoldenFormulaFixtureTests
{
	[Theory]
	[InlineData("StatFunctions.adjustPvpDpGained.json")]
	public void CsharpFormulaMatchesJavaGoldenFixture(string fixtureFile)
	{
		using var fixture = LoadFixture(fixtureFile);
		var formula = fixture.RootElement.GetProperty("formula").GetString()!;

		foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var inputs = caseElement.GetProperty("inputs");
			var expected = caseElement.GetProperty("result").GetInt64();

			var actual = Evaluate(formula, inputs);

			Assert.True(expected == actual,
				$"{formula}({DescribeInputs(inputs)}): C# diverged from Java golden. " +
				$"Java={expected}, C#={actual}");
		}
	}

	private static long Evaluate(string formula, JsonElement inputs) => formula switch
	{
		"StatFunctions.adjustPvpDpGained" => PvpDpRewardService.AdjustPvpDpGained(
			inputs.GetProperty("points").GetInt32(),
			inputs.GetProperty("defeatedLvl").GetInt32(),
			inputs.GetProperty("killerLvl").GetInt32()),
		_ => throw new NotSupportedException($"No C# dispatch registered for formula {formula}"),
	};

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
