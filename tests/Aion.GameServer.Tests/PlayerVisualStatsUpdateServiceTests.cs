using Aion.GameServer.Dataholders;
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
	public async Task UpdateStatsAndSpeedVisuallyAsync_ReportsMissingSpeedSnapshotAfterStatsSend()
	{
		var registry = new CapturingConnectionRegistry();
		var service = new PlayerVisualStatsUpdateService(registry);
		var player = CreatePlayer(6103);

		var result = await service.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);

		Assert.Equal(PlayerVisualStatsUpdateStatus.SpeedSnapshotMissing, result.Status);
		Assert.True(result.StatsPacketSent);
		Assert.NotNull(result.StatsPacket);
		Assert.Null(result.SpeedPacket);
		Assert.Equal(0, result.SpeedBroadcastCount);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Same(result.StatsPacket, delivery.Packet);
		Assert.Empty(registry.Broadcasts);
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
