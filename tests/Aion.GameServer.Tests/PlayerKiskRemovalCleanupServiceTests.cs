using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskRemovalCleanupServiceTests
{
	[Fact]
	public void CreatePlanMatchesJavaRemoveKiskCreatorMemberAndPendingCleanup()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273);
		Assert.True(kisk.AddMember(1002));
		Assert.True(kisk.AddMember(1003));
		var despawn = PlayerKiskDespawnResult.Removed(
			kisk,
			worldId: 210010000,
			kisk.CurrentMemberIds,
			releasedObjectId: true,
			cancelledDespawnTask: false);
		var players = new[]
		{
			CreatePlayer(1001),
			CreatePlayer(1002, boundKiskObjectId: 9001, pendingKiskObjectId: 9001),
			CreatePlayer(1003, currentHp: 0),
			CreatePlayer(1004, boundKiskObjectId: 9001, currentHp: 0),
			CreatePlayer(1005, pendingKiskObjectId: 9001),
			CreatePlayer(1006, boundKiskObjectId: 8001, pendingKiskObjectId: 8001),
		};

		var plan = PlayerKiskRemovalCleanupService.CreatePlan(despawn, players);

		Assert.Equal(1001, plan.CreatorUpdateObjectId);
		Assert.Equal(new[] { 1002, 1004 }, plan.ClearBoundObjectIds);
		Assert.Equal(new[] { 1002, 1005 }, plan.ClearPendingRequestObjectIds);
		Assert.Equal(new[] { 1002, 1003, 1004 }, plan.BindPointResetObjectIds);
		Assert.Equal(new[] { 1003, 1004 }, plan.ResurrectionOptionRefreshObjectIds);
	}

	[Fact]
	public void CreatePlanIncludesCreatorMemberBindPointResetLikeJavaRemoveKisk()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273);
		Assert.True(kisk.AddMember(1001));
		var despawn = PlayerKiskDespawnResult.Removed(
			kisk,
			worldId: 210010000,
			kisk.CurrentMemberIds,
			releasedObjectId: true,
			cancelledDespawnTask: false);
		var creator = CreatePlayer(1001, boundKiskObjectId: 9001);

		var plan = PlayerKiskRemovalCleanupService.CreatePlan(despawn, [creator]);

		Assert.Equal(1001, plan.CreatorUpdateObjectId);
		Assert.Equal(new[] { 1001 }, plan.ClearBoundObjectIds);
		Assert.Equal(new[] { 1001 }, plan.BindPointResetObjectIds);
		Assert.Empty(plan.ResurrectionOptionRefreshObjectIds);
	}

	[Fact]
	public void CreatePlanIsEmptyWhenNoKiskWasRemoved()
	{
		var player = CreatePlayer(1001, boundKiskObjectId: 9001, pendingKiskObjectId: 9001);
		var despawn = PlayerKiskDespawnResult.NotFound(9001);

		var plan = PlayerKiskRemovalCleanupService.CreatePlan(despawn, [player]);

		Assert.Null(plan.CreatorUpdateObjectId);
		Assert.Empty(plan.ClearBoundObjectIds);
		Assert.Empty(plan.ClearPendingRequestObjectIds);
		Assert.Empty(plan.BindPointResetObjectIds);
		Assert.Empty(plan.ResurrectionOptionRefreshObjectIds);
	}

	private static Player CreatePlayer(
		int objectId,
		int boundKiskObjectId = 0,
		int pendingKiskObjectId = 0,
		int currentHp = 100)
	{
		return new Player
		{
			ObjectId = objectId,
			BoundKiskObjectId = boundKiskObjectId,
			LifeStats = new PlayerLifeStats(currentHp, CurrentMp: 0, CurrentFp: 0),
			PendingKiskBindRequest = pendingKiskObjectId == 0
				? null
				: new PendingKiskBindRequest(pendingKiskObjectId, 160018),
		};
	}
}
