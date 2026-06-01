using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreNameOpenCompositionPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_OpenStoreComposesStoreNameBroadcast()
	{
		var packet = CreatePacket("For Atreia");

		var plan = PrivateStoreNameOpenCompositionPlanService.CreateDisabledPlan(
			packet,
			new PrivateStoreNameOpenCompositionContext(PlayerObjectId: 9001, StoreIsOpen: true));

		Assert.Equal(PrivateStoreNameOpenCompositionPlanStatus.OpenPlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.WouldSetStoreMessage);
		Assert.True(plan.WouldBroadcastStoreName);
		Assert.NotNull(plan.OpenPlan);
		Assert.Equal(PrivateStoreOpenPlanStatus.PlanCreated, plan.OpenPlan!.Status);
		Assert.Equal("For Atreia", plan.OpenPlan.StoreMessage);
		Assert.Contains("PrivateStoreService.openPrivateStore", plan.JavaSource);
	}

	[Fact]
	public void CreateDisabledPlan_EmptyStoreNameStillComposesBroadcastLikeJava()
	{
		var packet = CreatePacket(string.Empty);

		var plan = PrivateStoreNameOpenCompositionPlanService.CreateDisabledPlan(
			packet,
			new PrivateStoreNameOpenCompositionContext(PlayerObjectId: 9001, StoreIsOpen: true));

		Assert.Equal(PrivateStoreNameOpenCompositionPlanStatus.OpenPlanCreated, plan.Status);
		Assert.NotNull(plan.OpenPlan);
		Assert.Equal(string.Empty, plan.OpenPlan!.StoreMessage);
		Assert.True(plan.OpenPlan.ShouldBroadcastStoreName);
		Assert.NotNull(plan.OpenPlan.PrivateStoreNamePacket);
	}

	[Fact]
	public void CreateDisabledPlan_MissingStoreRecordsJavaPreconditionWithoutSideEffects()
	{
		var packet = CreatePacket("For Atreia");

		var plan = PrivateStoreNameOpenCompositionPlanService.CreateDisabledPlan(
			packet,
			new PrivateStoreNameOpenCompositionContext(PlayerObjectId: 9001, StoreIsOpen: false));

		Assert.Equal(PrivateStoreNameOpenCompositionPlanStatus.MissingStorePrecondition, plan.Status);
		Assert.Null(plan.OpenPlan);
		Assert.False(plan.WouldSetStoreMessage);
		Assert.False(plan.WouldBroadcastStoreName);
		Assert.Contains("activePlayer.getStore()", plan.JavaSource);
	}

	private static CmPrivateStoreName CreatePacket(string storeName)
	{
		var packet = new CmPrivateStoreName(120, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteS(storeName);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}
}
