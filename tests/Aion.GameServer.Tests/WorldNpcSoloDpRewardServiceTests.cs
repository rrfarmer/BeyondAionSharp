using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcSoloDpRewardServiceTests
{
	[Fact]
	public async Task ApplySoloDpRewardAsync_CalculatesScalesAndAddsDpThroughPacketedBoundary()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1400, playerClass: "RANGER", level: 10, dp: 100);
		var npc = CreateNpc(objectId: 2400, level: 12, rating: "ELITE");

		var result = await service.ApplySoloDpRewardAsync(player, npc, damagePercent: 0.5f, maxDp: 4000);

		Assert.Equal(WorldNpcSoloDpRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(npc.ObjectId, result.NpcObjectId);
		Assert.Equal(39, result.BaseRewardDp);
		Assert.Equal(19, result.RewardDp);
		Assert.Equal(100, result.PreviousDp);
		Assert.Equal(119, result.CurrentDp);
		Assert.Equal(119, player.Dp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(19, result.Change.AppliedValue);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate);
		Assert.NotNull(result.Change.VisualStatsUpdate.StatsPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Same(result.Change.DpInfoPacket, Assert.Single(registry.Broadcasts).Packet);
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
			packet => Assert.Same(result.Change.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task ApplySoloDpRewardAsync_AddsZeroScaledDpAndStillSendsJavaDpPackets()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1401, playerClass: "RANGER", level: 30, dp: 500);
		var npc = CreateNpc(objectId: 2401, level: 10, rating: "NORMAL");

		var result = await service.ApplySoloDpRewardAsync(player, npc, damagePercent: 0.75f, maxDp: 4000);

		Assert.Equal(WorldNpcSoloDpRewardStatus.Applied, result.Status);
		Assert.Equal(0, result.BaseRewardDp);
		Assert.Equal(0, result.RewardDp);
		Assert.Equal(500, result.PreviousDp);
		Assert.Equal(500, result.CurrentDp);
		Assert.Equal(500, player.Dp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.NoChange, result.Change.Status);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate?.StatsPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Single(registry.Broadcasts);
		Assert.Equal(2, registry.SentPackets.Count);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.Change.DpInfoPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, packet),
			packet => Assert.Same(result.Change.DpStatUpdatePacket, packet));
	}

	[Theory]
	[InlineData(10, 10, "JUNK", 20)]
	[InlineData(10, 12, "ELITE", 39)]
	[InlineData(15, 4, "HERO", 0)]
	[InlineData(1, 6, "LEGENDARY", 36)]
	public void CalculateDpReward_MatchesJavaRatingAndLevelTables(
		int playerLevel,
		int npcLevel,
		string rating,
		int expectedReward)
	{
		var npcTemplate = CreateTemplate(npcLevel, rating);

		var reward = WorldNpcSoloDpRewardService.CalculateDpReward(playerLevel, npcTemplate);

		Assert.Equal(expectedReward, reward);
	}

	[Fact]
	public void CalculateDpReward_AppliesJavaRateAndDamageTruncation()
	{
		var npcTemplate = CreateTemplate(level: 12, rating: "ELITE");

		var reward = WorldNpcSoloDpRewardService.CalculateDpReward(playerLevel: 10, npcTemplate, dpPveRate: 1.5f);
		var scaled = WorldNpcSoloDpRewardService.ScaleRewardByDamagePercent(reward, damagePercent: 0.5f);
		var nanScaled = WorldNpcSoloDpRewardService.ScaleRewardByDamagePercent(reward, float.NaN);

		Assert.Equal(58, reward);
		Assert.Equal(29, scaled);
		Assert.Equal(0, nanScaled);
	}

	[Fact]
	public async Task ApplySoloDpRewardAsync_SkipsMissingDeadAndBoundaryCases()
	{
		var service = CreateService(out var registry);
		var npc = CreateNpc(objectId: 2402, level: 12, rating: "ELITE");
		var player = CreatePlayer(objectId: 1402, playerClass: "RANGER", level: 10, dp: 500);
		var deadPlayer = CreatePlayer(objectId: 1403, playerClass: "RANGER", level: 10, dp: 500, currentHp: 0);
		var startingClassPlayer = CreatePlayer(objectId: 1404, playerClass: "WARRIOR", level: 10, dp: 500);

		var missingPlayer = await service.ApplySoloDpRewardAsync(null, npc, damagePercent: 1f, maxDp: 4000);
		var missingNpc = await service.ApplySoloDpRewardAsync(player, null, damagePercent: 1f, maxDp: 4000);
		var playerDead = await service.ApplySoloDpRewardAsync(deadPlayer, npc, damagePercent: 1f, maxDp: 4000);
		var missingMax = await service.ApplySoloDpRewardAsync(player, npc, damagePercent: 1f);
		var startingClass = await service.ApplySoloDpRewardAsync(startingClassPlayer, npc, damagePercent: 1f, maxDp: 4000);

		Assert.Equal(WorldNpcSoloDpRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(WorldNpcSoloDpRewardStatus.MissingNpc, missingNpc.Status);
		Assert.Equal(WorldNpcSoloDpRewardStatus.PlayerDead, playerDead.Status);
		Assert.Equal(WorldNpcSoloDpRewardStatus.DpBoundarySkipped, missingMax.Status);
		Assert.NotNull(missingMax.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.MissingMaxResource, missingMax.Change.Status);
		Assert.Equal(WorldNpcSoloDpRewardStatus.DpBoundarySkipped, startingClass.Status);
		Assert.NotNull(startingClass.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.StartingClass, startingClass.Change.Status);
		Assert.Equal(500, player.Dp);
		Assert.Equal(500, deadPlayer.Dp);
		Assert.Equal(500, startingClassPlayer.Dp);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	private static WorldNpcSoloDpRewardService CreateService(out CapturingConnectionRegistry registry)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new WorldNpcSoloDpRewardService(resourceStats);
	}

	private static Player CreatePlayer(
		int objectId,
		string playerClass,
		int level,
		int dp,
		int currentHp = 100)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = playerClass,
			Level = level,
			Dp = dp,
			IsOnline = true,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(currentHp, 100, 100),
		};
	}

	private static WorldNpc CreateNpc(int objectId, int level, string rating)
	{
		var template = CreateTemplate(level, rating);
		return new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			new WorldPosition(210010000, 15, 25, 30, 0));
	}

	private static NpcTemplateSummary CreateTemplate(int level, string rating)
	{
		return new NpcTemplateSummary(
			TemplateId: 7300 + level,
			Name: "Training Target",
			NameId: 7300 + level,
			Level: level,
			Rank: "NORMAL",
			Rating: rating,
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC");
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
