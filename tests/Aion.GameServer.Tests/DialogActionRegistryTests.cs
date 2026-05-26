using Aion.GameServer.Services;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Tests;

public sealed class DialogActionRegistryTests
{
	[Theory]
	[InlineData(-1, "USE_OBJECT")]
	[InlineData(2, "BUY")]
	[InlineData(24, "RESURRECT_PET")]
	[InlineData(33, "OPEN_VENDOR")]
	[InlineData(54, "OPEN_PERSONAL_WAREHOUSE")]
	[InlineData(78, "TRADE_IN")]
	[InlineData(101, "AP_SELL")]
	[InlineData(107, "TRADE_IN_UPGRADE")]
	[InlineData(125, "OPEN_STIGMA_ENCHANT")]
	[InlineData(10255, "SET_SUCCEED")]
	[InlineData(20004, "CHECK_AP")]
	[InlineData(100000, "OPEN_WEB")]
	public void NameOf_ReturnsExactNamesForPortedFixedJavaConstants(int dialogActionId, string expectedName)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.True(result.IsKnown);
		Assert.True(result.NameIsExact);
		Assert.Equal(expectedName, result.Name);
		Assert.False(result.IsLive);
	}

	[Theory]
	[InlineData(9, "SELECTED_QUEST_REWARD2")]
	[InlineData(22, "SELECTED_QUEST_REWARD15")]
	[InlineData(110, "SELECTED_QUEST_AUTO_REWARD1")]
	[InlineData(124, "SELECTED_QUEST_AUTO_REWARD15")]
	[InlineData(10000, "SETPRO1")]
	[InlineData(10254, "SETPRO255")]
	public void NameOf_DerivesExactNamesForLinearJavaConstantFamilies(int dialogActionId, string expectedName)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.True(result.IsKnown);
		Assert.True(result.NameIsExact);
		Assert.Equal(expectedName, result.Name);
	}

	[Theory]
	[InlineData(1011, "SELECT1")]
	[InlineData(1015, "SELECT1_1_1_1_1")]
	[InlineData(1351, "SELECT1_4_4_4_4")]
	[InlineData(4421, "SELECT0")]
	[InlineData(4762, "SELECT_NONE")]
	[InlineData(5103, "SELECT1_1_5")]
	[InlineData(5106, "SELECT1_4_5")]
	[InlineData(6500, "SELECT11")]
	[InlineData(8204, "SELECT15_4_4_4_4")]
	public void NameOf_ReturnsExactNamesForGeneratedSelectConstants(int dialogActionId, string expectedName)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.True(result.IsKnown);
		Assert.True(result.NameIsExact);
		Assert.Equal(expectedName, result.Name);
	}

	[Fact]
	public void NameOf_MatchesEveryJavaPublicConstant()
	{
		foreach (var (name, id) in ReadJavaDialogActionConstants())
		{
			var result = DialogActionRegistry.NameOf(id);

			Assert.True(result.IsKnown, $"{name} ({id}) should be known");
			Assert.True(result.NameIsExact, $"{name} ({id}) should be exact");
			Assert.Equal(name, result.Name);
		}
	}

	[Fact]
	public void JavaDialogActionPublicConstants_HaveUniqueIdsForNameOfMap()
	{
		var duplicateIds = ReadJavaDialogActionConstants()
			.GroupBy(pair => pair.Id)
			.Where(group => group.Count() > 1)
			.Select(group => $"{group.Key}: {string.Join(", ", group.Select(pair => pair.Name))}")
			.ToArray();

		Assert.Empty(duplicateIds);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(102)]
	[InlineData(1010)]
	[InlineData(5107)]
	[InlineData(6499)]
	[InlineData(8205)]
	[InlineData(99999)]
	public void NameOf_ReturnsUnknownForJavaGaps(int dialogActionId)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.False(result.IsKnown);
		Assert.False(result.NameIsExact);
		Assert.Null(result.Name);
	}

	private static IEnumerable<(string Name, int Id)> ReadJavaDialogActionConstants()
	{
		var sourceFile = FindRepositoryRoot()
			.Select(root => Path.Combine(root, "game-server", "src", "com", "aionemu", "gameserver", "model", "DialogAction.java"))
			.FirstOrDefault(File.Exists);
		Assert.False(string.IsNullOrEmpty(sourceFile), "Java DialogAction.java must be available for generated-name parity coverage.");

		var pattern = new Regex(@"public static final int\s+(?<name>[A-Z0-9_]+)\s*=\s*(?<id>-?\d+);", RegexOptions.Compiled);
		foreach (Match match in pattern.Matches(File.ReadAllText(sourceFile)))
			yield return (match.Groups["name"].Value, int.Parse(match.Groups["id"].Value));
	}

	private static IEnumerable<string> FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			yield return directory.FullName;
			directory = directory.Parent;
		}
	}
}
