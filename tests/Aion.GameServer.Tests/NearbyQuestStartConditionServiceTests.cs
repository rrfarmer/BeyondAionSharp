using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestStartConditionServiceTests
{
	[Fact]
	public void CheckNearbyStartConditions_AppliesJavaBasicQuestTemplateGates()
	{
		var player = new Player
		{
			Level = 20,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			AbyssRank = PlayerAbyssRank.Default() with { Rank = 3 },
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(1001, MinLevelPermitted: 22, RacePermitted: "PC_ALL"),
			new NearbyQuestTemplateSummary(1002, MinLevelPermitted: 23),
			new NearbyQuestTemplateSummary(1003, MaxLevelPermitted: 19),
			new NearbyQuestTemplateSummary(1004, RacePermitted: "ASMODIANS"),
			new NearbyQuestTemplateSummary(1005, ClassPermitted: new HashSet<string>(["CLERIC"], StringComparer.Ordinal)),
			new NearbyQuestTemplateSummary(1006, GenderPermitted: "FEMALE"),
			new NearbyQuestTemplateSummary(1007, RequiredRank: 4),
		]);

		AssertPass(player, 1001, table);
		AssertFailure(player, 1002, table, NearbyQuestStartConditionFailure.MinLevel);
		AssertFailure(player, 1003, table, NearbyQuestStartConditionFailure.MaxLevel);
		AssertFailure(player, 1004, table, NearbyQuestStartConditionFailure.Race);
		AssertFailure(player, 1005, table, NearbyQuestStartConditionFailure.Class);
		AssertFailure(player, 1006, table, NearbyQuestStartConditionFailure.Gender);
		AssertFailure(player, 1007, table, NearbyQuestStartConditionFailure.Rank);
	}

	[Fact]
	public void CheckNearbyStartConditions_AppliesJavaQuestStateAndRepeatGatesConservatively()
	{
		var player = new Player
		{
			Level = 30,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			Quests =
			[
				new PlayerQuestState(2001, "START", QuestVars: 0, Flags: 0, CompleteCount: 0),
				new PlayerQuestState(2002, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
				new PlayerQuestState(2003, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
				new PlayerQuestState(2004, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
				new PlayerQuestState(2005, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(2001),
			new NearbyQuestTemplateSummary(2002),
			new NearbyQuestTemplateSummary(2003, MaxRepeatCount: 1),
			new NearbyQuestTemplateSummary(2004, MaxRepeatCount: 2),
			new NearbyQuestTemplateSummary(2005, MaxRepeatCount: 2, IsTimeBased: true),
		]);

		AssertFailure(player, 2001, table, NearbyQuestStartConditionFailure.AlreadyStarted);
		AssertFailure(player, 2002, table, NearbyQuestStartConditionFailure.AlreadyStarted);
		AssertFailure(player, 2003, table, NearbyQuestStartConditionFailure.RepeatCount);
		AssertPass(player, 2004, table);
		AssertFailure(player, 2005, table, NearbyQuestStartConditionFailure.UnsupportedRepeatTiming);
	}

	[Fact]
	public void CheckNearbyStartConditions_ReportsUnsupportedJavaDependenciesInsteadOfAssumingParity()
	{
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(3001, HasXmlStartConditions: true),
			new NearbyQuestTemplateSummary(3002, HasInventoryItems: true),
			new NearbyQuestTemplateSummary(3003, CombineSkill: 40001),
			new NearbyQuestTemplateSummary(3004, NpcFactionId: 12),
		]);

		AssertFailure(player, 3001, table, NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions);
		AssertFailure(player, 3002, table, NearbyQuestStartConditionFailure.UnsupportedInventoryItems);
		AssertFailure(player, 3003, table, NearbyQuestStartConditionFailure.UnsupportedCombineSkill);
		AssertFailure(player, 3004, table, NearbyQuestStartConditionFailure.UnsupportedNpcFaction);
	}

	[Fact]
	public void GetLevelRequirementDiff_MatchesJavaMissingTemplateAndMinLevelBehavior()
	{
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(4001, MinLevelPermitted: 22),
			new NearbyQuestTemplateSummary(4002, MinLevelPermitted: 18),
		]);

		Assert.Equal(2, NearbyQuestStartConditionService.GetLevelRequirementDiff(4001, playerLevel: 20, table));
		Assert.Equal(-2, NearbyQuestStartConditionService.GetLevelRequirementDiff(4002, playerLevel: 20, table));
		Assert.Equal(99, NearbyQuestStartConditionService.GetLevelRequirementDiff(4999, playerLevel: 20, table));
	}

	private static void AssertPass(Player player, int questId, NearbyQuestTemplateTable table)
	{
		var result = NearbyQuestStartConditionService.CheckNearbyStartConditions(player, questId, table);
		Assert.True(result.CanStart);
		Assert.Equal(NearbyQuestStartConditionFailure.None, result.Failure);
	}

	private static void AssertFailure(
		Player player,
		int questId,
		NearbyQuestTemplateTable table,
		NearbyQuestStartConditionFailure expectedFailure)
	{
		var result = NearbyQuestStartConditionService.CheckNearbyStartConditions(player, questId, table);
		Assert.False(result.CanStart);
		Assert.Equal(expectedFailure, result.Failure);
	}
}
