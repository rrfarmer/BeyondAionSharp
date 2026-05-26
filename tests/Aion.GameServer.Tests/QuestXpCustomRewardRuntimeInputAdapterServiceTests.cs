using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestXpCustomRewardRuntimeInputAdapterServiceTests
{
	[Fact]
	public async Task CreateInputAsync_DisabledGateDoesNotExecuteCustomRewardRepositories()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new QuestXpCustomRewardRuntimeInputAdapterService(new CustomLevelRewardExecutionService(repository));
		var input = new QuestXpLevelChangeContextFactoryInput(FromLevel: 64, ToLevel: 65);

		var result = await service.CreateInputAsync(
			CreatePlayer(),
			input,
			QuestXpCustomRewardRuntimeInputAdapterOptions.Disabled);

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.Disabled, result.Status);
		Assert.Same(input, result.Input);
		Assert.Null(result.Input.BonusPackExecutionResult);
		Assert.Null(result.Input.FactionPackExecutionResult);
		Assert.Empty(repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
	}

	[Fact]
	public async Task CreateInputAsync_RequiresObjectIdFactoryBeforeRepositoryExecution()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new QuestXpCustomRewardRuntimeInputAdapterService(new CustomLevelRewardExecutionService(repository));
		var input = new QuestXpLevelChangeContextFactoryInput(FromLevel: 64, ToLevel: 65);

		var result = await service.CreateInputAsync(
			CreatePlayer(),
			input,
			new QuestXpCustomRewardRuntimeInputAdapterOptions(EnableCustomRewardExecution: true));

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.MissingDependency, result.Status);
		Assert.Equal("nextObjectId", result.MissingDependency);
		Assert.Same(input, result.Input);
		Assert.Empty(repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
	}

	[Fact]
	public async Task CreateInputAsync_CreatesBonusThenFactionExecutionResultsForXpComposition()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new QuestXpCustomRewardRuntimeInputAdapterService(new CustomLevelRewardExecutionService(repository));
		var nextIds = new Queue<int>(Enumerable.Range(9501, 40));
		var itemTemplates = CreateTemplatesWithAdditional(
			CustomLevelRewardPlanService.BonusRewards.Select(reward => reward.ItemId)
				.Concat(CustomLevelRewardPlanService.FactionRewards.Select(reward => reward.ItemId))
				.Distinct()
				.Where(itemId => itemId != 162002030),
			(162002030, "ELYOS"));
		var input = new QuestXpLevelChangeContextFactoryInput(FromLevel: 64, ToLevel: 65);

		var result = await service.CreateInputAsync(
			CreatePlayer(race: "ASMODIANS"),
			input,
			new QuestXpCustomRewardRuntimeInputAdapterOptions(
				EnableCustomRewardExecution: true,
				NextObjectId: () => nextIds.Dequeue(),
				ReceivedTime: new DateTime(2026, 5, 26, 13, 0, 0),
				FactionPackAccountCreationLocalTime: new DateTime(2022, 6, 18, 0, 0, 0),
				ItemTemplates: itemTemplates));

		Assert.Equal(QuestXpCustomRewardRuntimeInputAdapterStatus.Created, result.Status);
		Assert.True(result.Applied);
		Assert.NotSame(input, result.Input);
		Assert.Equal([CustomLevelRewardReceiptKind.Bonus, CustomLevelRewardReceiptKind.Faction], repository.LoadKinds);
		Assert.Equal(
			[
				(CustomLevelRewardReceiptKind.Bonus, 3301, 4701),
				(CustomLevelRewardReceiptKind.Faction, 3301, 4701),
			],
			repository.StoreCalls);
		Assert.NotNull(result.Input.BonusPackExecutionResult);
		Assert.NotNull(result.Input.FactionPackExecutionResult);
		Assert.Equal(CustomLevelRewardExecutionStatus.PlannedMail, result.Input.BonusPackExecutionResult.Status);
		Assert.Equal(CustomLevelRewardExecutionStatus.PlannedMail, result.Input.FactionPackExecutionResult.Status);
		Assert.Equal(CustomLevelRewardPlanService.BonusRewards.Count, result.Input.BonusPackExecutionResult.MailPlans.Count);
		Assert.Equal(5, result.Input.FactionPackExecutionResult.MailPlans.Count);
		Assert.Same(itemTemplates, result.Input.ItemTemplates);
	}

	private static Player CreatePlayer(int level = 65, string race = "ELYOS")
	{
		return new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Customreward",
			Race = race,
			PlayerClass = "RANGER",
			Level = level,
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
