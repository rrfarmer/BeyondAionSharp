using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class QuestRewardServiceTests
{
	[Fact]
	public async Task ApplyDpRewardAsync_AddsQuestDpThroughPacketedBoundary()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1300, playerClass: "RANGER", dp: 3500);

		var result = await service.ApplyDpRewardAsync(player, rewardDp: 600, maxDp: 4000);

		Assert.Equal(QuestDpRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(600, result.RewardDp);
		Assert.Equal(3500, result.PreviousDp);
		Assert.Equal(4000, result.CurrentDp);
		Assert.Equal(4000, player.Dp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(500, result.Change.AppliedValue);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate);
		Assert.NotNull(result.Change.VisualStatsUpdate.StatsPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate.SpeedPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Same(result.Change.DpInfoPacket, registry.Broadcasts[0].Packet);
		Assert.Same(result.Change.VisualStatsUpdate.SpeedPacket, registry.Broadcasts[1].Packet);
		Assert.Collection(
			registry.SentPackets,
			delivery =>
			{
				Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
				Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, delivery.Packet);
			},
			delivery =>
			{
				Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
				Assert.Same(result.Change.DpStatUpdatePacket, delivery.Packet);
			});
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.Change.DpInfoPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.SpeedPacket, packet),
			packet => Assert.Same(result.Change.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task ApplyDpRewardAsync_SkipsZeroDpRewardWithoutMutationOrPackets()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1301, playerClass: "RANGER", dp: 500);

		var result = await service.ApplyDpRewardAsync(player, rewardDp: 0, maxDp: 4000);

		Assert.Equal(QuestDpRewardStatus.NoDpReward, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(0, result.RewardDp);
		Assert.Equal(500, result.PreviousDp);
		Assert.Equal(500, result.CurrentDp);
		Assert.Equal(500, player.Dp);
		Assert.Null(result.Change);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ApplyDpRewardAsync_RequiresPlayerAndUsesOnlineMaxDp()
	{
		var service = CreateService(out var registry);
		var onlinePlayer = CreatePlayer(objectId: 1302, playerClass: "RANGER", dp: 500);

		var missingPlayer = await service.ApplyDpRewardAsync(player: null, rewardDp: 100, maxDp: 4000);
		var liveMax = await service.ApplyDpRewardAsync(onlinePlayer, rewardDp: 100);

		Assert.Equal(QuestDpRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(100, missingPlayer.RewardDp);
		Assert.Equal(QuestDpRewardStatus.Applied, liveMax.Status);
		Assert.NotNull(liveMax.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, liveMax.Change.Status);
		Assert.Equal(4000, liveMax.Change.MaxValue);
		Assert.Equal(600, onlinePlayer.Dp);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Equal(2, registry.SentPackets.Count);
	}

	[Fact]
	public async Task ApplyDpRewardAsync_PreservesStartingClassGuard()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1303, playerClass: "WARRIOR", dp: 500);

		var result = await service.ApplyDpRewardAsync(player, rewardDp: 100, maxDp: 4000);

		Assert.Equal(QuestDpRewardStatus.DpBoundarySkipped, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.StartingClass, result.Change.Status);
		Assert.Equal(500, player.Dp);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public void ApplyApReward_AppliesConfiguredQuestRateAndAddsApThroughPlanner()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApQuestRates = [1f, 1.75f],
				},
			});
		var player = CreatePlayer(objectId: 1304, playerClass: "RANGER", dp: 500, ap: 900, membership: 1);

		var result = service.ApplyApReward(player, rewardAp: 200);

		Assert.Equal(QuestApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(200, result.RewardAp);
		Assert.Equal(350, result.AppliedRewardAp);
		Assert.False(result.IsNonCountQuest);
		Assert.Equal(900, result.PreviousAp);
		Assert.Equal(1_250, result.CurrentAp);
		Assert.Equal(1_250, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(350, result.AbyssPointsPlan.Added);
		Assert.Collection(
			result.AbyssPointsPlan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1320000, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public void ApplyApReward_SkipsQuestRateForJavaNonCountCategory()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApQuestRates = [1f, 3f],
				},
			});
		var player = CreatePlayer(objectId: 1305, playerClass: "RANGER", dp: 500, ap: 100, membership: 1);

		var result = service.ApplyApReward(player, rewardAp: 200, isNonCountQuest: true);

		Assert.Equal(QuestApRewardStatus.Applied, result.Status);
		Assert.Equal(200, result.RewardAp);
		Assert.Equal(200, result.AppliedRewardAp);
		Assert.True(result.IsNonCountQuest);
		Assert.Equal(300, result.CurrentAp);
		Assert.Equal(300, player.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyApReward_SkipsMissingPlayerAndZeroApReward()
	{
		var service = CreateService(out _);
		var player = CreatePlayer(objectId: 1306, playerClass: "RANGER", dp: 500, ap: 700);

		var missingPlayer = service.ApplyApReward(null, rewardAp: 200);
		var zeroReward = service.ApplyApReward(player, rewardAp: 0);

		Assert.Equal(QuestApRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(QuestApRewardStatus.NoApReward, zeroReward.Status);
		Assert.Null(missingPlayer.AbyssPointsPlan);
		Assert.Null(zeroReward.AbyssPointsPlan);
		Assert.Equal(700, player.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyQuestApRate_MatchesJavaMembershipFallbacksAndOverflowBehavior()
	{
		var clampedMembership = QuestRewardService.ApplyQuestApRate(
			membershipLevel: 7,
			rewardAp: 200,
			apQuestRates: [1f, 1.5f]);
		var emptyRates = QuestRewardService.ApplyQuestApRate(
			membershipLevel: 7,
			rewardAp: 200,
			apQuestRates: []);
		var overflowFallback = QuestRewardService.ApplyQuestApRate(
			membershipLevel: 1,
			rewardAp: int.MaxValue,
			apQuestRates: [1f, 2f]);

		Assert.Equal(300, clampedMembership);
		Assert.Equal(200, emptyRates);
		Assert.Equal(int.MaxValue, overflowFallback);
	}

	private static QuestRewardService CreateService(
		out CapturingConnectionRegistry registry,
		GameServerOptions? options = null)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new QuestRewardService(resourceStats, options);
	}

	private static Player CreatePlayer(
		int objectId,
		string playerClass,
		int dp,
		int ap = 0,
		byte membership = 0)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = playerClass,
			Level = 10,
			Dp = dp,
			IsOnline = true,
			AccountMembership = membership,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = ap },
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

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
