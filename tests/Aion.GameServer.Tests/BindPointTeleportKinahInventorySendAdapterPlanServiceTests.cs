using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahInventorySendAdapterPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_WithoutPacketIntentReturnsFailedNoSendResult()
	{
		var packetPlan = CreatePacketPlan(
			BindPointTeleportKinahPersistenceStatus.MissingRow,
			includeTemplate: true);
		var registry = new ThrowingConnectionRegistry();

		var plan = BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(
			packetPlan,
			PlayerObjectId,
			registry);

		Assert.Equal(BindPointTeleportKinahInventorySendAdapterStatus.NoPacketIntent, plan.Status);
		Assert.Equal(BindPointTeleportKinahInventorySendStatus.Failed, plan.SendResult.Status);
		Assert.False(plan.SendResult.SentPacket);
		Assert.False(plan.WouldCallSendPacketAsync);
		Assert.False(plan.DidCallSendPacketAsync);
		Assert.False(plan.IsLive);
		Assert.Equal(0, registry.SendPacketCalls);
	}

	[Fact]
	public void CreateDisabledPlan_WithPacketIntentRecordsBoundaryWithoutCallingRegistry()
	{
		var packetPlan = CreatePacketPlan(
			BindPointTeleportKinahPersistenceStatus.Saved,
			includeTemplate: true);
		var registry = new ThrowingConnectionRegistry();

		var plan = BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(
			packetPlan,
			PlayerObjectId,
			registry);

		Assert.Equal(BindPointTeleportKinahInventorySendAdapterStatus.DisabledNoSend, plan.Status);
		Assert.Same(packetPlan, plan.PacketPlan);
		Assert.Equal(BindPointTeleportKinahInventorySendStatus.Failed, plan.SendResult.Status);
		Assert.Equal(PlayerObjectId, plan.SendResult.PlayerObjectId);
		Assert.False(plan.SendResult.SentPacket);
		Assert.True(plan.WouldCallSendPacketAsync);
		Assert.False(plan.DidCallSendPacketAsync);
		Assert.False(plan.IsLive);
		Assert.Equal(0, registry.SendPacketCalls);
	}

	[Fact]
	public void CreateDisabledPlan_DisabledSendResultStopsCooldownFanoutGate()
	{
		var packetPlan = CreatePacketPlan(
			BindPointTeleportKinahPersistenceStatus.Saved,
			includeTemplate: true);
		var composition = CreateComposition(packetPlan);
		var adapterPlan = BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(
			packetPlan,
			PlayerObjectId,
			new ThrowingConnectionRegistry());

		var decision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			adapterPlan.SendResult);

		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.StoppedSendFailed, decision.Status);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldStoreCooldown);
		Assert.False(decision.ShouldBroadcastCooldown);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
	}

	private const int PlayerObjectId = 7001;
	private const int LocId = 6001;

	private static BindPointTeleportKinahInventoryUpdatePacketPlan CreatePacketPlan(
		BindPointTeleportKinahPersistenceStatus persistenceStatus,
		bool includeTemplate)
	{
		var callbackPlan = CreateCallbackPlan();
		var persistenceResult = new BindPointTeleportKinahPersistenceResult(
			persistenceStatus,
			callbackPlan.KinahItemUpdate!.OwnerId,
			callbackPlan.KinahItemUpdate.ObjectId,
			callbackPlan.KinahItemUpdate.Count,
			ShouldRollbackInMemoryMutation: persistenceStatus != BindPointTeleportKinahPersistenceStatus.Saved,
			"InventoryDAO.store(player) dirty item persistence planned as owner-checked C# count update",
			IsLive: false);
		var persistenceDecision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult);
		return BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			persistenceDecision,
			includeTemplate ? CreateKinahTemplate() : null);
	}

	private static BindPointTeleportKinahCallbackComposition CreateComposition(
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan)
	{
		var callbackPlan = packetPlan.Decision.CallbackPlan;
		return BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			packetPlan.Decision,
			packetPlan,
			CreateRuntimeResult(callbackPlan));
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan()
	{
		var currentKinah = 2_000L;
		var requiredPrice = 1_000L;
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(requiredPrice, currentKinah);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = 1824,
						OwnerId = PlayerObjectId,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice);
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

	private sealed class ThrowingConnectionRegistry : IGameClientConnectionRegistry
	{
		public int SendPacketCalls { get; private set; }

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SendPacketCalls++;
			throw new InvalidOperationException("Disabled send adapter must not call SendPacketAsync.");
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}
