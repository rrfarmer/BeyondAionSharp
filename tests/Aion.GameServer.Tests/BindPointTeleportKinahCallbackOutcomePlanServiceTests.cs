using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahCallbackOutcomePlanServiceTests
{
	[Fact]
	public void CreatePlan_NotEnoughKinahStopsBeforePersistence()
	{
		var scenario = CreateScenario(currentKinah: 500, requiredPrice: 1_000);

		var outcome = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			scenario.MutationPlan,
			scenario.PersistenceOperationPlan,
			scenario.PersistenceDecision,
			scenario.SendAdapterPlan,
			scenario.SendDecision,
			scenario.OwnerRollbackPlan);

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.StoppedNotEnoughKinah, outcome.Status);
		Assert.False(outcome.ShouldExecuteSql);
		Assert.False(outcome.ShouldSendInventoryUpdatePacket);
		Assert.False(outcome.ShouldRollbackInMemoryMutation);
		Assert.False(outcome.ShouldContinueToCooldownFanout);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreatePlan_NonPositivePriceContinuesWithoutMutation()
	{
		var scenario = CreateScenario(currentKinah: 2_000, requiredPrice: 0);

		var outcome = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			scenario.MutationPlan,
			scenario.PersistenceOperationPlan,
			scenario.PersistenceDecision,
			scenario.SendAdapterPlan,
			scenario.SendDecision,
			scenario.OwnerRollbackPlan);

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.ContinueWithoutMutation, outcome.Status);
		Assert.False(outcome.ShouldExecuteSql);
		Assert.False(outcome.ShouldSendInventoryUpdatePacket);
		Assert.False(outcome.ShouldRollbackInMemoryMutation);
		Assert.False(outcome.ShouldCommitInMemoryMutation);
		Assert.True(outcome.ShouldContinueToCooldownFanout);
		Assert.True(outcome.ShouldScheduleFinalTeleport);
		Assert.True(outcome.ShouldTeleport);
	}

	[Fact]
	public void CreatePlan_MissingPersistenceResultAwaitsBeforeSend()
	{
		var scenario = CreateScenario(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			persistenceAffectedRows: null);

		var outcome = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			scenario.MutationPlan,
			scenario.PersistenceOperationPlan,
			scenario.PersistenceDecision,
			scenario.SendAdapterPlan,
			scenario.SendDecision,
			scenario.OwnerRollbackPlan);

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.AwaitingPersistenceResult, outcome.Status);
		Assert.True(outcome.ShouldExecuteSql);
		Assert.False(outcome.ShouldSendInventoryUpdatePacket);
		Assert.True(outcome.ShouldRollbackInMemoryMutation);
		Assert.False(outcome.ShouldContinueToCooldownFanout);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void CreatePlan_PersistenceFailureRequiresRollback(int affectedRows)
	{
		var scenario = CreateScenario(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			persistenceAffectedRows: affectedRows);

		var outcome = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			scenario.MutationPlan,
			scenario.PersistenceOperationPlan,
			scenario.PersistenceDecision,
			scenario.SendAdapterPlan,
			scenario.SendDecision,
			scenario.OwnerRollbackPlan);

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterPersistenceFailure, outcome.Status);
		Assert.True(outcome.ShouldExecuteSql);
		Assert.True(outcome.ShouldRollbackInMemoryMutation);
		Assert.False(outcome.ShouldSendInventoryUpdatePacket);
		Assert.False(outcome.ShouldContinueToCooldownFanout);
		Assert.Contains(BindPointTeleportKinahCallbackOutcomeStep.RollbackMutation, outcome.Steps);
	}

	[Fact]
	public void CreatePlan_DisabledSendRequiresRollbackAfterPersistence()
	{
		var scenario = CreateScenario(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			persistenceAffectedRows: 1,
			useDisabledSendAdapter: true);

		var outcome = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			scenario.MutationPlan,
			scenario.PersistenceOperationPlan,
			scenario.PersistenceDecision,
			scenario.SendAdapterPlan,
			scenario.SendDecision,
			scenario.OwnerRollbackPlan);

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.RollbackAfterSendFailure, outcome.Status);
		Assert.True(outcome.ShouldExecuteSql);
		Assert.True(outcome.ShouldRollbackInMemoryMutation);
		Assert.False(outcome.ShouldSendInventoryUpdatePacket);
		Assert.False(outcome.ShouldContinueToCooldownFanout);
	}

	[Fact]
	public void CreatePlan_SavedAndSentCommitsAndContinues()
	{
		var scenario = CreateScenario(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			persistenceAffectedRows: 1,
			sendStatus: BindPointTeleportKinahInventorySendStatus.Sent);

		var outcome = BindPointTeleportKinahCallbackOutcomePlanService.CreatePlan(
			scenario.MutationPlan,
			scenario.PersistenceOperationPlan,
			scenario.PersistenceDecision,
			scenario.SendAdapterPlan,
			scenario.SendDecision,
			scenario.OwnerRollbackPlan);

		Assert.Equal(BindPointTeleportKinahCallbackOutcomeStatus.ReadyForCooldownFanout, outcome.Status);
		Assert.True(outcome.ShouldExecuteSql);
		Assert.True(outcome.ShouldSendInventoryUpdatePacket);
		Assert.False(outcome.ShouldRollbackInMemoryMutation);
		Assert.True(outcome.ShouldCommitInMemoryMutation);
		Assert.True(outcome.ShouldContinueToCooldownFanout);
		Assert.True(outcome.ShouldScheduleFinalTeleport);
		Assert.True(outcome.ShouldTeleport);
	}

	private const int PlayerObjectId = 7001;
	private const int KinahObjectId = 1824;
	private const int LocId = 6001;

	private static Scenario CreateScenario(
		long currentKinah,
		long requiredPrice,
		int? persistenceAffectedRows = null,
		BindPointTeleportKinahInventorySendStatus? sendStatus = null,
		bool useDisabledSendAdapter = false)
	{
		var player = CreatePlayer(currentKinah);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice);
		var callbackPlan = CreateCallbackPlan(mutationPlan);
		var operationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(mutationPlan);
		var persistenceResult = persistenceAffectedRows == null
			? null
			: BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(
				operationPlan,
				persistenceAffectedRows.Value);
		var persistenceDecision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult);

		BindPointTeleportKinahInventorySendAdapterPlan? sendAdapterPlan = null;
		BindPointTeleportKinahInventorySendDecision? sendDecision = null;
		if (persistenceDecision.Status == BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence)
		{
			var packetPlan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
				persistenceDecision,
				CreateKinahTemplate());
			var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
				persistenceDecision,
				packetPlan,
				CreateRuntimeResult(callbackPlan));
			sendAdapterPlan = useDisabledSendAdapter
				? BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(packetPlan, PlayerObjectId)
				: CreateSuppliedSendAdapterPlan(packetPlan, sendStatus ?? BindPointTeleportKinahInventorySendStatus.Failed);
			sendDecision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
				composition,
				sendAdapterPlan.SendResult);
		}

		var ownerRollbackPlan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(
			player,
			mutationPlan,
			persistenceDecision,
			sendDecision);
		return new Scenario(
			mutationPlan,
			operationPlan,
			persistenceDecision,
			sendAdapterPlan,
			sendDecision,
			ownerRollbackPlan);
	}

	private static BindPointTeleportKinahInventorySendAdapterPlan CreateSuppliedSendAdapterPlan(
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		BindPointTeleportKinahInventorySendStatus status)
	{
		var sendResult = new BindPointTeleportKinahInventorySendResult(
			status,
			PlayerObjectId,
			SentPacket: status == BindPointTeleportKinahInventorySendStatus.Sent,
			"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) supplied test result",
			IsLive: false);
		return new BindPointTeleportKinahInventorySendAdapterPlan(
			BindPointTeleportKinahInventorySendAdapterStatus.DisabledNoSend,
			packetPlan,
			sendResult,
			WouldCallSendPacketAsync: true,
			DidCallSendPacketAsync: false,
			"Supplied send-result metadata for non-live callback outcome composition",
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

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		BindPointTeleportScheduledKinahMutationPlan mutationPlan)
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			mutationPlan.RequiredPrice,
			mutationPlan.CurrentKinah);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			PlayerObjectId,
			LocId,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			PlayerObjectId,
			SmBindPointTeleport.Cooldown(PlayerObjectId, LocId, cooldownSeconds: 600));
		var movementPlan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			new BindPointTeleportDestinationFact(210010000, 1, 2, 3, 0, 210010000, 1),
			playerIsDead: false,
			playerIsAboutToDie: false);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			kinahMutationPlan: mutationPlan);
	}

	private static BindPointTeleportRuntimeCallbackExecutionResult CreateRuntimeResult(
		BindPointTeleportScheduledCallbackPlan callbackPlan)
	{
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

	private sealed record Scenario(
		BindPointTeleportScheduledKinahMutationPlan MutationPlan,
		BindPointTeleportKinahPersistenceOperationPlan PersistenceOperationPlan,
		BindPointTeleportKinahPersistenceDecision PersistenceDecision,
		BindPointTeleportKinahInventorySendAdapterPlan? SendAdapterPlan,
		BindPointTeleportKinahInventorySendDecision? SendDecision,
		BindPointTeleportKinahOwnerRollbackPlan OwnerRollbackPlan);
}
