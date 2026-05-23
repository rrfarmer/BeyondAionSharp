using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskAttackabilityServiceTests
{
	[Fact]
	public void GetCreatureTypeMatchesJavaEnemyRaceAndPvpZoneRule()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			ownerRace: "ELYOS");
		var enemyPlayer = new Player { ObjectId = 1002, Race = "ASMODIANS" };
		var sameRacePlayer = new Player { ObjectId = 1003, Race = "ELYOS" };

		Assert.True(PlayerKiskAttackabilityService.IsEnemyFrom(
			enemyPlayer,
			kisk,
			kiskInsidePvpZone: true,
			playerInsidePvpZone: true));
		Assert.Equal(
			PlayerKiskCreatureType.Attackable,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskInsidePvpZone: true,
				playerInsidePvpZone: true));

		Assert.False(PlayerKiskAttackabilityService.IsEnemyFrom(
			sameRacePlayer,
			kisk,
			kiskInsidePvpZone: true,
			playerInsidePvpZone: true));
		Assert.Equal(
			PlayerKiskCreatureType.Support,
			PlayerKiskAttackabilityService.GetCreatureType(
				sameRacePlayer,
				kisk,
				kiskInsidePvpZone: true,
				playerInsidePvpZone: true));
	}

	[Fact]
	public void GetCreatureTypeRequiresBothPlayerAndKiskPvpZoneState()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			ownerRace: "ELYOS");
		var enemyPlayer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		Assert.Equal(
			PlayerKiskCreatureType.Support,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskInsidePvpZone: false,
				playerInsidePvpZone: true));
		Assert.Equal(
			PlayerKiskCreatureType.Support,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskInsidePvpZone: true,
				playerInsidePvpZone: false));
		Assert.Equal(
			PlayerKiskCreatureType.Support,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskInsidePvpZone: false,
				playerInsidePvpZone: false));
	}

	[Fact]
	public void GetCreatureTypeCanUseJavaCreaturePvpZoneCounters()
	{
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			ownerRace: "ELYOS");
		var enemyPlayer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		Assert.Equal(
			PlayerKiskCreatureType.Attackable,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskSiegeZoneCount: 0,
				kiskPvpZoneCount: 0,
				playerSiegeZoneCount: 0,
				playerPvpZoneCount: 0));
		Assert.Equal(
			PlayerKiskCreatureType.Support,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskSiegeZoneCount: 0,
				kiskPvpZoneCount: 1,
				playerSiegeZoneCount: 0,
				playerPvpZoneCount: 0));
		Assert.Equal(
			PlayerKiskCreatureType.Attackable,
			PlayerKiskAttackabilityService.GetCreatureType(
				enemyPlayer,
				kisk,
				kiskSiegeZoneCount: 0,
				kiskPvpZoneCount: 2,
				playerSiegeZoneCount: 1,
				playerPvpZoneCount: 1));
	}
}
