using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupJoinedTeamLifecycleRecorderTests
{
	[Fact]
	public void RecordGroupJoin_UsesRuntimeMembersAfterGroupMutationLikeJavaAddPlayerOrdering()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var runtime = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", "RANGER");
		var invited = CreatePlayer(1006, "Invited", "CLERIC");
		var members = new[]
		{
			leader,
			CreatePlayer(1002, "Member2", "RANGER"),
			CreatePlayer(1003, "Member3", "GLADIATOR"),
			CreatePlayer(1004, "Member4", "CLERIC"),
			CreatePlayer(1005, "Member5", "RANGER"),
			invited,
		};
		runtime.CreateOrUpdateGroup(7001, members);
		findGroupService.AddRecruitment(
			leader,
			"Team post",
			groupType: 7,
			nowEpochSeconds: 200,
			new FindGroupRecruitmentSubject(
				7001,
				"ELYOS",
				IsSoloPlayer: false,
				leader.Name,
				Size: 6,
				MinLevel: 45,
				MaxLevel: 65,
				ClassId: 5));
		var recorder = new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 333, serverId: 5);

		var plan = recorder.RecordGroupJoin(invited, runtime, teamId: 7001);

		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.SoloRecruitmentRemoval.Status);
		Assert.Null(plan.TeamRecruitmentAdd);
		Assert.NotNull(plan.FullTeamRecruitmentRemoval);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FullTeamRecruitmentRemoval!.Status);
		Assert.Equal(7001, plan.FullTeamRecruitmentRemoval.RemovedRecruitment?.ObjectId);
		Assert.NotNull(plan.FullTeamRecruitmentRemoval.WorldBroadcastIntent);
		Assert.Equal(
			"PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())",
			plan.FullTeamRecruitmentRemoval.WorldBroadcastIntent!.JavaSource);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
	}

	[Fact]
	public void RecordAllianceJoin_UsesRuntimeMembersAfterAllianceMutationLikeJavaAddPlayerOrdering()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(2001, "AllianceLeader", "RANGER");
		var invited = CreatePlayer(2024, "AllianceInvited", "CLERIC");
		runtime.CreateAlliance(9001, leader);
		for (var objectId = 2002; objectId <= 2023; objectId++)
			runtime.AddMember(9001, CreatePlayer(objectId, $"AllianceMember{objectId}", "GLADIATOR"));
		runtime.AddMember(9001, invited);
		findGroupService.AddRecruitment(
			leader,
			"Alliance post",
			groupType: 8,
			nowEpochSeconds: 200,
			new FindGroupRecruitmentSubject(
				9001,
				"ELYOS",
				IsSoloPlayer: false,
				leader.Name,
				Size: 24,
				MinLevel: 45,
				MaxLevel: 65,
				ClassId: 5));
		var recorder = new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 333, serverId: 5);

		var plan = recorder.RecordAllianceJoin(invited, runtime, allianceId: 9001);

		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.SoloRecruitmentRemoval.Status);
		Assert.Null(plan.TeamRecruitmentAdd);
		Assert.NotNull(plan.FullTeamRecruitmentRemoval);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FullTeamRecruitmentRemoval!.Status);
		Assert.Equal(9001, plan.FullTeamRecruitmentRemoval.RemovedRecruitment?.ObjectId);
		Assert.NotNull(plan.FullTeamRecruitmentRemoval.WorldBroadcastIntent);
		Assert.Equal(
			"PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())",
			plan.FullTeamRecruitmentRemoval.WorldBroadcastIntent!.JavaSource);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
	}

	private static Player CreatePlayer(int objectId, string name, string playerClass)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			Race = "ELYOS",
			PlayerClass = playerClass,
			Level = 65,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}
}
