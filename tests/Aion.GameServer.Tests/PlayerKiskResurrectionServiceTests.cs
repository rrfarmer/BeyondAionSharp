using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskResurrectionServiceTests
{
	[Fact]
	public void UseResurrectionMatchesJavaKiskResurrectionUsedChargeDecrement()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			maxResurrects: 2);

		var result = PlayerKiskResurrectionService.UseResurrection(kisk);

		Assert.Equal(PlayerKiskResurrectionUseStatus.Used, result.Status);
		Assert.Equal(2, result.PreviousRemainingResurrections);
		Assert.Equal(1, result.RemainingResurrections);
		Assert.True(result.ShouldBroadcastUpdate);
		Assert.False(result.ShouldDeleteKisk);
		Assert.Equal(1, kisk.RemainingResurrects);
	}

	[Fact]
	public void UseResurrectionRequestsKiskDeleteWhenLastChargeIsUsed()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9002,
			ownerObjectId: 1001,
			npcId: 700273,
			maxResurrects: 1);

		var result = PlayerKiskResurrectionService.UseResurrection(kisk);

		Assert.Equal(PlayerKiskResurrectionUseStatus.Depleted, result.Status);
		Assert.Equal(1, result.PreviousRemainingResurrections);
		Assert.Equal(0, result.RemainingResurrections);
		Assert.True(result.ShouldBroadcastUpdate);
		Assert.True(result.ShouldDeleteKisk);
		Assert.Equal(0, kisk.RemainingResurrects);
	}

	[Fact]
	public void UseResurrectionDoesNotMutateInactiveKisk()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9003,
			ownerObjectId: 1001,
			npcId: 700273,
			maxResurrects: 1);
		Assert.True(kisk.UseResurrection());

		var result = PlayerKiskResurrectionService.UseResurrection(kisk);

		Assert.Equal(PlayerKiskResurrectionUseStatus.NotActive, result.Status);
		Assert.Equal(0, result.PreviousRemainingResurrections);
		Assert.Equal(0, result.RemainingResurrections);
		Assert.False(result.ShouldBroadcastUpdate);
		Assert.False(result.ShouldDeleteKisk);
		Assert.Equal(0, kisk.RemainingResurrects);
	}
}
