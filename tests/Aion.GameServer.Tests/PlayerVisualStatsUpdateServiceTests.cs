using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerVisualStatsUpdateServiceTests
{
	[Fact]
	public async Task UpdateStatsVisuallyAsync_SendsStatsInfoToOwner()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6101);

		var result = await service.UpdateStatsVisuallyAsync(player);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsSent, result.Status);
		Assert.True(result.StatsPacketSent);
		Assert.NotNull(result.StatsPacket);
		Assert.Null(result.SpeedPacket);
		Assert.Equal(0, result.SpeedBroadcastCount);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.StatsPacket, delivery.Packet);
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_SendsStatsBeforeChangeSpeedBroadcast()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6102);
		var speedSnapshot = new PlayerVisualSpeedSnapshot(MovementSpeed: 6.75f, BaseAttackSpeed: 1500, CurrentAttackSpeed: 1200);

		var result = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, result.Status);
		Assert.True(result.StatsPacketSent);
		Assert.Same(speedSnapshot, result.SpeedSnapshot);
		Assert.NotNull(result.StatsPacket);
		Assert.NotNull(result.SpeedPacket);
		Assert.Equal(1, result.SpeedBroadcastCount);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.StatsPacket, packet),
			packet => Assert.Same(result.SpeedPacket, packet));
		var broadcast = Assert.Single(registry.Broadcasts);
		Assert.Equal(player.Position, broadcast.SourcePosition);
		Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
		Assert.True(broadcast.IncludeSourcePlayer);
		Assert.Same(result.SpeedPacket, broadcast.Packet);
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_ResolvesRideSpeedSnapshotWhenMissing()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6104);
		player.IsInRideMode = true;
		player.RideInfo = new PlayerRideInfo(NpcId: 9001, StartFp: 30, CostFp: 1, SprintSpeed: 12.0f, FlySpeed: 16.0f, MoveSpeed: 9.0f);

		var result = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, result.Status);
		Assert.True(result.StatsPacketSent);
		Assert.NotNull(result.SpeedSnapshot);
		Assert.Equal(9.0f, result.SpeedSnapshot.MovementSpeed);
		Assert.Equal(1500, result.SpeedSnapshot.BaseAttackSpeed);
		Assert.Equal(1500, result.SpeedSnapshot.CurrentAttackSpeed);
		Assert.NotNull(result.SpeedPacket);
		Assert.Equal(1, result.SpeedBroadcastCount);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.StatsPacket, packet),
			packet => Assert.Same(result.SpeedPacket, packet));
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_UsesRideSprintAndFlightSpeeds()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var sprintingPlayer = CreatePlayer(6105);
		sprintingPlayer.IsInRideMode = true;
		sprintingPlayer.IsInSprintMode = true;
		sprintingPlayer.RideInfo = new PlayerRideInfo(NpcId: 9001, StartFp: 30, CostFp: 1, SprintSpeed: 12.0f, FlySpeed: 16.0f, MoveSpeed: 9.0f);
		var flyingPlayer = CreatePlayer(6106);
		flyingPlayer.IsInRideMode = true;
		flyingPlayer.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		flyingPlayer.SetFlyState(PlayerFlyState.Flying);
		flyingPlayer.RideInfo = new PlayerRideInfo(NpcId: 9002, StartFp: 30, CostFp: 1, SprintSpeed: 12.0f, FlySpeed: 16.0f, MoveSpeed: 9.0f);

		var sprint = await service.UpdateStatsAndSpeedVisuallyAsync(sprintingPlayer, speedSnapshot: null);
		var flight = await service.UpdateStatsAndSpeedVisuallyAsync(flyingPlayer, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, sprint.Status);
		Assert.NotNull(sprint.SpeedSnapshot);
		Assert.Equal(12.0f, sprint.SpeedSnapshot.MovementSpeed);
		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, flight.Status);
		Assert.NotNull(flight.SpeedSnapshot);
		Assert.Equal(16.0f, flight.SpeedSnapshot.MovementSpeed);
		Assert.Equal(2, registry.Broadcasts.Count);
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_SkipsUnchangedResolvedSpeedAfterCacheWarm()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6107);
		player.IsInRideMode = true;
		player.RideInfo = new PlayerRideInfo(NpcId: 9001, StartFp: 30, CostFp: 1, SprintSpeed: 12.0f, FlySpeed: 16.0f, MoveSpeed: 9.0f);

		var first = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);
		var second = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, first.Status);
		Assert.Equal(PlayerVisualStatsUpdateStatus.SpeedUnchanged, second.Status);
		Assert.True(second.StatsPacketSent);
		Assert.NotNull(second.StatsPacket);
		Assert.NotNull(second.SpeedSnapshot);
		Assert.Null(second.SpeedPacket);
		Assert.Equal(0, second.SpeedBroadcastCount);
		Assert.Single(registry.Broadcasts);
		Assert.Equal(2, registry.SentPackets.Count);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(first.StatsPacket, packet),
			packet => Assert.Same(first.SpeedPacket, packet),
			packet => Assert.Same(second.StatsPacket, packet));
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_ResolvesClassRunSpeedWhenMissing()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6103);

		var result = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, result.Status);
		Assert.True(result.StatsPacketSent);
		Assert.NotNull(result.StatsPacket);
		Assert.NotNull(result.SpeedSnapshot);
		Assert.Equal(6.0f, result.SpeedSnapshot.MovementSpeed);
		Assert.NotNull(result.SpeedPacket);
		Assert.Equal(1, result.SpeedBroadcastCount);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.StatsPacket, packet),
			packet => Assert.Same(result.SpeedPacket, packet));
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Same(result.StatsPacket, delivery.Packet);
		Assert.Single(registry.Broadcasts);
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_UsesClassWalkAndFlySpeeds()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var walkingPlayer = CreatePlayer(6108);
		walkingPlayer.SetCreatureState(PlayerCreatureState.WalkMode, enabled: true);
		var flyingPlayer = CreatePlayer(6109);
		flyingPlayer.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		flyingPlayer.SetFlyState(PlayerFlyState.Flying);

		var walk = await service.UpdateStatsAndSpeedVisuallyAsync(walkingPlayer, speedSnapshot: null);
		var fly = await service.UpdateStatsAndSpeedVisuallyAsync(flyingPlayer, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, walk.Status);
		Assert.NotNull(walk.SpeedSnapshot);
		Assert.Equal(1.5f, walk.SpeedSnapshot.MovementSpeed);
		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, fly.Status);
		Assert.NotNull(fly.SpeedSnapshot);
		Assert.Equal(9.0f, fly.SpeedSnapshot.MovementSpeed);
		Assert.Equal(2, registry.Broadcasts.Count);
	}

	[Fact]
	public async Task UpdateStatsAndSpeedVisuallyAsync_UsesCreatureFlyingFallbackWhenFlyStateMissing()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6110);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);

		var result = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, result.Status);
		Assert.NotNull(result.SpeedSnapshot);
		Assert.Equal(12.0f, result.SpeedSnapshot.MovementSpeed);
		Assert.NotNull(result.SpeedPacket);
	}

	[Fact]
	public async Task LevelReadyFlightNotifier_RestartsFlyingAndBroadcastsFlyEmotion()
	{
		var registry = new CapturingConnectionRegistry();
		var visualStats = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6111);
		player.SetFlyState(PlayerFlyState.Flying);

		var result = await PlayerLevelReadyFlightNotifier.NotifyIfFlyingAsync(player, registry, visualStats);

		Assert.True(result.WasFlying);
		Assert.True(player.IsInFlyingState());
		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.True(player.IsFpReduceActive);
		Assert.NotNull(result.VisualStatsUpdate);
		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, result.VisualStatsUpdate.Status);
		Assert.True(result.VisualStatsUpdate.StatsPacketSent);
		Assert.NotNull(result.VisualStatsUpdate.SpeedPacket);
		Assert.NotNull(result.Packet);
		Assert.Equal(1, result.BroadcastCount);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.VisualStatsUpdate.StatsPacket, packet),
			packet => Assert.Same(result.VisualStatsUpdate.SpeedPacket, packet),
			packet => Assert.Same(result.Packet, packet));
		Assert.Equal(2, registry.Broadcasts.Count);
		var flyBroadcast = registry.Broadcasts[1];
		var flyPacket = Assert.IsType<SmEmotion>(flyBroadcast.Packet);
		Assert.Same(result.Packet, flyPacket);
		Assert.Equal(player.Position, flyBroadcast.SourcePosition);
		Assert.Equal(player.ObjectId, flyBroadcast.SourceObjectId);
		Assert.True(flyBroadcast.IncludeSourcePlayer);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(result.Packet));
		Assert.Equal(player.ObjectId, reader.ReadD());
		Assert.Equal((int)EmotionType.Fly, (int)reader.ReadC());
		Assert.Equal((int)PlayerCreatureState.Flying, reader.ReadH());
		Assert.Equal(0, reader.ReadF());
		Assert.Equal(0, reader.Remaining);

		var skipped = await PlayerLevelReadyFlightNotifier.NotifyIfFlyingAsync(CreatePlayer(6112), registry);
		Assert.False(skipped.WasFlying);
		Assert.Null(skipped.Packet);
		Assert.Equal(0, skipped.BroadcastCount);
		Assert.Null(skipped.VisualStatsUpdate);
		Assert.Equal(2, registry.Broadcasts.Count);
	}

	[Fact]
	public async Task UpdateStatsVisuallyAsync_ReportsMissingPlayer()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);

		var result = await service.UpdateStatsVisuallyAsync(null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.MissingPlayer, result.Status);
		Assert.False(result.StatsPacketSent);
		Assert.Null(result.StatsPacket);
		Assert.Empty(registry.SentPackets);
		Assert.Empty(registry.Broadcasts);
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 205, CurrentFp: 55),
		};
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
		public List<PacketDelivery> SentPackets { get; } = [];

		public List<BroadcastRecord> Broadcasts { get; } = [];

		public List<GameServerPacket> PacketOrder { get; } = [];

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
			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			PacketOrder.Add(packet);
			return Task.FromResult(true);
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
			PacketOrder.Add(packet);
			return Task.FromResult(1);
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

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
