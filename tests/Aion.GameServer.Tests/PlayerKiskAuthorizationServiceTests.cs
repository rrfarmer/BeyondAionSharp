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
	public void ValidateBindPrioritizesAlreadyRegisteredBeforeCapacityLikeJavaDialogGuard()
	{
		var boundPlayer = new Player { ObjectId = 1002, BoundKiskObjectId = 9001 };
		var memberOnlyPlayer = new Player { ObjectId = 1003 };
		var fullKisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			useMask: 0,
			maxMembers: 1);
		Assert.True(fullKisk.AddMember(memberOnlyPlayer.ObjectId));

		var boundDuplicate = PlayerKiskAuthorizationService.ValidateBind(boundPlayer, fullKisk);
		var memberDuplicate = PlayerKiskAuthorizationService.ValidateBind(memberOnlyPlayer, fullKisk);
		var unrelatedFull = PlayerKiskAuthorizationService.ValidateBind(new Player { ObjectId = 1004 }, fullKisk);

		Assert.Equal(PlayerKiskBindAuthorizationStatus.AlreadyRegistered, boundDuplicate.Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.AlreadyRegistered, memberDuplicate.Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Full, unrelatedFull.Status);
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

	[Fact]
	public void ValidateBindMatchesJavaGroupAndAllianceUseMasksWhenMembershipResolversAreAvailable()
	{
		var groupMember = new Player { ObjectId = 1002, TeamMembership = PlayerTeamMembership.Group };
		var allianceMember = new Player { ObjectId = 1003, TeamMembership = PlayerTeamMembership.Alliance };
		var soloPlayer = new Player { ObjectId = 1004 };
		var groupKisk = CreateKisk(useMask: 4);
		var allianceKisk = CreateKisk(useMask: 5);

		var allowedGroup = PlayerKiskAuthorizationService.ValidateBind(
			groupMember,
			groupKisk,
			hasCurrentGroupMember: (_, ownerObjectId) => ownerObjectId == groupKisk.OwnerObjectId);
		var deniedGroup = PlayerKiskAuthorizationService.ValidateBind(
			groupMember,
			groupKisk,
			hasCurrentGroupMember: (_, _) => false);
		var allowedAlliance = PlayerKiskAuthorizationService.ValidateBind(
			allianceMember,
			allianceKisk,
			hasCurrentTeamMember: (_, ownerObjectId) => ownerObjectId == allianceKisk.OwnerObjectId);
		var deniedAllianceWithoutTeamState = PlayerKiskAuthorizationService.ValidateBind(
			soloPlayer,
			allianceKisk,
			hasCurrentTeamMember: (_, _) => true);

		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, allowedGroup.Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.NoAuthority, deniedGroup.Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.Allowed, allowedAlliance.Status);
		Assert.Equal(PlayerKiskBindAuthorizationStatus.NoAuthority, deniedAllianceWithoutTeamState.Status);
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
