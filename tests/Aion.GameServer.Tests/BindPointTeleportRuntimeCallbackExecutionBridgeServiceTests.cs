using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRuntimeCallbackExecutionBridgeServiceTests
{
	[Fact]
	public async Task ExecuteCooldownFanoutAsync_KinahFailureStopsBeforeCooldownAndFanout()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var registry = new CapturingConnectionRegistry(sentCount: 0);
		var bridge = CreateBridge(owner, registry);

		var result = await bridge.ExecuteCooldownFanoutAsync(
			playerObjectId: 8401,
			CreateCallbackPlan(playerObjectId: 8401, locId: 6501, kinahContinues: false),
			new WorldPosition(210010000, 10, 20, 30, 0),
			currentTimeMillis: 1_000);

		Assert.Equal(BindPointTeleportRuntimeCallbackExecutionStatus.StoppedNotEnoughKinah, result.Status);
		Assert.True(result.ShouldSendNotEnoughFee);
		Assert.False(result.StoredCooldownFact);
		Assert.False(result.BroadcastCooldown);
		Assert.Null(result.StoredCooldown);
		Assert.Null(result.FanoutResult);
		Assert.Null(result.KinahItemUpdate);
		Assert.Null(result.KinahInventoryUpdateType);
		Assert.False(result.ShouldEmitKinahInventoryUpdatePacket);
		Assert.Null(owner.GetCooldown(8401));
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task ExecuteCooldownFanoutAsync_MissingCooldownOrFanoutMetadataDoesNotMutateRuntimeState()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var registry = new CapturingConnectionRegistry(sentCount: 0);
		var bridge = CreateBridge(owner, registry);

		var result = await bridge.ExecuteCooldownFanoutAsync(
			playerObjectId: 8402,
			CreateCallbackPlanMissingRuntimeMetadata(),
			new WorldPosition(210010000, 10, 20, 30, 0),
			currentTimeMillis: 1_000);

		Assert.Equal(BindPointTeleportRuntimeCallbackExecutionStatus.MissingCooldownOrFanoutMetadata, result.Status);
		Assert.False(result.StoredCooldownFact);
		Assert.False(result.BroadcastCooldown);
		Assert.Null(result.StoredCooldown);
		Assert.Null(result.FanoutResult);
		Assert.Null(result.KinahItemUpdate);
		Assert.Null(result.KinahInventoryUpdateType);
		Assert.Null(owner.GetCooldown(8402));
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task ExecuteCooldownFanoutAsync_KinahSuccessStoresCooldownThenBroadcastsActionThree()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var registry = new CapturingConnectionRegistry(sentCount: 4);
		var bridge = CreateBridge(owner, registry);
		var sourcePosition = new WorldPosition(210010000, 11, 22, 33, 1);

		var result = await bridge.ExecuteCooldownFanoutAsync(
			playerObjectId: 8403,
			CreateCallbackPlan(playerObjectId: 8403, locId: 6503, kinahContinues: true),
			sourcePosition,
			currentTimeMillis: 2_000);

		Assert.Equal(BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast, result.Status);
		Assert.False(result.ShouldSendNotEnoughFee);
		Assert.True(result.StoredCooldownFact);
		Assert.True(result.BroadcastCooldown);
		Assert.True(result.ShouldScheduleFinalTeleport);
		Assert.True(result.ShouldTeleport);
		Assert.Null(result.KinahItemUpdate);
		Assert.Null(result.KinahInventoryUpdateType);
		Assert.False(result.ShouldEmitKinahInventoryUpdatePacket);
		Assert.NotNull(result.StoredCooldown);
		Assert.Equal(8403, result.StoredCooldown.PlayerObjectId);
		Assert.Equal(6503, result.StoredCooldown.LocId);
		Assert.Equal(602_000, result.StoredCooldown.CooldownEndMillis);
		Assert.Equal(result.StoredCooldown, owner.GetCooldown(8403));
		Assert.NotNull(result.FanoutResult);
		Assert.Equal(4, result.FanoutResult.SentCount);
		Assert.True(result.FanoutResult.SentPacket);
		Assert.NotNull(result.FanoutResult.FanoutPlan);
		Assert.Equal(BindPointTeleportFanoutSource.TeleportCooldownBroadcast, result.FanoutResult.FanoutPlan.Source);
		Assert.True(result.FanoutResult.FanoutPlan.IncludeSourcePlayer);

		Assert.Single(registry.Broadcasts);
		Assert.Equal(sourcePosition, registry.Broadcasts[0].SourcePosition);
		Assert.Equal(8403, registry.Broadcasts[0].SourceObjectId);
		Assert.True(registry.Broadcasts[0].IncludeSourcePlayer);
		var packet = Assert.IsType<SmBindPointTeleport>(registry.Broadcasts[0].Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(8403, reader.ReadD());
		Assert.Equal(6503, reader.ReadD());
		Assert.Equal(600, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public async Task ExecuteCooldownFanoutAsync_BlockedFinalMovementStillStoresAndBroadcastsLikeJava()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var registry = new CapturingConnectionRegistry(sentCount: 1);
		var bridge = CreateBridge(owner, registry);

		var result = await bridge.ExecuteCooldownFanoutAsync(
			playerObjectId: 8404,
			CreateCallbackPlan(
				playerObjectId: 8404,
				locId: 6504,
				kinahContinues: true,
				playerIsAboutToDie: true),
			new WorldPosition(210010000, 12, 23, 34, 2),
			currentTimeMillis: 3_000);

		Assert.Equal(BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast, result.Status);
		Assert.True(result.StoredCooldownFact);
		Assert.True(result.BroadcastCooldown);
		Assert.True(result.ShouldScheduleFinalTeleport);
		Assert.False(result.ShouldTeleport);
		Assert.False(result.ShouldEmitKinahInventoryUpdatePacket);
		Assert.NotNull(owner.GetCooldown(8404));
		Assert.Single(registry.Broadcasts);
	}

	[Fact]
	public async Task ExecuteCooldownFanoutAsync_MutationFailureMetadataStopsBeforeCooldownAndFanout()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var registry = new CapturingConnectionRegistry(sentCount: 0);
		var bridge = CreateBridge(owner, registry);

		var result = await bridge.ExecuteCooldownFanoutAsync(
			playerObjectId: 8405,
			CreateCallbackPlanWithMutation(playerObjectId: 8405, locId: 6505, currentKinah: 500),
			new WorldPosition(210010000, 10, 20, 30, 0),
			currentTimeMillis: 4_000);

		Assert.Equal(BindPointTeleportRuntimeCallbackExecutionStatus.StoppedNotEnoughKinah, result.Status);
		Assert.True(result.ShouldSendNotEnoughFee);
		Assert.False(result.ShouldEmitKinahInventoryUpdatePacket);
		Assert.Null(result.KinahItemUpdate);
		Assert.Null(result.KinahInventoryUpdateType);
		Assert.Null(owner.GetCooldown(8405));
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task ExecuteCooldownFanoutAsync_MutationSuccessCarriesInventoryUpdateMetadataBeforeFanout()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var registry = new CapturingConnectionRegistry(sentCount: 2);
		var bridge = CreateBridge(owner, registry);

		var result = await bridge.ExecuteCooldownFanoutAsync(
			playerObjectId: 8406,
			CreateCallbackPlanWithMutation(playerObjectId: 8406, locId: 6506, currentKinah: 2_000),
			new WorldPosition(210010000, 10, 20, 30, 0),
			currentTimeMillis: 5_000);

		Assert.Equal(BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast, result.Status);
		Assert.True(result.ShouldEmitKinahInventoryUpdatePacket);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, result.KinahInventoryUpdateType);
		Assert.NotNull(result.KinahItemUpdate);
		Assert.Equal(1_000, result.KinahItemUpdate.Count);
		Assert.True(result.StoredCooldownFact);
		Assert.True(result.BroadcastCooldown);
		Assert.NotNull(owner.GetCooldown(8406));
		Assert.Single(registry.Broadcasts);
	}

	private static BindPointTeleportRuntimeCallbackExecutionBridgeService CreateBridge(
		BindPointTeleportRuntimeStateOwner owner,
		IGameClientConnectionRegistry registry)
	{
		return new BindPointTeleportRuntimeCallbackExecutionBridgeService(
			owner,
			new BindPointTeleportRuntimeFanoutService(registry));
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		int playerObjectId,
		int locId,
		bool kinahContinues,
		bool playerIsAboutToDie = false)
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_000,
			currentKinah: kinahContinues ? 2_000 : 500);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId,
			locId,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			playerObjectId,
			SmBindPointTeleport.Cooldown(playerObjectId, locId, cooldownSeconds: 600));
		var movementPlan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			new BindPointTeleportDestinationFact(
				WorldId: 210010000,
				X: 100,
				Y: 200,
				Z: 300,
				Heading: 60,
				CurrentWorldId: 210010000,
				CurrentInstanceId: 1),
			playerIsDead: false,
			playerIsAboutToDie);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlanMissingRuntimeMetadata()
	{
		var failedKinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_000,
			currentKinah: 500);
		var successKinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_000,
			currentKinah: 2_000);
		var failedPlan = BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			failedKinahPlan,
			BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(8402, 6502, currentTimeMillis: 1_000),
			BindPointTeleportFanoutPlanService.CreatePlan(
				BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
				sourcePlayerObjectId: 8402,
				SmBindPointTeleport.Cooldown(8402, 6502, cooldownSeconds: 600)),
			BindPointTeleportFinalMovementPlanService.CreatePlan(
				new BindPointTeleportDestinationFact(210010000, 1, 2, 3, 0, 210010000, 1),
				playerIsDead: false,
				playerIsAboutToDie: false));

		return failedPlan with
		{
			KinahPlan = successKinahPlan,
			ShouldSendNotEnoughFee = false,
			ShouldScheduleFinalTeleport = true,
		};
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlanWithMutation(
		int playerObjectId,
		int locId,
		long currentKinah)
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_000,
			currentKinah: 2_000);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(
			new Player
			{
				ObjectId = playerObjectId,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = 1824,
						OwnerId = playerObjectId,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice: 1_000);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId,
			locId,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			playerObjectId,
			SmBindPointTeleport.Cooldown(playerObjectId, locId, cooldownSeconds: 600));
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

	private static ThreadPoolManager CreateThreadPoolManager()
	{
		return new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly int _sentCount;

		public CapturingConnectionRegistry(int sentCount)
		{
			_sentCount = sentCount;
		}

		public List<BroadcastRecord> Broadcasts { get; } = [];

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
			return Task.FromResult(false);
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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(_sentCount);
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

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
