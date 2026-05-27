using Aion.GameServer.Services;
using Aion.GameServer.Model.GameObjects;

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

	[Fact]
	public void Apply_LiveAggroMutationClearsSuppliedPlayerOwnedAggroList()
	{
		var service = new PlayerReviveCleanupAdapterService();
		var player = new Player { ObjectId = PlayerObjectId };
		player.AggroList.TryAddKnownAttacker(2001, damage: 80, hate: 800, ownerKnownListKnowsAttacker: true);
		player.AggroList.TryAddKnownAttacker(2002, damage: 20, hate: 200, ownerKnownListKnowsAttacker: true);
		player.AggroList.MarkHateReductionTaskActiveForParity();
		var request = new PlayerReviveCleanupAdapterRequest(
			PlayerObjectId,
			PreReviveAggroEntries: [],
			ExecuteLiveAggroMutation: true,
			player.AggroList);

		var result = service.Apply(request);

		Assert.Equal(PlayerReviveCleanupAdapterStatus.LiveAggroCleared, result.Status);
		Assert.True(result.MutatedLiveAggro);
		Assert.True(result.IsLive);
		Assert.True(result.Plan.IsLive);
		Assert.True(result.Plan.AggroClearPlan.IsLive);
		Assert.Equal([2001, 2002], result.Plan.AggroClearPlan.ClearedEntries.Select(entry => entry.AttackerObjectId));
		Assert.Empty(player.AggroList.Entries);
		Assert.False(player.AggroList.HasHateReductionTask);
		Assert.Contains("player.getAggroList().clear()", result.JavaSource);
	}

	private const int PlayerObjectId = 1001;
}
