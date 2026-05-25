using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AbyssPointsServiceTests
{
	[Fact]
	public void AddAp_GainsPointsSendsRankAndLegionContributionLikeJava()
	{
		var player = CreatePlayer();
		player.LegionId = 77;

		var plan = AbyssPointsService.AddAp(
			player,
			amount: 1_500,
			new AbyssPointsAddOptions(CurrentLegionContributionPoints: 10_000));

		Assert.True(plan.Applied);
		Assert.Equal(0, plan.OldAp);
		Assert.Equal(1_500, plan.Added);
		Assert.Equal(1_500, player.AbyssRank.Ap);
		Assert.Equal(2, player.AbyssRank.Rank);
		Assert.True(plan.ShouldCheckRankLimitItems);
		Assert.True(plan.ShouldUpdateAbyssSkills);
		Assert.NotNull(plan.RankUpdatePacket);
		Assert.Collection(
			plan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1320000, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
		Assert.NotNull(plan.LegionContribution);
		Assert.Equal(77, plan.LegionContribution.LegionId);
		Assert.Equal(1_500, plan.LegionContribution.AddedContributionPoints);
		Assert.Equal(11_500, plan.LegionContribution.NewContributionPoints);
		AssertLegionContributionPacket(plan.LegionContribution.Packet, 11_500);
	}

	[Fact]
	public void AddAp_SpendingSendsUseMessageWithoutLegionContribution()
	{
		var player = CreatePlayer();
		player.LegionId = 77;
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 2_000, Rank = 2, MaxRank = 2 };

		var plan = AbyssPointsService.AddAp(player, amount: -500);

		Assert.True(plan.Applied);
		Assert.Equal(-500, plan.Added);
		Assert.Equal(1_500, player.AbyssRank.Ap);
		Assert.Equal(2, player.AbyssRank.Rank);
		Assert.False(plan.ShouldCheckRankLimitItems);
		Assert.False(plan.ShouldUpdateAbyssSkills);
		Assert.Null(plan.RankUpdatePacket);
		Assert.Null(plan.LegionContribution);
		Assert.Collection(
			plan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300965, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Theory]
	[InlineData(true, false, false, true)]
	[InlineData(false, true, false, true)]
	[InlineData(false, true, true, false)]
	[InlineData(false, false, false, false)]
	public void AddApFromObject_CreatesSiegeCallbackOnlyForPlayerOrNonPeaceSiegeNpc(
		bool sourceIsPlayer,
		bool sourceIsSiegeNpc,
		bool sourceSiegeNpcPeace,
		bool expectedCallback)
	{
		var player = CreatePlayer();

		var plan = AbyssPointsService.AddApFromObject(
			player,
			sourceObjectId: 9001,
			sourceIsPlayer,
			sourceIsSiegeNpc,
			sourceSiegeNpcPeace,
			amount: 200);

		Assert.Equal(expectedCallback, plan.SiegeCallback != null);
		if (expectedCallback)
		{
			Assert.Equal(player.ObjectId, plan.SiegeCallback!.PlayerObjectId);
			Assert.Equal(9001, plan.SiegeCallback.SourceObjectId);
			Assert.Equal(200, plan.SiegeCallback.AbyssPoints);
		}
	}

	[Fact]
	public void AddAp_NullPlayerDoesNothingLikeJava()
	{
		var plan = AbyssPointsService.AddAp(null, amount: 100);

		Assert.False(plan.Applied);
		Assert.Equal(AbyssPointsAddStatus.NoPlayer, plan.Status);
		Assert.Empty(plan.PlayerPackets);
		Assert.Null(plan.RankUpdatePacket);
		Assert.Null(plan.LegionContribution);
		Assert.Null(plan.SiegeCallback);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			AbyssRank = PlayerAbyssRank.Default(),
		};
	}

	private static void AssertLegionContributionPacket(SmLegionEdit packet, long expectedContributionPoints)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(0x03, (int)reader.ReadC());
		Assert.Equal(expectedContributionPoints, reader.ReadQ());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
