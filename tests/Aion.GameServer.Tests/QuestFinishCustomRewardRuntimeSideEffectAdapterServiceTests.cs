using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishCustomRewardRuntimeSideEffectAdapterServiceTests
{
	[Fact]
	public async Task CreateContextAsync_DisabledGateKeepsQuestFinishContextUnchanged()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = CreateService(repository);
		var context = CreateSideEffectContext(levelChangeContextInput: null);

		var result = await service.CreateContextAsync(
			context,
			QuestXpCustomRewardRuntimeInputAdapterOptions.Disabled);

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.Disabled, result.Status);
		Assert.Same(context, result.Context);
		Assert.Null(result.Context.LevelChangeContextInput);
		Assert.Empty(repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
	}

	[Fact]
	public async Task CreateContextAsync_RequiresQuestFinishLevelChangeInputBeforeRepositoryExecution()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = CreateService(repository);
		var context = CreateSideEffectContext(levelChangeContextInput: null);

		var result = await service.CreateContextAsync(
			context,
			new QuestXpCustomRewardRuntimeInputAdapterOptions(
				EnableCustomRewardExecution: true,
				NextObjectId: () => 9501));

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.MissingDependency, result.Status);
		Assert.Equal("levelChangeContextInput", result.MissingDependency);
		Assert.Same(context, result.Context);
		Assert.Empty(repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
	}

	[Fact]
	public async Task CreateContextAsync_PropagatesInputAdapterDependencyGuardWithoutReplacingContext()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = CreateService(repository);
		var input = new QuestXpLevelChangeContextFactoryInput(FromLevel: 64, ToLevel: 65);
		var context = CreateSideEffectContext(input);

		var result = await service.CreateContextAsync(
			context,
			new QuestXpCustomRewardRuntimeInputAdapterOptions(EnableCustomRewardExecution: true));

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.MissingDependency, result.Status);
		Assert.Equal("nextObjectId", result.MissingDependency);
		Assert.NotNull(result.InputAdapterResult);
		Assert.Same(context, result.Context);
		Assert.Same(input, result.Context.LevelChangeContextInput);
		Assert.Empty(repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
	}

	[Fact]
	public async Task CreateContextAsync_ComposesExecutionResultsIntoQuestFinishXpPlan()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = CreateService(repository);
		var nextIds = new Queue<int>(Enumerable.Range(9501, 40));
		var itemTemplates = CreateTemplatesWithAdditional(
			CustomLevelRewardPlanService.BonusRewards.Select(reward => reward.ItemId)
				.Concat(CustomLevelRewardPlanService.FactionRewards.Select(reward => reward.ItemId))
				.Distinct()
				.Where(itemId => itemId != 162002030),
			(162002030, "ELYOS"));
		var input = new QuestXpLevelChangeContextFactoryInput(
			FromLevel: 64,
			ToLevel: 65,
			FactionPackAccountCreationLocalTime: new DateTime(2022, 6, 18, 0, 0, 0),
			ItemTemplates: itemTemplates);
		var player = CreatePlayer(level: 65, race: "ASMODIANS");
		var context = CreateSideEffectContext(input, player);

		var result = await service.CreateContextAsync(
			context,
			new QuestXpCustomRewardRuntimeInputAdapterOptions(
				EnableCustomRewardExecution: true,
				NextObjectId: () => nextIds.Dequeue(),
				ReceivedTime: new DateTime(2026, 5, 26, 14, 0, 0),
				FactionPackAccountCreationLocalTime: new DateTime(2022, 6, 18, 0, 0, 0),
				ItemTemplates: itemTemplates));

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.Created, result.Status);
		Assert.True(result.Applied);
		Assert.NotSame(context, result.Context);
		Assert.NotSame(input, result.Context.LevelChangeContextInput);
		Assert.Equal([CustomLevelRewardReceiptKind.Bonus, CustomLevelRewardReceiptKind.Faction], repository.LoadKinds);
		Assert.Equal(
		[
			(CustomLevelRewardReceiptKind.Bonus, 3301, 4701),
			(CustomLevelRewardReceiptKind.Faction, 3301, 4701),
		], repository.StoreCalls);

		// Java has already mutated the player's level when PlayerController.onLevelChange runs custom rewards.
		// The C# quest-finish XP planner is still non-mutating, so this simulates the pre-mutation snapshot
		// it currently uses to detect the level-change boundary.
		player.Level = 64;
		player.Exp = 63_900;
		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions(),
			new QuestFinishRewardTemplateProjection(
				RewardGroupCount: 1,
				HasNonItemRewards: true,
				NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(Experience: 400)),
			rewardSideEffectContext: result.Context);

		var sideEffect = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.XpExecutionPlan is not null);
		var customRewardSubPlans = sideEffect.XpExecutionPlan!.LevelChangeSubPlans
			.Where(subPlan => subPlan.Action is QuestXpExecutionAction.BonusPackReward or QuestXpExecutionAction.FactionPackReward)
			.ToArray();
		Assert.Equal([QuestXpExecutionAction.BonusPackReward, QuestXpExecutionAction.FactionPackReward], customRewardSubPlans.Select(subPlan => subPlan.Action));
		Assert.Equal(nameof(CustomLevelRewardExecutionService), customRewardSubPlans[0].CSharpPlan);
		Assert.Equal(nameof(CustomLevelRewardExecutionService), customRewardSubPlans[1].CSharpPlan);
		Assert.All(customRewardSubPlans, subPlan => Assert.True(subPlan.IsLive));
		Assert.Equal(CustomLevelRewardPlanService.BonusRewards.Count, customRewardSubPlans[0].PlannedDescriptorCount);
		Assert.Equal(5, customRewardSubPlans[1].PlannedDescriptorCount);
		Assert.Equal(65, sideEffect.XpExecutionPlan.CurrentLevel);
		Assert.Equal(65, sideEffect.XpExecutionPlan.MinNewLevel);
	}

	private static QuestFinishCustomRewardRuntimeSideEffectAdapterService CreateService(
		ICustomLevelRewardRepository repository)
	{
		return new QuestFinishCustomRewardRuntimeSideEffectAdapterService(
			new QuestXpCustomRewardRuntimeInputAdapterService(
				new CustomLevelRewardExecutionService(repository)));
	}

	private static QuestFinishRewardSideEffectContext CreateSideEffectContext(
		QuestXpLevelChangeContextFactoryInput? levelChangeContextInput,
		Player? player = null)
	{
		return new QuestFinishRewardSideEffectContext(
			player ?? CreatePlayer(),
			ExperienceTable: CreateLinearExperienceTable(),
			LevelChangeContextInput: levelChangeContextInput);
	}

	private static Player CreatePlayer(int level = 64, string race = "ELYOS")
	{
		return new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Questfinish",
			Race = race,
			PlayerClass = "RANGER",
			Level = level,
			Exp = 64_900,
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}

	private static PlayerExperienceTable CreateLinearExperienceTable()
	{
		return new PlayerExperienceTable(Enumerable.Range(0, 70).Select(level => (long)level * 1000).ToArray());
	}

	private static GameServerOptions CreateOptions()
	{
		return new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = "UTC",
			},
		};
	}

	private static ItemTemplateTable CreateTemplatesWithAdditional(IEnumerable<int> itemIds, params (int ItemId, string Race)[] additionalTemplates)
	{
		return new ItemTemplateTable(itemIds
			.Select(itemId => (ItemId: itemId, Race: "PC_ALL"))
			.Concat(additionalTemplates)
			.Select(item => new ItemTemplateSummary(item.ItemId, $"Item {item.ItemId}", 0, 0, 1, "NONE", "NORMAL", "COMMON", item.Race, 100, 0, 0))
			.ToArray());
	}

	private sealed class RecordingCustomLevelRewardRepository : ICustomLevelRewardRepository
	{
		private readonly int _loadReceivingPlayerId;
		private readonly bool _storeSucceeded;

		public RecordingCustomLevelRewardRepository(int loadReceivingPlayerId, bool storeSucceeded)
		{
			_loadReceivingPlayerId = loadReceivingPlayerId;
			_storeSucceeded = storeSucceeded;
		}

		public List<CustomLevelRewardReceiptKind> LoadKinds { get; } = [];

		public List<(CustomLevelRewardReceiptKind Kind, int AccountId, int PlayerObjectId)> StoreCalls { get; } = [];

		public Task<int> LoadReceivingPlayerAsync(
			CustomLevelRewardReceiptKind kind,
			int accountId,
			CancellationToken cancellationToken = default)
		{
			LoadKinds.Add(kind);
			return Task.FromResult(_loadReceivingPlayerId);
		}

		public Task<bool> StoreReceivingPlayerAsync(
			CustomLevelRewardReceiptKind kind,
			int accountId,
			int playerObjectId,
			CancellationToken cancellationToken = default)
		{
			StoreCalls.Add((kind, accountId, playerObjectId));
			return Task.FromResult(_storeSucceeded);
		}
	}
}
