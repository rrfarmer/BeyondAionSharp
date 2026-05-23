using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PvpDpRewardServiceTests
{
	[Fact]
	public async Task ApplyMemberDpRewardAsync_CalculatesRatesAndAddsDpThroughPacketedBoundary()
	{
		var service = CreateService(out var registry);
		var member = CreatePlayer(objectId: 1500, playerClass: "RANGER", level: 18, rank: 3, dp: 100);
		var victim = CreatePlayer(objectId: 2500, playerClass: "ASSASSIN", level: 20, rank: 5, dp: 0);

		var result = await service.ApplyMemberDpRewardAsync(
			member,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true,
			maxDp: 4000,
			dpPvpRate: 1.5f);

		Assert.Equal(PvpDpRewardStatus.Applied, result.Status);
		Assert.Equal(member.ObjectId, result.ObjectId);
		Assert.Equal(victim.ObjectId, result.VictimObjectId);
		Assert.Equal(942, result.BaseRewardDp);
		Assert.Equal(236, result.RewardPerMember);
		Assert.Equal(360, result.MemberDpGain);
		Assert.Equal(100, result.PreviousDp);
		Assert.Equal(460, result.CurrentDp);
		Assert.Equal(460, member.Dp);
		Assert.True(result.UnderDailyKillLimit);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(360, result.Change.AppliedValue);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate?.StatsPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate.SpeedPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Same(result.Change.DpInfoPacket, registry.Broadcasts[0].Packet);
		Assert.Same(result.Change.VisualStatsUpdate.SpeedPacket, registry.Broadcasts[1].Packet);
		Assert.Collection(
			registry.SentPackets,
			delivery =>
			{
				Assert.Equal(member.ObjectId, delivery.PlayerObjectId);
				Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, delivery.Packet);
			},
			delivery =>
			{
				Assert.Equal(member.ObjectId, delivery.PlayerObjectId);
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
	public async Task ApplyMemberDpRewardAsync_DailyCapStillAddsJavaMinimumDp()
	{
		var service = CreateService(out var registry);
		var member = CreatePlayer(objectId: 1501, playerClass: "RANGER", level: 18, rank: 3, dp: 100);
		var victim = CreatePlayer(objectId: 2501, playerClass: "ASSASSIN", level: 20, rank: 5, dp: 0);

		var result = await service.ApplyMemberDpRewardAsync(
			member,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: false,
			maxDp: 4000);

		Assert.Equal(PvpDpRewardStatus.Applied, result.Status);
		Assert.Equal(942, result.BaseRewardDp);
		Assert.Equal(236, result.RewardPerMember);
		Assert.Equal(1, result.MemberDpGain);
		Assert.False(result.UnderDailyKillLimit);
		Assert.Equal(101, member.Dp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(1, result.Change.AppliedValue);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Equal(2, registry.SentPackets.Count);
	}

	[Theory]
	[InlineData(1000, 10, 20, 0)]
	[InlineData(1000, 10, 12, 800)]
	[InlineData(1000, 20, 10, 1100)]
	[InlineData(1000, 20, 18, 1020)]
	public void AdjustPvpDpGained_MatchesJavaLevelPenalty(
		int points,
		int defeatedLevel,
		int killerLevel,
		int expected)
	{
		var adjusted = PvpDpRewardService.AdjustPvpDpGained(points, defeatedLevel, killerLevel);

		Assert.Equal(expected, adjusted);
	}

	[Fact]
	public void CalculatePvpDpGainedAndMemberShare_MatchJavaFormulas()
	{
		var baseReward = PvpDpRewardService.CalculatePvpDpGained(
			victimLevel: 20,
			victimRank: 5,
			maxRank: 3,
			maxLevel: 22);
		var rewardPerMember = PvpDpRewardService.CalculateRewardPerMember(
			baseReward,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2);
		var zeroRewardGain = PvpDpRewardService.CalculateMemberDpGain(
			rewardPerMember: 0,
			victimLevel: 20,
			memberLevel: 18,
			underDailyKillLimit: true);

		Assert.Equal(942, baseReward);
		Assert.Equal(236, rewardPerMember);
		Assert.Equal(1, zeroRewardGain);
	}

	[Fact]
	public async Task ApplyMemberDpRewardAsync_SkipsMissingInputsAndUsesOnlineMaxDp()
	{
		var service = CreateService(out var registry);
		var member = CreatePlayer(objectId: 1502, playerClass: "RANGER", level: 18, rank: 3, dp: 100);
		var victim = CreatePlayer(objectId: 2502, playerClass: "ASSASSIN", level: 20, rank: 5, dp: 0);
		var startingClassMember = CreatePlayer(objectId: 1503, playerClass: "WARRIOR", level: 18, rank: 3, dp: 100);

		var missingMember = await service.ApplyMemberDpRewardAsync(
			null,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true,
			maxDp: 4000);
		var missingVictim = await service.ApplyMemberDpRewardAsync(
			member,
			null,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true,
			maxDp: 4000);
		var noEligibleMembers = await service.ApplyMemberDpRewardAsync(
			member,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 0,
			underDailyKillLimit: true,
			maxDp: 4000);
		var liveMax = await service.ApplyMemberDpRewardAsync(
			member,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true);
		var startingClass = await service.ApplyMemberDpRewardAsync(
			startingClassMember,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true,
			maxDp: 4000);

		Assert.Equal(PvpDpRewardStatus.MissingMember, missingMember.Status);
		Assert.Equal(PvpDpRewardStatus.MissingVictim, missingVictim.Status);
		Assert.Equal(PvpDpRewardStatus.NoEligibleMembers, noEligibleMembers.Status);
		Assert.Equal(PvpDpRewardStatus.Applied, liveMax.Status);
		Assert.NotNull(liveMax.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, liveMax.Change.Status);
		Assert.Equal(4000, liveMax.Change.MaxValue);
		Assert.Equal(240, liveMax.MemberDpGain);
		Assert.Equal(PvpDpRewardStatus.DpBoundarySkipped, startingClass.Status);
		Assert.NotNull(startingClass.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.StartingClass, startingClass.Change.Status);
		Assert.Equal(340, member.Dp);
		Assert.Equal(100, startingClassMember.Dp);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Equal(2, registry.SentPackets.Count);
	}

	private static PvpDpRewardService CreateService(out CapturingConnectionRegistry registry)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new PvpDpRewardService(resourceStats);
	}

	private static Player CreatePlayer(int objectId, string playerClass, int level, int rank, int dp)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = playerClass,
			Level = level,
			Dp = dp,
			IsOnline = true,
			AbyssRank = PlayerAbyssRank.Default() with { Rank = rank, MaxRank = rank },
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
