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
				new PlayerQuestState(2006, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1,
					NextRepeatTime: new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero)),
				new PlayerQuestState(2007, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1,
					NextRepeatTime: new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero)),
				new PlayerQuestState(2008, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 255,
					NextRepeatTime: new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero)),
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(2001),
			new NearbyQuestTemplateSummary(2002),
			new NearbyQuestTemplateSummary(2003, MaxRepeatCount: 1),
			new NearbyQuestTemplateSummary(2004, MaxRepeatCount: 2),
			new NearbyQuestTemplateSummary(2005, MaxRepeatCount: 2, IsTimeBased: true, RepeatCycle: ["ALL"]),
			new NearbyQuestTemplateSummary(2006, MaxRepeatCount: 2, IsTimeBased: true, RepeatCycle: ["ALL"]),
			new NearbyQuestTemplateSummary(2007, MaxRepeatCount: 2, IsTimeBased: true, RepeatCycle: ["MON", "WED"]),
			new NearbyQuestTemplateSummary(2008, MaxRepeatCount: 255, IsTimeBased: true, RepeatCycle: ["ALL"]),
		]);
		var beforeReset = new DateTimeOffset(2026, 5, 25, 8, 59, 59, TimeSpan.Zero);
		var atReset = new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero);

		AssertFailure(player, 2001, table, NearbyQuestStartConditionFailure.AlreadyStarted);
		AssertFailure(player, 2002, table, NearbyQuestStartConditionFailure.AlreadyStarted);
		AssertFailure(player, 2003, table, NearbyQuestStartConditionFailure.RepeatCount);
		AssertPass(player, 2004, table);
		AssertPass(player, 2005, table);
		AssertFailure(player, 2006, table, NearbyQuestStartConditionFailure.RepeatTiming, beforeReset);
		AssertPass(player, 2007, table, atReset);
		AssertPass(player, 2008, table, atReset);
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
			new NearbyQuestTemplateSummary(3004, NpcFactionId: 12),
		]);

		AssertFailure(player, 3001, table, NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions);
		AssertFailure(player, 3002, table, NearbyQuestStartConditionFailure.UnsupportedInventoryItems);
		AssertFailure(player, 3004, table, NearbyQuestStartConditionFailure.NpcFaction);
	}

	[Fact]
	public void CheckNearbyStartConditions_AppliesJavaNpcFactionGate()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(1_800);
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			NpcFactions = new PlayerNpcFactionsSnapshot(
			[
				new PlayerNpcFactionState(
					FactionId: 2,
					IsActive: true,
					IsMentor: false,
					TimeEpochSeconds: 1_000,
					State: PlayerNpcFactionQuestState.Complete),
				new PlayerNpcFactionState(
					FactionId: 4,
					IsActive: false,
					IsMentor: false,
					TimeEpochSeconds: 0,
					State: PlayerNpcFactionQuestState.Noting),
				new PlayerNpcFactionState(
					FactionId: 10,
					IsActive: true,
					IsMentor: true,
					TimeEpochSeconds: 2_000,
					State: PlayerNpcFactionQuestState.Complete),
			]),
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(3601, NpcFactionId: 2),
			new NearbyQuestTemplateSummary(3602, NpcFactionId: 4),
			new NearbyQuestTemplateSummary(3603, NpcFactionId: 3),
			new NearbyQuestTemplateSummary(3604, NpcFactionId: 10, IsMentorQuest: true),
			new NearbyQuestTemplateSummary(3605, NpcFactionId: 10, IsMentorQuest: true, IsTimeBased: true, RepeatCycle: ["ALL"]),
			new NearbyQuestTemplateSummary(3606, NpcFactionId: 11, IsMentorQuest: true, IsTimeBased: true, RepeatCycle: ["ALL"]),
		]);

		AssertPass(player, 3601, table, now);
		AssertFailure(player, 3602, table, NearbyQuestStartConditionFailure.NpcFaction, now);
		AssertFailure(player, 3603, table, NearbyQuestStartConditionFailure.NpcFaction, now);
		AssertFailure(player, 3604, table, NearbyQuestStartConditionFailure.NpcFaction, now);
		AssertPass(player, 3605, table, now);
		AssertFailure(player, 3606, table, NearbyQuestStartConditionFailure.NpcFaction, now);
	}

	[Fact]
	public void CheckNearbyStartConditions_AppliesJavaCombineSkillGate()
	{
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			Skills =
			[
				new PlayerSkill { SkillId = 40001, SkillLevel = 199 },
				new PlayerSkill { SkillId = 30002, SkillLevel = 399 },
				new PlayerSkill { SkillId = 40002, SkillLevel = 241 },
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(3301, CombineSkill: 40001, CombineSkillPoint: 199),
			new NearbyQuestTemplateSummary(3302, CombineSkill: 40001, CombineSkillPoint: 200),
			new NearbyQuestTemplateSummary(3303, CombineSkill: -1, CombineSkillPoint: 399),
			new NearbyQuestTemplateSummary(3304, CombineSkill: -1, CombineSkillPoint: 399, NpcFactionId: 12),
			new NearbyQuestTemplateSummary(3305, CombineSkill: 40002, CombineSkillPoint: 200, QuestCategory: "TASK"),
			new NearbyQuestTemplateSummary(3306, CombineSkill: 40002, CombineSkillPoint: 201, QuestCategory: "TASK"),
		]);

		AssertPass(player, 3301, table);
		AssertFailure(player, 3302, table, NearbyQuestStartConditionFailure.CombineSkill);
		AssertPass(player, 3303, table);
		AssertFailure(player, 3304, table, NearbyQuestStartConditionFailure.CombineSkill);
		AssertFailure(player, 3305, table, NearbyQuestStartConditionFailure.CombineSkill);
		AssertPass(player, 3306, table);
	}

	[Fact]
	public void CheckNearbyStartConditions_AppliesJavaInventoryItemPresenceGate()
	{
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			InventoryItems =
			[
				new InventoryItem { ItemId = 182200001, Count = 1 },
				new InventoryItem { ItemId = 182200002, Count = 1 },
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(3501, HasInventoryItems: true, InventoryItems:
			[
				new NearbyQuestInventoryItem(182200001, Count: 99),
				new NearbyQuestInventoryItem(182200002),
			]),
			new NearbyQuestTemplateSummary(3502, HasInventoryItems: true, InventoryItems:
			[
				new NearbyQuestInventoryItem(182200003),
			]),
		]);

		AssertPass(player, 3501, table);
		AssertFailure(player, 3502, table, NearbyQuestStartConditionFailure.InventoryItems);
	}

	[Fact]
	public void CheckNearbyStartConditions_AppliesSupportedJavaXmlStartConditions()
	{
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			TitleId = 7,
			Quests =
			[
				new PlayerQuestState(5001, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 2, RewardGroup: 1),
				new PlayerQuestState(5002, "START", QuestVars: 0, Flags: 0, CompleteCount: 0),
				new PlayerQuestState(5003, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
				new PlayerQuestState(5004, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(5001, MaxRepeatCount: 2),
			new NearbyQuestTemplateSummary(4001, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(
					Finished: [new NearbyQuestFinishedCondition(5001, Reward: 1)],
					Unfinished: new HashSet<int>([5002], EqualityComparer<int>.Default),
					NoAcquired: new HashSet<int>([5003], EqualityComparer<int>.Default),
					Acquired: new HashSet<int>([5004], EqualityComparer<int>.Default),
					Equipped: new HashSet<int>([110101001], EqualityComparer<int>.Default),
					RequiredTitle: 7),
			]),
		]);

		AssertPass(player, 4001, table);
	}

	[Fact]
	public void CheckNearbyStartConditions_AppliesJavaXmlStartConditionFailures()
	{
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			TitleId = 9,
			Quests =
			[
				new PlayerQuestState(6001, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1, RewardGroup: 0),
				new PlayerQuestState(6002, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
				new PlayerQuestState(6003, "START", QuestVars: 0, Flags: 0, CompleteCount: 0),
				new PlayerQuestState(6004, "LOCKED", QuestVars: 0, Flags: 0, CompleteCount: 0),
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(4101, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(Finished: [new NearbyQuestFinishedCondition(6999)]),
			]),
			new NearbyQuestTemplateSummary(4102, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(Finished: [new NearbyQuestFinishedCondition(6001, Reward: 1)]),
			]),
			new NearbyQuestTemplateSummary(4103, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(Unfinished: new HashSet<int>([6002], EqualityComparer<int>.Default)),
			]),
			new NearbyQuestTemplateSummary(4104, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(NoAcquired: new HashSet<int>([6003], EqualityComparer<int>.Default)),
			]),
			new NearbyQuestTemplateSummary(4105, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(Acquired: new HashSet<int>([6004], EqualityComparer<int>.Default)),
			]),
			new NearbyQuestTemplateSummary(4106, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(RequiredTitle: 7),
			]),
		]);

		AssertFailure(player, 4101, table, NearbyQuestStartConditionFailure.XmlStartConditions);
		AssertFailure(player, 4102, table, NearbyQuestStartConditionFailure.XmlStartConditions);
		AssertFailure(player, 4103, table, NearbyQuestStartConditionFailure.XmlStartConditions);
		AssertFailure(player, 4104, table, NearbyQuestStartConditionFailure.XmlStartConditions);
		AssertFailure(player, 4105, table, NearbyQuestStartConditionFailure.XmlStartConditions);
		AssertFailure(player, 4106, table, NearbyQuestStartConditionFailure.XmlStartConditions);
	}

	[Fact]
	public void CheckNearbyStartConditions_RequiresAllMandatoryAndOneOptionalXmlBlockLikeJava()
	{
		var player = new Player
		{
			Level = 50,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			TitleId = 11,
			Quests =
			[
				new PlayerQuestState(7001, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1),
			],
		};
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(4201, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(Finished: [new NearbyQuestFinishedCondition(7999)]),
				new NearbyQuestXmlStartCondition(Finished: [new NearbyQuestFinishedCondition(7001)]),
				new NearbyQuestXmlStartCondition(RequiredTitle: 11),
			]),
			new NearbyQuestTemplateSummary(4202, HasXmlStartConditions: true, XmlStartConditions:
			[
				new NearbyQuestXmlStartCondition(Finished: [new NearbyQuestFinishedCondition(7001)]),
				new NearbyQuestXmlStartCondition(RequiredTitle: 12),
			]),
		]);

		AssertPass(player, 4201, table);
		AssertFailure(player, 4202, table, NearbyQuestStartConditionFailure.XmlStartConditions);
	}

	[Fact]
	public void CheckNearbyStartConditions_FailsClosedForUnknownXmlStartConditionChildren()
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
			new NearbyQuestTemplateSummary(
				4301,
				HasXmlStartConditions: true,
				XmlStartConditions: [new NearbyQuestXmlStartCondition(HasUnsupportedElements: true)],
				HasUnsupportedXmlStartConditionElements: true),
		]);

		AssertFailure(player, 4301, table, NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions);
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

	private static void AssertPass(
		Player player,
		int questId,
		NearbyQuestTemplateTable table,
		DateTimeOffset? now = null)
	{
		var result = NearbyQuestStartConditionService.CheckNearbyStartConditions(player, questId, table, now);
		Assert.True(result.CanStart);
		Assert.Equal(NearbyQuestStartConditionFailure.None, result.Failure);
	}

	private static void AssertFailure(
		Player player,
		int questId,
		NearbyQuestTemplateTable table,
		NearbyQuestStartConditionFailure expectedFailure,
		DateTimeOffset? now = null)
	{
		var result = NearbyQuestStartConditionService.CheckNearbyStartConditions(player, questId, table, now);
		Assert.False(result.CanStart);
		Assert.Equal(expectedFailure, result.Failure);
	}
}
