using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class AturamSkyFortressApRewardServiceTests
{
	[Fact]
	public void ApplyGeneratorApReward_AddsFixedJavaRewardToMostDamagePlayer()
	{
		var service = new AturamSkyFortressApRewardService();
		var player = CreatePlayer(objectId: 1700, currentAp: 1_000);

		var result = service.ApplyGeneratorApReward(player, AturamSkyFortressApRewardService.RewardNpcId);

		Assert.Equal(AturamSkyFortressApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(AturamSkyFortressApRewardService.RewardNpcId, result.NpcId);
		Assert.Equal(540, result.RewardAp);
		Assert.Equal(1_000, result.PreviousAp);
		Assert.Equal(1_540, result.CurrentAp);
		Assert.Equal(1_540, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(540, result.AbyssPointsPlan.Added);
		Assert.Null(result.AbyssPointsPlan.SiegeCallback);
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
	public void ApplyGeneratorApReward_SkipsMissingMostDamagePlayer()
	{
		var service = new AturamSkyFortressApRewardService();

		var result = service.ApplyGeneratorApReward(null, AturamSkyFortressApRewardService.RewardNpcId);

		Assert.Equal(AturamSkyFortressApRewardStatus.NoMostDamagePlayer, result.Status);
		Assert.Equal(AturamSkyFortressApRewardService.RewardNpcId, result.NpcId);
		Assert.Equal(540, result.RewardAp);
		Assert.Null(result.AbyssPointsPlan);
	}

	[Fact]
	public void ApplyGeneratorApReward_SkipsNonRewardNpc()
	{
		var service = new AturamSkyFortressApRewardService();
		var player = CreatePlayer(objectId: 1701, currentAp: 2_000);

		var result = service.ApplyGeneratorApReward(player, npcId: 217371);

		Assert.Equal(AturamSkyFortressApRewardStatus.NotRewardNpc, result.Status);
		Assert.Equal(217371, result.NpcId);
		Assert.Null(result.AbyssPointsPlan);
		Assert.Equal(2_000, player.AbyssRank.Ap);
	}

	private static Player CreatePlayer(int objectId, int currentAp)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 60,
			IsOnline = true,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = currentAp },
			Position = new WorldPosition(300240000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}
}
