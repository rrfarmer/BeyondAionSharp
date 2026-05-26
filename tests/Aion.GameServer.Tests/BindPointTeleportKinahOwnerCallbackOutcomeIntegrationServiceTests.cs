using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahOwnerCallbackOutcomeIntegrationServiceTests
{
	[Fact]
	public void CreatePlan_NotEnoughOwnerResultStopsBeforePersistence()
	{
		var player = CreatePlayer(currentKinah: 500);

		var plan = CreatePlan(player, requiredPrice: 1_000);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah, plan.OwnerMutationResult.Status);
		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.StoppedNotEnoughKinah, plan.OutcomePlan.Status);
		Assert.False(plan.OutcomePlan.ShouldExecuteSql);
		Assert.False(plan.OutcomePlan.ShouldSendInventoryUpdatePacket);
		Assert.False(plan.OutcomePlan.ShouldContinueToCooldownFanout);
		Assert.False(plan.OutcomePlan.ShouldTeleport);
		Assert.False(plan.DidRollbackOwnerMutation);
		Assert.Equal(500, player.InventoryItems.Single().Count);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NonPositivePriceContinuesWithoutMutation()
	{
		var player = CreatePlayer(currentKinah: 500);

		var plan = CreatePlan(player, requiredPrice: 0);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.ContinueWithoutMutation, plan.OwnerMutationResult.Status);
		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.ContinueWithoutMutation, plan.OutcomePlan.Status);
		Assert.False(plan.OutcomePlan.ShouldExecuteSql);
		Assert.False(plan.OutcomePlan.ShouldSendInventoryUpdatePacket);
		Assert.False(plan.OutcomePlan.ShouldRollbackInMemoryMutation);
		Assert.True(plan.OutcomePlan.ShouldContinueToCooldownFanout);
		Assert.True(plan.OutcomePlan.ShouldTeleport);
		Assert.Equal(500, player.InventoryItems.Single().Count);
	}

	[Theory]
	[InlineData(0, BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterPersistenceFailure)]
	[InlineData(2, BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterPersistenceFailure)]
	public void CreatePlan_PersistenceFailureRollsBackOwnerMutation(
		int affectedRows,
		BindPointTeleportKinahCallbackOutcomeStatus expectedStatus)
	{
		var player = CreatePlayer(currentKinah: 2_000);

		var plan = CreatePlan(player, requiredPrice: 1_000, persistenceAffectedRows: affectedRows);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.AppliedMutation, plan.OwnerMutationResult.Status);
		Assert.Equal(expectedStatus, plan.OutcomePlan.Status);
		Assert.True(plan.OutcomePlan.ShouldExecuteSql);
		Assert.True(plan.OutcomePlan.ShouldRollbackInMemoryMutation);
		Assert.True(plan.DidRollbackOwnerMutation);
		Assert.False(plan.OutcomePlan.ShouldSendInventoryUpdatePacket);
		Assert.False(plan.OutcomePlan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void CreatePlan_DisabledSendRollsBackOwnerMutationAfterPersistence()
	{
		var player = CreatePlayer(currentKinah: 2_000);

		var plan = CreatePlan(
			player,
			requiredPrice: 1_000,
			persistenceAffectedRows: 1,
			useDisabledSendAdapter: true,
			runtimeResult: CreateRuntimeResult(currentKinah: 2_000, requiredPrice: 1_000));

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterSendFailure, plan.OutcomePlan.Status);
		Assert.Equal(BindPointTeleportKinahInventorySendAdapterStatus.DisabledNoSend, plan.SendAdapterPlan?.Status);
		Assert.True(plan.OutcomePlan.ShouldRollbackInMemoryMutation);
		Assert.True(plan.DidRollbackOwnerMutation);
		Assert.False(plan.OutcomePlan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void CreatePlan_SavedAndSentCommitsOwnerMutationAndContinues()
	{
		var player = CreatePlayer(currentKinah: 2_000);

		var plan = CreatePlan(
			player,
			requiredPrice: 1_000,
			persistenceAffectedRows: 1,
			suppliedSendResult: CreateSendResult(BindPointTeleportKinahInventorySendStatus.Sent),
			runtimeResult: CreateRuntimeResult(currentKinah: 2_000, requiredPrice: 1_000));

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.ReadyForCooldownFanout, plan.OutcomePlan.Status);
		Assert.Equal(BindPointTeleportKinahInventoryUpdatePacketPlanStatus.PacketReady, plan.PacketPlan.Status);
		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.ReadyForCooldownFanout, plan.SendDecision?.Status);
		Assert.True(plan.OutcomePlan.ShouldSendInventoryUpdatePacket);
		Assert.True(plan.OutcomePlan.ShouldCommitInMemoryMutation);
		Assert.True(plan.DidCommitOwnerMutation);
		Assert.False(plan.DidRollbackOwnerMutation);
		Assert.True(plan.OutcomePlan.ShouldContinueToCooldownFanout);
		Assert.True(plan.OutcomePlan.ShouldTeleport);
		Assert.Equal(1_000, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void CreatePlan_ExactPriceCommitsZeroCountKinahItem()
	{
		var player = CreatePlayer(currentKinah: 1_000);

		var plan = CreatePlan(
			player,
			requiredPrice: 1_000,
			persistenceAffectedRows: 1,
			suppliedSendResult: CreateSendResult(BindPointTeleportKinahInventorySendStatus.Sent, kinahCount: 0),
			runtimeResult: CreateRuntimeResult(currentKinah: 1_000, requiredPrice: 1_000));

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.ReadyForCooldownFanout, plan.OutcomePlan.Status);
		Assert.Equal(0, plan.OwnerMutationResult.UpdatedKinahItem?.Count);
		Assert.Equal(0, plan.PersistenceResult?.KinahCount);
		Assert.Equal(0, player.InventoryItems.Single().Count);
		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanService.KinahItemId, player.InventoryItems.Single().ItemId);
	}

	private const int PlayerObjectId = 7001;
	private const int KinahObjectId = 1824;
	private const int LocId = 6001;

	private static BindPointTeleportKinahOwnerCallbackOutcomeIntegrationPlan CreatePlan(
		Player player,
		long requiredPrice,
		int? persistenceAffectedRows = null,
		BindPointTeleportKinahInventorySendResult? suppliedSendResult = null,
		bool useDisabledSendAdapter = false,
		BindPointTeleportRuntimeCallbackExecutionResult? runtimeResult = null)
	{
		var service = new BindPointTeleportKinahOwnerCallbackOutcomeIntegrationService();
		return service.CreatePlan(
			player,
			requiredPrice,
			BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
				PlayerObjectId,
				LocId,
				currentTimeMillis: 1_000),
			BindPointTeleportFanoutPlanService.CreatePlan(
				BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
				PlayerObjectId,
				SmBindPointTeleport.Cooldown(PlayerObjectId, LocId, cooldownSeconds: 600)),
			BindPointTeleportFinalMovementPlanService.CreatePlan(
				new BindPointTeleportDestinationFact(210010000, 1, 2, 3, 0, 210010000, 1),
				playerIsDead: false,
				playerIsAboutToDie: false),
			CreateKinahTemplate(),
			persistenceAffectedRows,
			suppliedSendResult: suppliedSendResult,
			useDisabledSendAdapter: useDisabledSendAdapter,
			runtimeResult: runtimeResult);
	}

	private static BindPointTeleportRuntimeCallbackExecutionResult CreateRuntimeResult(
		long currentKinah,
		long requiredPrice)
	{
		var callbackPlan = CreateCallbackPlan(currentKinah, requiredPrice);
		return new BindPointTeleportRuntimeCallbackExecutionResult(
			BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast,
			callbackPlan,
			new BindPointTeleportCooldownFact(PlayerObjectId, LocId, CooldownEndMillis: 601_000),
			new BindPointTeleportRuntimeFanoutResult(
				BindPointTeleportRuntimeFanoutStatus.BroadcastVisiblePlayersAndSelf,
				callbackPlan.CooldownFanoutPlan,
				SentCount: 1,
				SentPacket: true,
				"PacketSendUtility.broadcastPacket(player, action 3, true)",
				IsLive: false),
			callbackPlan.KinahItemUpdate,
			callbackPlan.KinahInventoryUpdateType,
			ShouldSendNotEnoughFee: false,
			callbackPlan.ShouldEmitKinahInventoryUpdatePacket,
			StoredCooldownFact: true,
			BroadcastCooldown: true,
			ShouldScheduleFinalTeleport: true,
			ShouldTeleport: true,
			"BindPointTeleportService.teleport scheduled task -> addCooldown -> broadcast action 3 -> schedule final teleport",
			IsLive: false);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		long currentKinah,
		long requiredPrice)
	{
		var player = CreatePlayer(currentKinah);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice);
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(requiredPrice, currentKinah);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
				PlayerObjectId,
				LocId,
				currentTimeMillis: 1_000),
			BindPointTeleportFanoutPlanService.CreatePlan(
				BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
				PlayerObjectId,
				SmBindPointTeleport.Cooldown(PlayerObjectId, LocId, cooldownSeconds: 600)),
			BindPointTeleportFinalMovementPlanService.CreatePlan(
				new BindPointTeleportDestinationFact(210010000, 1, 2, 3, 0, 210010000, 1),
				playerIsDead: false,
				playerIsAboutToDie: false),
			kinahMutationPlan: mutationPlan);
	}

	private static BindPointTeleportKinahInventorySendResult CreateSendResult(
		BindPointTeleportKinahInventorySendStatus status,
		long kinahCount = 1_000)
	{
		return new BindPointTeleportKinahInventorySendResult(
			status,
			PlayerObjectId,
			SentPacket: status == BindPointTeleportKinahInventorySendStatus.Sent,
			$"Supplied non-live SM_INVENTORY_UPDATE_ITEM result for Kinah count {kinahCount}",
			IsLive: false);
	}

	private static Player CreatePlayer(long currentKinah)
	{
		return new Player
		{
			ObjectId = PlayerObjectId,
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = KinahObjectId,
					OwnerId = PlayerObjectId,
					ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
					Count = currentKinah,
					Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
				},
			],
		};
	}

	private static ItemTemplateSummary CreateKinahTemplate()
	{
		return new ItemTemplateSummary(
			BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
			"Kinah",
			0,
			0,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0);
	}
}
