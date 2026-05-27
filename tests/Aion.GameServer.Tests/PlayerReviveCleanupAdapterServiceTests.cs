using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerReviveCleanupAdapterServiceTests
{
	[Fact]
	public void Apply_DisabledExposesKiskReviveCleanupPlanWithoutLiveAggroMutation()
	{
		var service = new PlayerReviveCleanupAdapterService();
		var request = new PlayerReviveCleanupAdapterRequest(
			PlayerObjectId,
			[
				new PlayerAggroEntrySnapshot(2001, Damage: 80, Hate: 800),
				new PlayerAggroEntrySnapshot(2002, Damage: 20, Hate: 200),
			]);

		var result = service.Apply(request);

		Assert.Equal(PlayerReviveCleanupAdapterStatus.DisabledPlanned, result.Status);
		Assert.False(result.MutatedLiveAggro);
		Assert.True(result.ExposesPlanForObservation);
		Assert.False(result.IsLive);
		Assert.Equal(PlayerObjectId, result.Plan.PlayerObjectId);
		Assert.Equal(PlayerAggroCleanupReason.Revive, result.Plan.AggroClearPlan.Reason);
		Assert.Contains(PlayerReviveCleanupPlanStep.ClearPlayerAggro, result.Plan.Steps);
		Assert.Contains("PlayerReviveService.kiskRevive", result.JavaSource);
	}

	[Fact]
	public void Apply_LiveAggroMutationRequestReportsMissingLivePlayerAggroList()
	{
		var service = new PlayerReviveCleanupAdapterService();
		var request = new PlayerReviveCleanupAdapterRequest(
			PlayerObjectId,
			[new PlayerAggroEntrySnapshot(2001, Damage: 80, Hate: 800)],
			ExecuteLiveAggroMutation: true);

		var result = service.Apply(request);

		Assert.Equal(PlayerReviveCleanupAdapterStatus.BlockedMissingLiveAggroList, result.Status);
		Assert.False(result.MutatedLiveAggro);
		Assert.True(result.ExposesPlanForObservation);
		Assert.Equal(PlayerObjectId, result.Plan.PlayerObjectId);
		Assert.Contains("PlayerAggroList", result.JavaSource);
		Assert.False(result.IsLive);
	}

	private const int PlayerObjectId = 1001;
}
