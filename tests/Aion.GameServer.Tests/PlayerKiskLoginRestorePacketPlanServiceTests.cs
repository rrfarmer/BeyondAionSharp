using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskLoginRestorePacketPlanServiceTests
{
	[Fact]
	public void CreatePlanOrdersRestoredKiskUpdateBeforeBindPointsLikeJavaEnterWorld()
	{
		var player = CreatePlayer();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273);
		var restored = PlayerKiskOfflineBindingRestoreResult.RestoredExisting(kisk);
		var kiskPosition = new WorldPosition(210010000, 11.5f, 22.5f, 33.5f, 9);

		var plan = PlayerKiskLoginRestorePacketPlanService.CreatePlan(player, restored, kiskPosition, staticData: null);

		Assert.False(plan.ShouldBroadcastAddedMemberUpdate);
		Assert.Same(kisk, plan.RestoredKisk);
		Assert.Equal(kiskPosition, plan.RestoredKiskPosition);
		Assert.Collection(
			plan.DirectPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmBindPointInfo>(packet),
			packet => Assert.IsType<SmBindPointInfo>(packet));
	}

	[Fact]
	public void CreatePlanBroadcastsAddedMemberAfterDirectJavaLoginPackets()
	{
		var player = CreatePlayer();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273);
		var restored = PlayerKiskOfflineBindingRestoreResult.RestoredAdded(kisk);
		var kiskPosition = new WorldPosition(210010000, 11.5f, 22.5f, 33.5f, 9);

		var plan = PlayerKiskLoginRestorePacketPlanService.CreatePlan(player, restored, kiskPosition, staticData: null);

		Assert.True(plan.ShouldBroadcastAddedMemberUpdate);
		Assert.Collection(
			plan.DirectPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmBindPointInfo>(packet),
			packet => Assert.IsType<SmBindPointInfo>(packet));
	}

	[Fact]
	public void CreatePlanSendsOnlyObeliskBindPointWhenNoKiskWasRestored()
	{
		var player = CreatePlayer();

		var plan = PlayerKiskLoginRestorePacketPlanService.CreatePlan(
			player,
			restoredKiskBinding: null,
			restoredKiskPosition: null,
			staticData: null);

		Assert.False(plan.ShouldBroadcastAddedMemberUpdate);
		Assert.Null(plan.RestoredKisk);
		Assert.Null(plan.RestoredKiskPosition);
		var packet = Assert.Single(plan.DirectPackets);
		Assert.IsType<SmBindPointInfo>(packet);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1002,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}
}
