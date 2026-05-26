using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SystemMailRewardPersistenceExecutionServiceTests
{
	[Fact]
	public async Task ExecuteAsync_DisabledGateDoesNotExecutePlannedOperations()
	{
		var service = new SystemMailRewardPersistenceExecutionService();
		var executor = new RecordingExecutor();
		var plan = CreatePersistencePlan();

		var result = await service.ExecuteAsync(
			plan,
			executor,
			SystemMailRewardPersistenceExecutionOptions.Disabled);

		Assert.Equal(SystemMailRewardPersistenceExecutionStatus.Disabled, result.Status);
		Assert.False(result.IsLive);
		Assert.Empty(result.ExecutedOperations);
		Assert.Empty(executor.Operations);
		Assert.Contains("disabled", result.JavaSource, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ExecuteAsync_StopsAfterJavaStoreLetterFailure()
	{
		var service = new SystemMailRewardPersistenceExecutionService();
		var executor = new RecordingExecutor(SystemMailRewardPersistenceOperationKind.StoreLetter);
		var plan = CreatePersistencePlan();

		var result = await service.ExecuteAsync(
			plan,
			executor,
			new SystemMailRewardPersistenceExecutionOptions(EnableLivePersistence: true));

		Assert.Equal(SystemMailRewardPersistenceExecutionStatus.StoreLetterFailed, result.Status);
		Assert.True(result.IsLive);
		Assert.False(result.Applied);
		Assert.Single(result.ExecutedOperations);
		Assert.Single(result.FailedOperations);
		Assert.Equal(SystemMailRewardPersistenceOperationKind.StoreLetter, executor.Operations.Single().Kind);
	}

	[Fact]
	public async Task ExecuteAsync_StopsAfterJavaAttachedItemStoreFailureBeforeMailboxFanout()
	{
		var service = new SystemMailRewardPersistenceExecutionService();
		var executor = new RecordingExecutor(SystemMailRewardPersistenceOperationKind.StoreAttachedItem);
		var plan = CreatePersistencePlan();

		var result = await service.ExecuteAsync(
			plan,
			executor,
			new SystemMailRewardPersistenceExecutionOptions(EnableLivePersistence: true));

		Assert.Equal(SystemMailRewardPersistenceExecutionStatus.StoreAttachedItemFailed, result.Status);
		Assert.Equal(
			[
				SystemMailRewardPersistenceOperationKind.StoreLetter,
				SystemMailRewardPersistenceOperationKind.StoreAttachedItem,
			],
			result.ExecutedOperations.Select(operation => operation.Kind).ToArray());
		Assert.DoesNotContain(
			executor.Operations,
			operation => operation.Kind == SystemMailRewardPersistenceOperationKind.UpdateOfflineMailboxCounter);
	}

	[Fact]
	public async Task ExecuteAsync_CompletesAllOperationsWhenEnabledAndExecutorSucceeds()
	{
		var service = new SystemMailRewardPersistenceExecutionService();
		var executor = new RecordingExecutor();
		var plan = CreatePersistencePlan();

		var result = await service.ExecuteAsync(
			plan,
			executor,
			new SystemMailRewardPersistenceExecutionOptions(EnableLivePersistence: true));

		Assert.Equal(SystemMailRewardPersistenceExecutionStatus.Completed, result.Status);
		Assert.True(result.Applied);
		Assert.True(result.IsLive);
		Assert.Equal(plan.Operations, result.ExecutedOperations);
		Assert.Empty(result.FailedOperations);
	}

	private static SystemMailRewardPersistencePlan CreatePersistencePlan()
	{
		var player = new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Mailreward",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Mailbox =
			[
				new PlayerMail(1, 4701, "sender", "title", "message", true, 0, 0, 0, 0, DateTime.UnixEpoch),
			],
		};
		var descriptor = new CustomLevelRewardDescriptor(
			new CustomLevelRewardItem(186000242, 15),
			CustomLevelRewardDescriptorStatus.PlannedSystemMail,
			"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
			TemplateRace: null);
		var mailPlan = SystemMailRewardPlanService.CreatePlan(
			player,
			descriptor,
			mailObjectId: 9001,
			attachedItemObjectId: 9101,
			DateTime.UnixEpoch,
			new ItemTemplateTable(
			[
				new ItemTemplateSummary(186000242, "Item 186000242", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 100, 0, 0),
			]));

		return SystemMailRewardPersistencePlanService.CreatePlan(
			mailPlan,
			SystemMailRecipientRuntimeState.Offline);
	}

	private sealed class RecordingExecutor : ISystemMailRewardPersistenceOperationExecutor
	{
		private readonly SystemMailRewardPersistenceOperationKind? _failureKind;

		public RecordingExecutor(SystemMailRewardPersistenceOperationKind? failureKind = null)
		{
			_failureKind = failureKind;
		}

		public List<SystemMailRewardPersistenceOperation> Operations { get; } = [];

		public Task<bool> ExecuteAsync(SystemMailRewardPersistenceOperation operation, CancellationToken cancellationToken = default)
		{
			Operations.Add(operation);
			return Task.FromResult(operation.Kind != _failureKind);
		}
	}
}
