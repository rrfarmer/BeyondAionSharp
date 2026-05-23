using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerReviveTargetCleanupServiceTests
{
	[Fact]
	public void ClearKnownPlayerTargetsClearsOnlyPlayersTargetingRevivedPlayer()
	{
		var revived = CreatePlayer(1001);
		var targetingRevived = CreatePlayer(1002, targetObjectId: revived.ObjectId);
		var targetingOther = CreatePlayer(1003, targetObjectId: 3001);
		var noTarget = CreatePlayer(1004);

		var result = PlayerReviveTargetCleanupService.ClearKnownPlayerTargets(
			revived,
			new[] { revived, targetingRevived, targetingOther, noTarget });

		Assert.True(result.ClearedAny);
		Assert.Equal(new[] { targetingRevived.ObjectId }, result.ClearedTargetObjectIds);
		Assert.Equal(0, targetingRevived.TargetObjectId);
		Assert.Equal(3001, targetingOther.TargetObjectId);
		Assert.Equal(0, noTarget.TargetObjectId);
	}

	[Fact]
	public void ClearKnownPlayerTargetsDoesNotClearRevivedPlayerSelfTarget()
	{
		var revived = CreatePlayer(1001, targetObjectId: 1001);

		var result = PlayerReviveTargetCleanupService.ClearKnownPlayerTargets(revived, new[] { revived });

		Assert.False(result.ClearedAny);
		Assert.Empty(result.ClearedTargetObjectIds);
		Assert.Equal(revived.ObjectId, revived.TargetObjectId);
	}

	[Fact]
	public void ClearKnownPlayerTargetsHonorsKnownListVisibilityPredicate()
	{
		var revived = CreatePlayer(1001, position: new WorldPosition(210010000, 0, 0, 0, 0));
		var visibleTargetingPlayer = CreatePlayer(
			1002,
			targetObjectId: revived.ObjectId,
			position: new WorldPosition(210010000, 20, 0, 0, 0));
		var distantTargetingPlayer = CreatePlayer(
			1003,
			targetObjectId: revived.ObjectId,
			position: new WorldPosition(210010000, 200, 0, 0, 0));
		var otherWorldTargetingPlayer = CreatePlayer(
			1004,
			targetObjectId: revived.ObjectId,
			position: new WorldPosition(220010000, 0, 0, 0, 0));

		var result = PlayerReviveTargetCleanupService.ClearKnownPlayerTargets(
			revived,
			new[] { visibleTargetingPlayer, distantTargetingPlayer, otherWorldTargetingPlayer },
			(candidate, revivedPlayer) => WorldVisibility.IsVisibleTo(candidate, revivedPlayer.Position));

		Assert.Equal(new[] { visibleTargetingPlayer.ObjectId }, result.ClearedTargetObjectIds);
		Assert.Equal(0, visibleTargetingPlayer.TargetObjectId);
		Assert.Equal(revived.ObjectId, distantTargetingPlayer.TargetObjectId);
		Assert.Equal(revived.ObjectId, otherWorldTargetingPlayer.TargetObjectId);
	}

	private static Player CreatePlayer(
		int objectId,
		int targetObjectId = 0,
		WorldPosition? position = null)
	{
		return new Player
		{
			ObjectId = objectId,
			TargetObjectId = targetObjectId,
			Position = position ?? new WorldPosition(210010000, 0, 0, 0, 0),
		};
	}
}
