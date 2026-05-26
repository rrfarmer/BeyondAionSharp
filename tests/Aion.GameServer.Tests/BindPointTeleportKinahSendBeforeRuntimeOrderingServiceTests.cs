using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahSendBeforeRuntimeOrderingServiceTests
{
	[Fact]
	public void CreatePlan_StoppedPersistenceBlocksPacketSendAndRuntime()
	{
		var scenario = CreateScenario(currentKinah: 500, requiredPrice: 1_000);

		var plan = BindPointTeleportKinahSendBeforeRuntimeOrderingService.CreatePlan(
			scenario.PersistenceDecision,
			scenario.PacketPlan,
			sendResult: null,
			runtimeResult: null);

		Assert.Equal(BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedBeforePersistence, plan.Status);
		Assert.False(plan.ShouldSendInventoryUpdatePacket);
		Assert.False(plan.ShouldStoreCooldown);
		Assert.False(plan.ShouldTeleport);
	}

	[Fact]
	public void CreatePlan_MissingSendResultBlocksRuntimeAfterPacketIntent()
	{
		var scenario = CreateScenario(currentKinah: 2_000, requiredPrice: 1_000, affectedRows: 1);

		var plan = BindPointTeleportKinahSendBeforeRuntimeOrderingService.CreatePlan(
			scenario.PersistenceDecision,
			scenario.PacketPlan,
			sendResult: null,
			runtimeResult: CreateRuntimeResult(scenario.CallbackPlan));

		Assert.Equal(BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedMissingSendResult, plan.Status);
		Assert.Contains(BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateInventoryUpdatePacketIntent, plan.Steps);
		Assert.DoesNotContain(BindPointTeleportKinahSendBeforeRuntimeOrderingStep.StoreCooldown, plan.Steps);
		Assert.False(plan.ShouldBroadcastCooldown);
	}

	[Fact]
	public void CreatePlan_SendFailureBlocksCooldownFanoutAndMovement()
	{
		var scenario = CreateScenario(currentKinah: 2_000, requiredPrice: 1_000, affectedRows: 1);

		var plan = BindPointTeleportKinahSendBeforeRuntimeOrderingService.CreatePlan(
			scenario.PersistenceDecision,
			scenario.PacketPlan,
			CreateSendResult(BindPointTeleportKinahInventorySendStatus.Failed),
			CreateRuntimeResult(scenario.CallbackPlan));

		Assert.Equal(BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedSendFailed, plan.Status);
		Assert.Contains(BindPointTeleportKinahSendBeforeRuntimeOrderingStep.SendInventoryUpdatePacket, plan.Steps);
		Assert.False(plan.ShouldStoreCooldown);
		Assert.False(plan.ShouldScheduleFinalTeleport);
	}

	[Fact]
	public void CreatePlan_SentPacketWaitsForRuntimeCallbackMetadata()
	{
		var scenario = CreateScenario(currentKinah: 2_000, requiredPrice: 1_000, affectedRows: 1);

		var plan = BindPointTeleportKinahSendBeforeRuntimeOrderingService.CreatePlan(
			scenario.PersistenceDecision,
			scenario.PacketPlan,
			CreateSendResult(BindPointTeleportKinahInventorySendStatus.Sent),
			runtimeResult: null);

		Assert.Equal(BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.AwaitingRuntimeCallback, plan.Status);
		Assert.Contains(BindPointTeleportKinahSendBeforeRuntimeOrderingStep.SendInventoryUpdatePacket, plan.Steps);
		Assert.False(plan.ShouldBroadcastCooldown);
		Assert.False(plan.ShouldTeleport);
	}

	[Fact]
	public void CreatePlan_SentPacketThenRuntimeCallbackContinuesInJavaOrder()
	{
		var scenario = CreateScenario(currentKinah: 2_000, requiredPrice: 1_000, affectedRows: 1);

		var plan = BindPointTeleportKinahSendBeforeRuntimeOrderingService.CreatePlan(
			scenario.PersistenceDecision,
			scenario.PacketPlan,
			CreateSendResult(BindPointTeleportKinahInventorySendStatus.Sent),
			CreateRuntimeResult(scenario.CallbackPlan));

		Assert.Equal(BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.ReadyForRuntimeCallback, plan.Status);
		Assert.Equal(
			[
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateInventoryUpdatePacketIntent,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.SendInventoryUpdatePacket,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.StoreCooldown,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.BroadcastCooldown,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.ScheduleFinalTeleport,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateFinalMovementIntent,
			],
			plan.Steps);
		Assert.True(plan.ShouldSendInventoryUpdatePacket);
		Assert.True(plan.ShouldStoreCooldown);
		Assert.True(plan.ShouldBroadcastCooldown);
		Assert.True(plan.ShouldScheduleFinalTeleport);
		Assert.True(plan.ShouldTeleport);
		Assert.False(plan.IsLive);
	}

	private const int PlayerObjectId = 7001;
	private const int KinahObjectId = 1824;
	private const int LocId = 6001;

	private static Scenario CreateScenario(
		long currentKinah,
		long requiredPrice,
		int? affectedRows = null)
	{
		var player = CreatePlayer(currentKinah);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice);
		var callbackPlan = CreateCallbackPlan(mutationPlan, currentKinah, requiredPrice);
		var operationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(mutationPlan);
		var result = affectedRows == null
			? null
			: BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(operationPlan, affectedRows.Value);
		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(callbackPlan, result);
		var packetPlan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(decision, CreateKinahTemplate());
		return new Scenario(callbackPlan, decision, packetPlan);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		BindPointTeleportScheduledKinahMutationPlan mutationPlan,
		long currentKinah,
		long requiredPrice)
	{
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			BindPointTeleportScheduledKinahPlanService.CreatePlan(requiredPrice, currentKinah),
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

	private static BindPointTeleportKinahInventorySendResult CreateSendResult(
		BindPointTeleportKinahInventorySendStatus status)
	{
		return new BindPointTeleportKinahInventorySendResult(
			status,
			PlayerObjectId,
			SentPacket: status == BindPointTeleportKinahInventorySendStatus.Sent,
			"Supplied non-live SM_INVENTORY_UPDATE_ITEM send result",
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

	private sealed record Scenario(
		BindPointTeleportScheduledCallbackPlan CallbackPlan,
		BindPointTeleportKinahPersistenceDecision PersistenceDecision,
		BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan);
}
