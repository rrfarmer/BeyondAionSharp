using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreClosePlanServiceTests
{
	[Fact]
	public void CreatePlan_WithOpenStoreCreatesCloseEmotionBroadcast()
	{
		var plan = PrivateStoreClosePlanService.CreatePlan(playerObjectId: 9001, playerCreatureState: 0, storeIsOpen: true);

		Assert.Equal(PrivateStoreClosePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldBroadcastEmotion);
		Assert.NotNull(plan.ClosePrivateShopEmotionPacket);
		Assert.Contains("CLOSE_PRIVATESHOP", plan.JavaSource);
		Assert.Contains("broadcastPacket", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_NoStoreSkipsEmotionBroadcast()
	{
		// Java: if player.getStore() == null -> return immediately
		var plan = PrivateStoreClosePlanService.CreatePlan(playerObjectId: 9001, playerCreatureState: 0, storeIsOpen: false);

		Assert.Equal(PrivateStoreClosePlanStatus.SkippedNoStoreOpen, plan.Status);
		Assert.False(plan.ShouldBroadcastEmotion);
		Assert.Null(plan.ClosePrivateShopEmotionPacket);
		Assert.Contains("getStore() == null", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_RecordsLiveSideEffectsAsDeferred()
	{
		var plan = PrivateStoreClosePlanService.CreatePlan(playerObjectId: 9001, playerCreatureState: 0, storeIsOpen: true);

		Assert.Contains("setStore(null)", plan.JavaSource);
		Assert.Contains("deferred", plan.JavaSource);
		Assert.Contains("setState(ACTIVE)", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_EmotionPacketIsTypeSmEmotion()
	{
		var plan = PrivateStoreClosePlanService.CreatePlan(playerObjectId: 9001, playerCreatureState: 0, storeIsOpen: true);

		Assert.IsType<SmEmotion>(plan.ClosePrivateShopEmotionPacket);
	}
}
