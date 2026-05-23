using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskAuthorizationServiceTests
{
	[Fact]
	public void ValidateBindMatchesJavaUseMaskRaceLegionSoloAndCapacitySlice()
	{
		var owner = new Player { ObjectId = 1001, Race = "ELYOS", LegionId = 77 };
		var sameRace = new Player { ObjectId = 1002, Race = "ELYOS" };
		var otherRace = new Player { ObjectId = 1003, Race = "ASMODIANS" };
		var legionMate = new Player { ObjectId = 1004, Race = "ELYOS", LegionId = 77 };

		var raceKisk = CreateKisk(useMask: 1, ownerRace: "ELYOS");
		var legionKisk = CreateKisk(useMask: 2, ownerLegionId: 77);
		var soloKisk = CreateKisk(useMask: 3);
		var unrestrictedKisk = CreateKisk(useMask: 0);
		var fullKisk = CreateKisk(useMask: 0, maxMembers: 1);
		Assert.True(fullKisk.AddMember(2000));

		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, PlayerKiskAuthorizationService.ValidateBind(sameRace, raceKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.NoAuthority, PlayerKiskAuthorizationService.ValidateBind(otherRace, raceKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, PlayerKiskAuthorizationService.ValidateBind(legionMate, legionKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, PlayerKiskAuthorizationService.ValidateBind(owner, soloKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.NoAuthority, PlayerKiskAuthorizationService.ValidateBind(sameRace, soloKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, PlayerKiskAuthorizationService.ValidateBind(otherRace, unrestrictedKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Full, PlayerKiskAuthorizationService.ValidateBind(owner, fullKisk).Status);
	}

	[Fact]
	public void ValidateBindKeepsGroupAndAllianceMasksOwnerOnlyUntilTeamMembershipCanBeResolved()
	{
		var owner = new Player { ObjectId = 1001, TeamMembership = PlayerTeamMembership.Group };
		var groupedPlayer = new Player { ObjectId = 1002, TeamMembership = PlayerTeamMembership.Group };
		var groupKisk = CreateKisk(useMask: 4);
		var allianceKisk = CreateKisk(useMask: 5);

		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, PlayerKiskAuthorizationService.ValidateBind(owner, groupKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.NoAuthority, PlayerKiskAuthorizationService.ValidateBind(groupedPlayer, groupKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, PlayerKiskAuthorizationService.ValidateBind(owner, allianceKisk).Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.NoAuthority, PlayerKiskAuthorizationService.ValidateBind(groupedPlayer, allianceKisk).Status);
	}

	private static PlayerKiskRuntimeState CreateKisk(
		int useMask,
		int maxMembers = 6,
		string ownerRace = "ELYOS",
		int ownerLegionId = 0)
	{
		return new PlayerKiskRuntimeState(
			objectId: 9001 + useMask,
			ownerObjectId: 1001,
			npcId: 700273,
			useMask: useMask,
			maxMembers: maxMembers,
			maxResurrects: 18,
			ownerRace: ownerRace,
			ownerLegionId: ownerLegionId);
	}
}
