using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CustomLevelRewardExecutionServiceTests
{
	[Fact]
	public async Task CreateBonusPackExecutionPlan_LoadsStoresReceiptThenPlansJavaSystemMail()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new CustomLevelRewardExecutionService(repository);
		var nextIds = new Queue<int>(Enumerable.Range(9001, 30));

		var result = await service.CreateBonusPackExecutionPlanAsync(
			CreatePlayer(),
			() => nextIds.Dequeue(),
			new DateTime(2026, 5, 25, 10, 0, 0),
			CreateTemplates(CustomLevelRewardPlanService.BonusRewards.Select(reward => reward.ItemId)));

		Assert.Equal(CustomLevelRewardExecutionStatus.PlannedMail, result.Status);
		Assert.True(result.Applied);
		Assert.True(result.IsLiveReceiptBoundary);
		Assert.False(result.IsLiveMailBoundary);
		Assert.Equal([CustomLevelRewardReceiptKind.Bonus], repository.LoadKinds);
		Assert.Equal([(CustomLevelRewardReceiptKind.Bonus, 3301, 4701)], repository.StoreCalls);
		Assert.Equal(CustomLevelRewardPlanService.BonusRewards.Count, result.MailPlans.Count);
		Assert.All(result.MailPlans, mailPlan =>
		{
			Assert.Equal(SystemMailRewardPlanStatus.Planned, mailPlan.Status);
			Assert.False(mailPlan.IsLive);
			Assert.NotNull(mailPlan.Mail?.AttachedItem);
			Assert.Equal(1, mailPlan.Mail.LetterType);
		});
		Assert.Equal(9001, result.MailPlans[0].Mail?.AttachedItemObjectId);
		Assert.Equal(9002, result.MailPlans[0].Mail?.Id);
	}

	[Fact]
	public async Task CreateBonusPackExecutionPlan_SkipsRepositoryWhenJavaStaticGuardsFail()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new CustomLevelRewardExecutionService(repository);

		var result = await service.CreateBonusPackExecutionPlanAsync(
			CreatePlayer(level: 64),
			() => throw new InvalidOperationException("IDs should not be allocated"),
			DateTime.UnixEpoch,
			CreateTemplates(CustomLevelRewardPlanService.BonusRewards.Select(reward => reward.ItemId)));

		Assert.Equal(CustomLevelRewardExecutionStatus.SkippedBeforeReceipt, result.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedWrongLevel, result.RewardPlan.Status);
		Assert.Empty(repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
		Assert.Empty(result.MailPlans);
		Assert.False(result.IsLiveReceiptBoundary);
	}

	[Fact]
	public async Task CreateBonusPackExecutionPlan_StopsAfterAlreadyReceivedLoad()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 4701, storeSucceeded: true);
		var service = new CustomLevelRewardExecutionService(repository);

		var result = await service.CreateBonusPackExecutionPlanAsync(
			CreatePlayer(),
			() => throw new InvalidOperationException("IDs should not be allocated"),
			DateTime.UnixEpoch,
			CreateTemplates(CustomLevelRewardPlanService.BonusRewards.Select(reward => reward.ItemId)));

		Assert.Equal(CustomLevelRewardExecutionStatus.SkippedAlreadyReceived, result.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.SkippedAlreadyReceived, result.RewardPlan.Status);
		Assert.Equal(4701, result.ReceivedPlayerId);
		Assert.Null(result.StoreReceivingPlayerSucceeded);
		Assert.Equal([CustomLevelRewardReceiptKind.Bonus], repository.LoadKinds);
		Assert.Empty(repository.StoreCalls);
		Assert.Empty(result.MailPlans);
	}

	[Fact]
	public async Task CreateFactionPackExecutionPlan_StoresBeforeOppositeRaceFilteringAndPlansDeliverableMail()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new CustomLevelRewardExecutionService(repository);
		var nextIds = new Queue<int>(Enumerable.Range(9201, 20));
		var itemTemplates = CreateTemplatesWithAdditional(
			CustomLevelRewardPlanService.FactionRewards
				.Select(reward => reward.ItemId)
				.Where(itemId => itemId != 162002030),
			(162002030, "ELYOS"));

		var result = await service.CreateFactionPackExecutionPlanAsync(
			CreatePlayer(race: "ASMODIANS"),
			new DateTime(2022, 6, 18, 0, 0, 0),
			() => nextIds.Dequeue(),
			new DateTime(2026, 5, 25, 10, 15, 0),
			itemTemplates);

		Assert.Equal(CustomLevelRewardExecutionStatus.PlannedMail, result.Status);
		Assert.Equal([(CustomLevelRewardReceiptKind.Faction, 3301, 4701)], repository.StoreCalls);
		Assert.Equal(6, result.RewardPlan.Descriptors.Count);
		Assert.Equal(5, result.MailPlans.Count);
		Assert.DoesNotContain(result.MailPlans, mailPlan => mailPlan.Mail?.AttachedItemTemplateId == 162002030);
		Assert.All(result.MailPlans, mailPlan => Assert.Equal("Faction Pack", mailPlan.Mail?.Title));
	}

	[Fact]
	public async Task CreateFactionPackExecutionPlan_ReportsNoDeliverableRewardsAfterReceiptStore()
	{
		var repository = new RecordingCustomLevelRewardRepository(loadReceivingPlayerId: 0, storeSucceeded: true);
		var service = new CustomLevelRewardExecutionService(repository);
		var itemTemplates = CreateTemplates(
			CustomLevelRewardPlanService.FactionRewards.Select(reward => (reward.ItemId, "ELYOS")));

		var result = await service.CreateFactionPackExecutionPlanAsync(
			CreatePlayer(race: "ASMODIANS"),
			new DateTime(2022, 6, 18, 0, 0, 0),
			() => throw new InvalidOperationException("IDs should not be allocated"),
			DateTime.UnixEpoch,
			itemTemplates);

		Assert.Equal(CustomLevelRewardExecutionStatus.NoDeliverableRewards, result.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.NoDeliverableRewards, result.RewardPlan.Status);
		Assert.Equal([(CustomLevelRewardReceiptKind.Faction, 3301, 4701)], repository.StoreCalls);
		Assert.Empty(result.MailPlans);
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

	private static ItemTemplateTable CreateTemplates(IEnumerable<int> itemIds)
	{
		return CreateTemplates(itemIds.Select(itemId => (itemId, "PC_ALL")));
	}

	private static ItemTemplateTable CreateTemplatesWithAdditional(IEnumerable<int> itemIds, params (int ItemId, string Race)[] additionalTemplates)
	{
		return CreateTemplates(itemIds.Select(itemId => (itemId, "PC_ALL")), additionalTemplates);
	}

	private static ItemTemplateTable CreateTemplates(IEnumerable<(int ItemId, string Race)> itemTemplates, params (int ItemId, string Race)[] additionalTemplates)
	{
		return new ItemTemplateTable(itemTemplates
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
