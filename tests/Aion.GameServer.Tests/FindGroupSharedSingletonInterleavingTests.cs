using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupSharedSingletonInterleavingTests
{
	[Fact]
	public void LogoutBeforeJoinedTeamDoesNotRecreateClientActionState()
	{
		var service = new FindGroupRecruitmentPlanService();
		var planner = new FindGroupClientActionPlanService(service);
		var player = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		var team = CreateTeamSubject(0x01020309, "Leader", size: 6, minLevel: 60, maxLevel: 65);

		planner.Plan(player, new FindGroupClientAction(Action: 2, Message: "Solo", GroupType: 4), nowEpochSeconds: 100);
		planner.Plan(
			player,
			new FindGroupClientAction(Action: 6, Message: "Apply", GroupType: 2, ClassId: 5, Level: 65),
			nowEpochSeconds: 101);
		planner.Plan(
			player,
			new FindGroupClientAction(Action: 8, Message: "Entry", InstanceMaskId: 0x11223344, MinMembers: 6),
			nowEpochSeconds: 102);

		var logout = service.OnLogout(player);
		WithTeam(player, PlayerTeamMembership.Group, team.ObjectId);
		var joined = service.OnJoinedTeam(player, team, isLeader: true, isFull: true, nowEpochSeconds: 200, serverId: 5);

		Assert.NotNull(logout.RemovedRecruitment);
		Assert.NotNull(logout.RemovedApplication);
		Assert.NotNull(logout.RemovedInstanceGroup);
		Assert.Equal(FindGroupApplicationPlanStatus.Missing, joined.ApplicationRemoval.Status);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, joined.SoloRecruitmentRemoval.Status);
		Assert.Null(joined.TeamRecruitmentAdd);
		Assert.NotNull(joined.FullTeamRecruitmentRemoval);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, joined.FullTeamRecruitmentRemoval!.Status);
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 300).Recruitments);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 301).Applications);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 302).InstanceGroups);
	}

	[Fact]
	public void JoinedTeamBeforeLogoutLeavesTeamRecruitmentForDisbandCleanup()
	{
		var service = new FindGroupRecruitmentPlanService();
		var planner = new FindGroupClientActionPlanService(service);
		var player = CreatePlayer(0x01020304, "Leader", "ELYOS", "RANGER", 65);
		var team = CreateTeamSubject(0x01020309, "Leader", size: 2, minLevel: 60, maxLevel: 65);

		planner.Plan(player, new FindGroupClientAction(Action: 2, Message: "Solo leader post", GroupType: 4), nowEpochSeconds: 100);
		planner.Plan(
			player,
			new FindGroupClientAction(Action: 6, Message: "Apply", GroupType: 2, ClassId: 5, Level: 65),
			nowEpochSeconds: 101);
		planner.Plan(
			player,
			new FindGroupClientAction(Action: 8, Message: "Entry", InstanceMaskId: 0x11223344, MinMembers: 3),
			nowEpochSeconds: 102);
		WithTeam(player, PlayerTeamMembership.Group, team.ObjectId);

		var joined = service.OnJoinedTeam(player, team, isLeader: true, isFull: false, nowEpochSeconds: 200, serverId: 5);
		var logout = service.OnLogout(player);
		var disbandRemoval = service.RemoveRecruitment(team.ObjectId, serverId: 5, unknown1: 0, unknown2: 0, unknown3: 0);

		Assert.False(joined.InstanceGroupRemoval.ShouldRemove);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, joined.ApplicationRemoval.Status);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, joined.SoloRecruitmentRemoval.Status);
		Assert.NotNull(joined.TeamRecruitmentAdd);
		Assert.Equal(team.ObjectId, joined.TeamRecruitmentAdd!.CurrentRecruitment?.ObjectId);
		Assert.Null(joined.FullTeamRecruitmentRemoval);
		Assert.Null(logout.RemovedRecruitment);
		Assert.Null(logout.RemovedApplication);
		Assert.NotNull(logout.RemovedInstanceGroup);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, disbandRemoval.Status);
		Assert.Equal(team.ObjectId, disbandRemoval.RemovedRecruitment?.ObjectId);
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 300).Recruitments);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 301).Applications);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 302).InstanceGroups);
	}

	private static Player CreatePlayer(int objectId, string name, string race, string playerClass, int level)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = playerClass,
			Level = level,
		};
	}

	private static void WithTeam(Player player, PlayerTeamMembership membership, int teamId)
	{
		player.TeamMembership = membership;
		player.CurrentTeamId = teamId;
	}

	private static FindGroupRecruitmentSubject CreateTeamSubject(int objectId, string recruiterName, int size, int minLevel, int maxLevel)
	{
		return new FindGroupRecruitmentSubject(
			objectId,
			"ELYOS",
			IsSoloPlayer: false,
			recruiterName,
			size,
			minLevel,
			maxLevel,
			ClassId: 5);
	}
}
