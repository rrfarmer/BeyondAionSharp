using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerAllianceRuntimeTests
{
	[Fact]
	public void CreateAlliance_AttachesLeaderToFirstJavaAllianceGroup()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);

		var snapshot = runtime.CreateAlliance(88001, leader, PlayerAllianceTeamType.AutoAlliance);

		Assert.Equal(88001, snapshot.AllianceId);
		Assert.Equal(1001, snapshot.LeaderObjectId);
		Assert.Equal([1001], snapshot.MemberObjectIds);
		Assert.Equal([1001], snapshot.MemberObjectIdsByGroupId[1000]);
		Assert.Empty(snapshot.MemberObjectIdsByGroupId[1001]);
		Assert.Empty(snapshot.MemberObjectIdsByGroupId[1002]);
		Assert.Empty(snapshot.MemberObjectIdsByGroupId[1003]);
		Assert.Equal(PlayerAllianceTeamType.AutoAlliance, snapshot.TeamType);
		Assert.Equal(PlayerTeamMembership.Alliance, leader.TeamMembership);
		Assert.Equal(88001, leader.CurrentTeamId);
		Assert.Equal([1001], leader.CurrentTeamMemberObjectIds);
		Assert.Same(snapshot, leader.CurrentAllianceSnapshot);
		Assert.Null(leader.CurrentGroupSnapshot);
		Assert.Same(snapshot, runtime.Resolve(leader));
		var descriptor = Assert.IsType<PlayerAllianceDescriptor>(runtime.GetDescriptor(88001));
		Assert.Equal(24, descriptor.MaxMemberCount);
		Assert.Equal(6, descriptor.MaxGroupMemberCount);
		Assert.Equal([1000, 1001, 1002, 1003], descriptor.AllianceGroupIds);
		Assert.True(runtime.HasMember(88001, 1001));
		Assert.True(runtime.IsLeader(88001, leader));
		Assert.False(runtime.IsFull(88001));
		var leaderMember = Assert.IsType<PlayerAllianceMember>(runtime.GetMember(88001, 1001));
		Assert.Equal(88001, leaderMember.AllianceId);
		Assert.Equal(1000, leaderMember.AllianceGroupId);
		Assert.Same(leader, leaderMember.Player);
	}

	[Fact]
	public void AddMember_FillsAllianceGroupsInJavaOrderAndCapsAtSixPerGroup()
	{
		var runtime = new PlayerAllianceRuntime();
		var members = Enumerable.Range(0, 24)
			.Select(index => CreatePlayer(1001 + index, $"Member{index}", worldId: 210010000 + index))
			.ToArray();
		runtime.CreateAlliance(88001, members[0]);

		PlayerAllianceSnapshot snapshot = members[0].CurrentAllianceSnapshot!;
		foreach (var member in members.Skip(1))
			snapshot = runtime.AddMember(88001, member);

		Assert.True(runtime.IsFull(88001));
		Assert.Equal(members.Select(member => member.ObjectId).ToArray(), snapshot.MemberObjectIds);
		Assert.Equal([1001, 1002, 1003, 1004, 1005, 1006], runtime.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Equal([1007, 1008, 1009, 1010, 1011, 1012], runtime.GetMemberObjectIdsByGroupId(88001, 1001));
		Assert.Equal([1013, 1014, 1015, 1016, 1017, 1018], runtime.GetMemberObjectIdsByGroupId(88001, 1002));
		Assert.Equal([1019, 1020, 1021, 1022, 1023, 1024], runtime.GetMemberObjectIdsByGroupId(88001, 1003));
		Assert.Same(snapshot, members[0].CurrentAllianceSnapshot);
		Assert.Same(snapshot, members[23].CurrentAllianceSnapshot);
		var rejected = CreatePlayer(2001, "Rejected", worldId: 220010000);
		Assert.Throws<InvalidOperationException>(() => runtime.AddMember(88001, rejected));
		Assert.Null(rejected.CurrentAllianceSnapshot);
	}

	[Fact]
	public void RemoveMember_ClearsRemovedPlayerAndRefreshesRemainingAllianceSnapshot()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var removed = CreatePlayer(1002, "Removed", worldId: 220010000);
		var remaining = CreatePlayer(1003, "Remaining", worldId: 230010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, removed);
		runtime.AddMember(88001, remaining);
		runtime.SetViceCaptains(88001, [1002, 1003]);

		var snapshot = Assert.IsType<PlayerAllianceSnapshot>(runtime.RemoveMember(removed));

		Assert.Equal([1001, 1003], snapshot.MemberObjectIds);
		Assert.Equal([1001, 1003], snapshot.MemberObjectIdsByGroupId[1000]);
		Assert.Equal([1003], snapshot.ViceCaptainObjectIds);
		Assert.Equal(PlayerTeamMembership.None, removed.TeamMembership);
		Assert.Equal(0, removed.CurrentTeamId);
		Assert.Empty(removed.CurrentTeamMemberObjectIds);
		Assert.Null(removed.CurrentAllianceSnapshot);
		Assert.Equal(PlayerTeamMembership.Alliance, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Alliance, remaining.TeamMembership);
		Assert.Same(snapshot, leader.CurrentAllianceSnapshot);
		Assert.Same(snapshot, remaining.CurrentAllianceSnapshot);
		Assert.False(runtime.HasMember(88001, 1002));
		Assert.True(runtime.HasMember(88001, 1003));
		Assert.False(runtime.IsViceCaptain(88001, 1002));
		Assert.True(runtime.IsViceCaptain(88001, 1003));
	}

	[Fact]
	public void UpdateMemberLastOnlineTime_UpdatesAllianceMemberLikeJavaLogout()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(456_789);

		var updated = runtime.UpdateMemberLastOnlineTime(member, now);

		Assert.True(updated);
		Assert.Equal(456_789, runtime.GetMember(88001, 1002)?.LastOnlineTimeMillis);
		Assert.Equal(0, runtime.GetMember(88001, 1001)?.LastOnlineTimeMillis);
	}

	[Fact]
	public void UpdateMemberLastOnlineTime_ReturnsFalseForPlayerWithoutRuntimeAlliance()
	{
		var runtime = new PlayerAllianceRuntime();
		var player = CreatePlayer(1001, "Solo", worldId: 210010000);

		var updated = runtime.UpdateMemberLastOnlineTime(player, DateTimeOffset.FromUnixTimeMilliseconds(456_789));

		Assert.False(updated);
		Assert.Null(runtime.GetMember(88001, 1001));
	}

	[Fact]
	public void RemoveMemberWithLeaveWorkflow_DisbandRemovesFindGroupAllianceRecruitmentLikeJava()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var runtime = new PlayerAllianceRuntime(findGroupService, serverId: 5);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);
		findGroupService.AddRecruitment(
			leader,
			"Alliance recruitment",
			groupType: 8,
			nowEpochSeconds: 100,
			new FindGroupRecruitmentSubject(88001, "ELYOS", IsSoloPlayer: false, "Leader", Size: 2, MinLevel: 45, MaxLevel: 45, ClassId: 5));

		var plan = Assert.IsType<PlayerAllianceLeaveWorkflowPlan>(runtime.RemoveMemberWithLeaveWorkflow(member));

		Assert.True(plan.AllianceLeavePlan.WouldDisband);
		Assert.NotNull(plan.FindGroupRecruitmentRemoval);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FindGroupRecruitmentRemoval!.Status);
		Assert.Equal(88001, plan.FindGroupRecruitmentRemoval.RemovedRecruitment?.ObjectId);
		Assert.NotNull(plan.FindGroupRecruitmentRemoval.WorldBroadcastIntent);
		Assert.Contains("broadcastToWorld", plan.FindGroupRecruitmentRemoval.WorldBroadcastIntent!.JavaSource, StringComparison.Ordinal);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
	}

	[Fact]
	public void RemoveMemberWithLeaveWorkflow_DisbandRemovesRecruitmentCreatedByDisabledClientActionPlannerLikeJavaSingleton()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var clientActionPlanner = new FindGroupClientActionPlanService(findGroupService);
		var runtime = new PlayerAllianceRuntime(findGroupService, serverId: 5);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);
		var addPlan = clientActionPlanner.Plan(
			leader,
			new FindGroupClientAction(Action: 2, Message: "Alliance recruitment", GroupType: 8),
			nowEpochSeconds: 100,
			currentTeam: new FindGroupRecruitmentSubject(88001, "ELYOS", IsSoloPlayer: false, "Leader", Size: 2, MinLevel: 40, MaxLevel: 40, ClassId: 5));

		var plan = Assert.IsType<PlayerAllianceLeaveWorkflowPlan>(runtime.RemoveMemberWithLeaveWorkflow(member));

		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, addPlan.Kind);
		Assert.True(plan.AllianceLeavePlan.WouldDisband);
		Assert.NotNull(plan.FindGroupRecruitmentRemoval);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FindGroupRecruitmentRemoval!.Status);
		Assert.Equal(88001, plan.FindGroupRecruitmentRemoval.RemovedRecruitment?.ObjectId);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
	}

	[Fact]
	public void DisbandAfterDisconnectedNoOnlineMembers_ClearsRuntimeAndRecruitmentLikeJavaLogoutDisband()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var runtime = new PlayerAllianceRuntime(findGroupService, serverId: 5);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		leader.IsOnline = false;
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		member.IsOnline = false;
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);
		findGroupService.AddRecruitment(
			leader,
			"Alliance recruitment",
			groupType: 8,
			nowEpochSeconds: 100,
			new FindGroupRecruitmentSubject(88001, "ELYOS", IsSoloPlayer: false, "Leader", Size: 2, MinLevel: 45, MaxLevel: 45, ClassId: 5));

		var plan = Assert.IsType<PlayerAllianceDisconnectedDisbandPlan>(
			runtime.DisbandAfterDisconnectedNoOnlineMembers(88001));

		Assert.True(plan.RemovedRuntimeAlliance);
		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal([1001, 1002], plan.DisbandedPlayerObjectIds);
		Assert.NotNull(plan.FindGroupRecruitmentRemoval);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FindGroupRecruitmentRemoval!.Status);
		Assert.Equal(88001, plan.FindGroupRecruitmentRemoval.RemovedRecruitment?.ObjectId);
		Assert.NotNull(plan.FindGroupRecruitmentRemoval.WorldBroadcastIntent);
		Assert.False(plan.WouldNotifyLeagueAfterDisband);
		Assert.All(plan.BaseLeavePlans, basePlan =>
		{
			Assert.False(basePlan.IsOnline);
			Assert.Empty(basePlan.PacketIntents);
			Assert.True(basePlan.WouldNotifyEventServiceOnLeftTeam);
		});
		Assert.Null(runtime.GetDescriptor(88001));
		Assert.Empty(runtime.GetMemberObjectIds(88001));
		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, member.TeamMembership);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
	}

	[Fact]
	public void Snapshot_CreatesAllianceInfoPlanForExistingPlanners()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var viceCaptain = CreatePlayer(1002, "Vice", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		var snapshot = runtime.AddMember(88001, viceCaptain);
		snapshot = Assert.IsType<PlayerAllianceSnapshot>(runtime.SetViceCaptains(88001, [1002, 404]));

		var infoPlan = snapshot.CreateInfoPacketPlan(activePlayerMapId: viceCaptain.Position.WorldId);

		Assert.Equal(2, infoPlan.AllianceGroupSize);
		Assert.Equal(88001, infoPlan.AllianceId);
		Assert.Equal(1001, infoPlan.LeaderObjectId);
		Assert.Equal(220010000, infoPlan.ActivePlayerMapId);
		Assert.Equal([1002], infoPlan.ViceCaptainObjectIds);
		Assert.Equal([1002, 0, 0, 0], infoPlan.PaddedViceCaptainObjectIds);
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, infoPlan.LootRules.LootRule);
		Assert.Equal(0x3F, infoPlan.TeamType);
		Assert.Equal(0, infoPlan.TeamSubType);
		Assert.Equal(
			[
				new PlayerAllianceInfoGroupPlaceholder(0, 1000),
				new PlayerAllianceInfoGroupPlaceholder(1, 1001),
				new PlayerAllianceInfoGroupPlaceholder(2, 1002),
				new PlayerAllianceInfoGroupPlaceholder(3, 1003),
			],
			infoPlan.GroupPlaceholders);
	}

	[Fact]
	public void ChangeMemberGroup_MovesMemberToTargetJavaAllianceGroupAndReturnsBroadcastPlan()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var moved = CreatePlayer(1002, "Moved", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, moved);

		var plan = Assert.IsType<PlayerAllianceMemberGroupChangePlan>(
			runtime.ChangeMemberGroup(88001, firstMemberObjectId: 1002, secondMemberObjectId: 0, targetAllianceGroupId: 1003));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1002, plan.FirstMemberObjectId);
		Assert.Equal(0, plan.SecondMemberObjectId);
		Assert.Equal(1003, plan.TargetAllianceGroupId);
		Assert.Equal([1001], runtime.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Empty(runtime.GetMemberObjectIdsByGroupId(88001, 1001));
		Assert.Empty(runtime.GetMemberObjectIdsByGroupId(88001, 1002));
		Assert.Equal([1002], runtime.GetMemberObjectIdsByGroupId(88001, 1003));
		Assert.Equal(1003, Assert.IsType<PlayerAllianceMember>(runtime.GetMember(88001, 1002)).AllianceGroupId);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent => AssertGroupChangeIntent(intent, expectedSubjectObjectId: 1002, expectedName: "Moved"));
		Assert.Equal([1001, 1002], moved.CurrentAllianceSnapshot?.MemberObjectIds);
		Assert.Equal([1002], moved.CurrentAllianceSnapshot?.MemberObjectIdsByGroupId[1003]);
	}

	[Fact]
	public void ChangeMemberGroup_SwapsMemberGroupsLikeJavaEvent()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var first = CreatePlayer(1002, "First", worldId: 220010000);
		var second = CreatePlayer(1003, "Second", worldId: 230010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, first);
		runtime.AddMember(88001, second);
		runtime.ChangeMemberGroup(88001, firstMemberObjectId: 1003, secondMemberObjectId: 0, targetAllianceGroupId: 1002);

		var plan = Assert.IsType<PlayerAllianceMemberGroupChangePlan>(
			runtime.ChangeMemberGroup(88001, firstMemberObjectId: 1002, secondMemberObjectId: 1003, targetAllianceGroupId: 0));

		Assert.Equal([1001, 1003], runtime.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Empty(runtime.GetMemberObjectIdsByGroupId(88001, 1001));
		Assert.Equal([1002], runtime.GetMemberObjectIdsByGroupId(88001, 1002));
		Assert.Empty(runtime.GetMemberObjectIdsByGroupId(88001, 1003));
		Assert.Equal(1002, Assert.IsType<PlayerAllianceMember>(runtime.GetMember(88001, 1002)).AllianceGroupId);
		Assert.Equal(1000, Assert.IsType<PlayerAllianceMember>(runtime.GetMember(88001, 1003)).AllianceGroupId);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent => AssertGroupChangeIntent(intent, expectedSubjectObjectId: 1002, expectedName: "First"),
			intent => AssertGroupChangeIntent(intent, expectedSubjectObjectId: 1003, expectedName: "Second"));
	}

	[Fact]
	public void ChangeMemberGroup_ReturnsNullWhenEventMemberLeftBeforeHandling()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);

		Assert.Null(runtime.ChangeMemberGroup(88001, firstMemberObjectId: 404, secondMemberObjectId: 0, targetAllianceGroupId: 1001));
		Assert.Null(runtime.ChangeMemberGroup(88001, firstMemberObjectId: 1001, secondMemberObjectId: 404, targetAllianceGroupId: 0));
		var ex = Assert.Throws<InvalidOperationException>(() => runtime.ChangeMemberGroup(88001, firstMemberObjectId: 1001, secondMemberObjectId: 0, targetAllianceGroupId: 404));

		Assert.Equal("No such alliance group 404", ex.Message);
		Assert.Equal([1002], runtime.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(88001));
		Assert.Equal(0, Assert.IsType<PlayerAllianceMember>(runtime.GetMember(88001, 1001)).AllianceGroupId);
	}

	[Fact]
	public void GroupChangeServicePlanner_ReturnsNotAllianceMemberMessageLikeJavaService()
	{
		var runtime = new PlayerAllianceRuntime();
		var planner = new PlayerAllianceGroupChangeServicePlanner(runtime);
		var caller = CreatePlayer(1001, "Caller", worldId: 210010000);

		var plan = planner.CreateChangeMemberGroupPlan(caller, firstMemberObjectId: 1001, secondMemberObjectId: 0, targetAllianceGroupId: 1001);

		Assert.Equal(PlayerAllianceGroupChangeServicePlanStatus.NotAllianceMember, plan.Status);
		Assert.Equal(0, plan.AllianceId);
		Assert.Equal(1001, plan.CallerObjectId);
		Assert.Null(plan.GroupChangePlan);
		var systemMessage = Assert.IsType<PlayerAllianceSystemMessageIntent>(plan.SystemMessageIntent);
		Assert.Equal(1001, systemMessage.RecipientObjectId);
		Assert.Equal(1301015, systemMessage.Message.MessageId);
	}

	[Fact]
	public void GroupChangeServicePlanner_ReturnsNoRightsMessageForNonCaptainLikeJavaService()
	{
		var runtime = new PlayerAllianceRuntime();
		var planner = new PlayerAllianceGroupChangeServicePlanner(runtime);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var caller = CreatePlayer(1002, "Caller", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, caller);

		var plan = planner.CreateChangeMemberGroupPlan(caller, firstMemberObjectId: 1002, secondMemberObjectId: 0, targetAllianceGroupId: 1003);

		Assert.Equal(PlayerAllianceGroupChangeServicePlanStatus.NotAuthorized, plan.Status);
		Assert.Equal(88001, plan.AllianceId);
		Assert.Null(plan.GroupChangePlan);
		var systemMessage = Assert.IsType<PlayerAllianceSystemMessageIntent>(plan.SystemMessageIntent);
		Assert.Equal(1002, systemMessage.RecipientObjectId);
		Assert.Equal(1300976, systemMessage.Message.MessageId);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Empty(runtime.GetMemberObjectIdsByGroupId(88001, 1003));
	}

	[Fact]
	public void GroupChangeServicePlanner_DispatchesForLeaderAndViceCaptainLikeJavaService()
	{
		var runtime = new PlayerAllianceRuntime();
		var planner = new PlayerAllianceGroupChangeServicePlanner(runtime);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var viceCaptain = CreatePlayer(1002, "Vice", worldId: 220010000);
		var moved = CreatePlayer(1003, "Moved", worldId: 230010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, viceCaptain);
		runtime.AddMember(88001, moved);

		var leaderPlan = planner.CreateChangeMemberGroupPlan(leader, firstMemberObjectId: 1003, secondMemberObjectId: 0, targetAllianceGroupId: 1001);
		runtime.SetViceCaptains(88001, [1002]);
		var viceCaptainPlan = planner.CreateChangeMemberGroupPlan(viceCaptain, firstMemberObjectId: 1003, secondMemberObjectId: 0, targetAllianceGroupId: 1002);

		Assert.Equal(PlayerAllianceGroupChangeServicePlanStatus.Dispatched, leaderPlan.Status);
		Assert.NotNull(leaderPlan.GroupChangePlan);
		Assert.Null(leaderPlan.SystemMessageIntent);
		Assert.Equal([1003], runtime.GetMemberObjectIdsByGroupId(88001, 1002));
		Assert.Equal(PlayerAllianceGroupChangeServicePlanStatus.Dispatched, viceCaptainPlan.Status);
		Assert.NotNull(viceCaptainPlan.GroupChangePlan);
		Assert.Null(viceCaptainPlan.SystemMessageIntent);
	}

	[Fact]
	public void GroupChangeServicePlanner_ReportsSkippedWhenJavaEventTargetAlreadyLeft()
	{
		var runtime = new PlayerAllianceRuntime();
		var planner = new PlayerAllianceGroupChangeServicePlanner(runtime);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		runtime.CreateAlliance(88001, leader);

		var plan = planner.CreateChangeMemberGroupPlan(leader, firstMemberObjectId: 404, secondMemberObjectId: 0, targetAllianceGroupId: 1001);

		Assert.Equal(PlayerAllianceGroupChangeServicePlanStatus.EventSkipped, plan.Status);
		Assert.Equal(88001, plan.AllianceId);
		Assert.Null(plan.GroupChangePlan);
		Assert.Null(plan.SystemMessageIntent);
	}

	[Fact]
	public void AllianceReadyCheckCommand_JavaCodesMatchTeamCommand()
	{
		Assert.Equal(20, (int)PlayerAllianceReadyCheckCommand.Cancel);
		Assert.Equal(21, (int)PlayerAllianceReadyCheckCommand.Start);
		Assert.Equal(22, (int)PlayerAllianceReadyCheckCommand.AutoCancel);
		Assert.Equal(23, (int)PlayerAllianceReadyCheckCommand.Ready);
		Assert.Equal(24, (int)PlayerAllianceReadyCheckCommand.NotReady);
	}

	[Fact]
	public void SmAllianceReadyCheck_WritesJavaPayload()
	{
		var packet = new SmAllianceReadyCheck(playerObjectId: 1001, statusCode: 5);

		Assert.Equal(250, SmAllianceReadyCheck.PacketOpCode);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(5, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void CheckReady_StartSetsOnlineMemberCountMinusOneAndBroadcastsStartPacketsLikeJava()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var onlineMember = CreatePlayer(1002, "Online", worldId: 220010000);
		var offlineMember = CreatePlayer(1003, "Offline", worldId: 230010000);
		offlineMember.IsOnline = false;
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, onlineMember);
		runtime.AddMember(88001, offlineMember);

		var plan = Assert.IsType<PlayerAllianceReadyCheckPlan>(
			runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.Start));

		Assert.Equal(0, plan.ReadyStatusBefore);
		Assert.Equal(1, plan.ReadyStatusAfter);
		Assert.Equal(1, runtime.GetAllianceReadyStatus(88001));
		AssertReadyIntent(plan.PacketIntents[0], sequence: 0, recipientObjectId: 1001, playerObjectId: 1001, statusCode: 5);
		AssertReadyIntent(plan.PacketIntents[1], sequence: 1, recipientObjectId: 1001, playerObjectId: 1001, statusCode: 1);
		AssertReadyIntent(plan.PacketIntents[2], sequence: 2, recipientObjectId: 1002, playerObjectId: 1001, statusCode: 5);
		AssertReadyIntent(plan.PacketIntents[3], sequence: 3, recipientObjectId: 1002, playerObjectId: 1001, statusCode: 1);
		AssertReadyIntent(plan.PacketIntents[4], sequence: 4, recipientObjectId: 1003, playerObjectId: 1001, statusCode: 5);
		AssertReadyIntent(plan.PacketIntents[5], sequence: 5, recipientObjectId: 1003, playerObjectId: 1001, statusCode: 1);
	}

	[Fact]
	public void CheckReady_ReadyAndNotReadyDecrementAndSendCompletionWhenStatusReachesZero()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var readyMember = CreatePlayer(1002, "Ready", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, readyMember);
		runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.Start);

		var readyPlan = Assert.IsType<PlayerAllianceReadyCheckPlan>(
			runtime.CheckReady(88001, readyMember, PlayerAllianceReadyCheckCommand.Ready));
		runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.Start);
		var notReadyPlan = Assert.IsType<PlayerAllianceReadyCheckPlan>(
			runtime.CheckReady(88001, readyMember, PlayerAllianceReadyCheckCommand.NotReady));

		Assert.Equal(1, readyPlan.ReadyStatusBefore);
		Assert.Equal(0, readyPlan.ReadyStatusAfter);
		Assert.Collection(
			readyPlan.PacketIntents,
			intent => AssertReadyIntent(intent, sequence: 0, recipientObjectId: 1001, playerObjectId: 1002, statusCode: 5),
			intent => AssertReadyIntent(intent, sequence: 1, recipientObjectId: 1001, playerObjectId: 0, statusCode: 3),
			intent => AssertReadyIntent(intent, sequence: 2, recipientObjectId: 1002, playerObjectId: 1002, statusCode: 5),
			intent => AssertReadyIntent(intent, sequence: 3, recipientObjectId: 1002, playerObjectId: 0, statusCode: 3));
		Assert.Equal(1, notReadyPlan.ReadyStatusBefore);
		Assert.Equal(0, notReadyPlan.ReadyStatusAfter);
		Assert.Collection(
			notReadyPlan.PacketIntents,
			intent => AssertReadyIntent(intent, sequence: 0, recipientObjectId: 1001, playerObjectId: 1002, statusCode: 4),
			intent => AssertReadyIntent(intent, sequence: 1, recipientObjectId: 1001, playerObjectId: 0, statusCode: 3),
			intent => AssertReadyIntent(intent, sequence: 2, recipientObjectId: 1002, playerObjectId: 1002, statusCode: 4),
			intent => AssertReadyIntent(intent, sequence: 3, recipientObjectId: 1002, playerObjectId: 0, statusCode: 3));
	}

	[Fact]
	public void CheckReady_CancelAndAutoCancelResetStatusLikeJava()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);
		runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.Start);

		var cancelPlan = Assert.IsType<PlayerAllianceReadyCheckPlan>(
			runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.Cancel));
		runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.Start);
		var autoCancelPlan = Assert.IsType<PlayerAllianceReadyCheckPlan>(
			runtime.CheckReady(88001, leader, PlayerAllianceReadyCheckCommand.AutoCancel));

		Assert.Equal(1, cancelPlan.ReadyStatusBefore);
		Assert.Equal(0, cancelPlan.ReadyStatusAfter);
		Assert.Equal(0, runtime.GetAllianceReadyStatus(88001));
		Assert.Collection(
			cancelPlan.PacketIntents,
			intent => AssertReadyIntent(intent, sequence: 0, recipientObjectId: 1001, playerObjectId: 1001, statusCode: 0),
			intent => AssertReadyIntent(intent, sequence: 1, recipientObjectId: 1002, playerObjectId: 1001, statusCode: 0));
		Assert.Equal(1, autoCancelPlan.ReadyStatusBefore);
		Assert.Equal(0, autoCancelPlan.ReadyStatusAfter);
		Assert.Collection(
			autoCancelPlan.PacketIntents,
			intent => AssertReadyIntent(intent, sequence: 0, recipientObjectId: 1001, playerObjectId: 1001, statusCode: 2),
			intent => AssertReadyIntent(intent, sequence: 1, recipientObjectId: 1002, playerObjectId: 1001, statusCode: 2));
	}

	[Fact]
	public void CheckReady_ReturnsNullForMissingAllianceOrPlayer()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var outsider = CreatePlayer(1002, "Outsider", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);

		Assert.Null(runtime.CheckReady(88001, outsider, PlayerAllianceReadyCheckCommand.Start));
		Assert.Null(runtime.CheckReady(99001, leader, PlayerAllianceReadyCheckCommand.Start));
	}

	[Fact]
	public void UpdateBrand_StoresBrandAndPlansAllianceBroadcastLikeJavaTemporaryPlayerTeam()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, member);

		var plan = Assert.IsType<PlayerAllianceBrandUpdatePlan>(runtime.UpdateBrand(88001, brandId: 4, targetObjectId: 8002));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(4, plan.BrandId);
		Assert.Equal(8002, plan.TargetObjectId);
		Assert.Collection(
			plan.BrandBroadcasts,
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertShowBrandPayload(intent.CreatePacket(), (4, 8002));
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				AssertShowBrandPayload(intent.CreatePacket(), (4, 8002));
			});
		var sendBrandsIntent = Assert.IsType<PlayerAllianceBrandIntent>(runtime.CreateSendBrandsIntent(88001, member));
		Assert.Equal(1002, sendBrandsIntent.RecipientObjectId);
		Assert.Equal(new Dictionary<int, int> { [4] = 8002 }, sendBrandsIntent.TargetObjectIdsByBrandId);
		AssertShowBrandPayload(sendBrandsIntent.CreatePacket(), (4, 8002));
	}

	[Fact]
	public void CreateSendBrandsIntent_EmptyAllianceBrandMapResetsAllJavaBrands()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		runtime.CreateAlliance(88001, leader);

		var intent = Assert.IsType<PlayerAllianceBrandIntent>(runtime.CreateSendBrandsIntent(88001, leader));

		Assert.Empty(intent.TargetObjectIdsByBrandId);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(intent.CreatePacket()));
		Assert.Equal(16, reader.ReadH());
		for (var brandId = 0; brandId < 16; brandId++)
		{
			Assert.Equal(1, reader.ReadD());
			Assert.Equal(brandId, reader.ReadD());
			Assert.Equal(0, reader.ReadD());
		}
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void AllianceBrandRuntime_ReturnsNullForUnknownAllianceOrRecipient()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var outsider = CreatePlayer(1002, "Outsider", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);

		Assert.Null(runtime.UpdateBrand(99001, brandId: 4, targetObjectId: 8002));
		Assert.Null(runtime.CreateSendBrandsIntent(88001, outsider));
		Assert.Null(runtime.CreateSendBrandsIntent(99001, leader));
	}

	[Fact]
	public void CreateEnteredPlan_IncludesCurrentBrandsIntentLikeJavaPlayerAllianceEnteredEvent()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var invited = CreatePlayer(1002, "Invited", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		runtime.UpdateBrand(88001, brandId: 4, targetObjectId: 8002);
		runtime.AddMember(88001, invited);

		var plan = Assert.IsType<PlayerAllianceEnteredPlan>(runtime.CreateEnteredPlan(88001, invited));

		Assert.True(plan.WouldSendBrands);
		var brandIntent = Assert.IsType<PlayerAllianceBrandIntent>(plan.BrandIntent);
		Assert.Equal(invited.ObjectId, brandIntent.RecipientObjectId);
		Assert.Equal(new Dictionary<int, int> { [4] = 8002 }, brandIntent.TargetObjectIdsByBrandId);
		AssertShowBrandPayload(brandIntent.CreatePacket(), (4, 8002));
		Assert.Equal(0, plan.PacketIntents[0].Sequence);
		Assert.Equal(PlayerAlliancePacketIntentKind.AllianceInfo, plan.PacketIntents[0].Kind);
		Assert.Equal(2, plan.PacketIntents[2].Sequence);
		Assert.Equal(PlayerAlliancePacketIntentKind.MemberInfo, plan.PacketIntents[2].Kind);
	}

	[Fact]
	public void ShowBrandCommandPlanner_EchoesSoloBrandLikeJavaCmShowBrand()
	{
		var planner = new PlayerShowBrandCommandPlanner(new PlayerGroupRuntime(), new PlayerAllianceRuntime());
		var player = CreatePlayer(1001, "Solo", worldId: 210010000);

		var plan = planner.CreatePlan(player, brandId: 7, targetObjectId: 9001);

		Assert.Equal(PlayerShowBrandCommandPlanStatus.SoloEcho, plan.Status);
		Assert.Equal(1001, plan.CallerObjectId);
		Assert.Equal(7, plan.BrandId);
		Assert.Equal(9001, plan.TargetObjectId);
		Assert.Null(plan.GroupUpdatePlan);
		Assert.Null(plan.AllianceUpdatePlan);
		var intent = Assert.IsType<PlayerShowBrandIntent>(plan.SoloEchoIntent);
		Assert.Equal(1001, intent.RecipientObjectId);
		AssertShowBrandPayload(intent.CreatePacket(), (7, 9001));
	}

	[Fact]
	public void ShowBrandCommandPlanner_GroupLeaderUpdatesGroupAndMemberIsIgnoredLikeJavaCmShowBrand()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var planner = new PlayerShowBrandCommandPlanner(groups, alliances);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var member = CreatePlayer(1002, "Member", worldId: 220010000);
		groups.CreateOrUpdateGroup(99001, [leader, member]);

		var leaderPlan = planner.CreatePlan(leader, brandId: 3, targetObjectId: 8001);
		var memberPlan = planner.CreatePlan(member, brandId: 4, targetObjectId: 8002);

		Assert.Equal(PlayerShowBrandCommandPlanStatus.GroupUpdated, leaderPlan.Status);
		var updatePlan = Assert.IsType<PlayerGroupBrandUpdatePlan>(leaderPlan.GroupUpdatePlan);
		Assert.Null(leaderPlan.SoloEchoIntent);
		Assert.Null(leaderPlan.AllianceUpdatePlan);
		Assert.Collection(
			updatePlan.BrandBroadcasts,
			intent => AssertShowBrandPayload(intent.CreatePacket(), (3, 8001)),
			intent => AssertShowBrandPayload(intent.CreatePacket(), (3, 8001)));
		Assert.Equal(PlayerShowBrandCommandPlanStatus.NotAuthorized, memberPlan.Status);
		Assert.Null(memberPlan.SoloEchoIntent);
		Assert.Null(memberPlan.GroupUpdatePlan);
		Assert.Null(memberPlan.AllianceUpdatePlan);
	}

	[Fact]
	public void ShowBrandCommandPlanner_AllianceLeaderAndViceCaptainUpdateAllianceLikeJavaCmShowBrand()
	{
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var planner = new PlayerShowBrandCommandPlanner(groups, alliances);
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var viceCaptain = CreatePlayer(1002, "Vice", worldId: 220010000);
		var member = CreatePlayer(1003, "Member", worldId: 230010000);
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, viceCaptain);
		alliances.AddMember(88001, member);
		alliances.SetViceCaptains(88001, [1002]);

		var leaderPlan = planner.CreatePlan(leader, brandId: 5, targetObjectId: 8005);
		var viceCaptainPlan = planner.CreatePlan(viceCaptain, brandId: 6, targetObjectId: 8006);
		var memberPlan = planner.CreatePlan(member, brandId: 7, targetObjectId: 8007);

		Assert.Equal(PlayerShowBrandCommandPlanStatus.AllianceUpdated, leaderPlan.Status);
		Assert.Equal(3, Assert.IsType<PlayerAllianceBrandUpdatePlan>(leaderPlan.AllianceUpdatePlan).BrandBroadcasts.Count);
		Assert.Equal(PlayerShowBrandCommandPlanStatus.AllianceUpdated, viceCaptainPlan.Status);
		var viceUpdate = Assert.IsType<PlayerAllianceBrandUpdatePlan>(viceCaptainPlan.AllianceUpdatePlan);
		Assert.Collection(
			viceUpdate.BrandBroadcasts,
			intent => AssertShowBrandPayload(intent.CreatePacket(), (6, 8006)),
			intent => AssertShowBrandPayload(intent.CreatePacket(), (6, 8006)),
			intent => AssertShowBrandPayload(intent.CreatePacket(), (6, 8006)));
		Assert.Equal(PlayerShowBrandCommandPlanStatus.NotAuthorized, memberPlan.Status);
		Assert.Null(memberPlan.AllianceUpdatePlan);
	}

	[Fact]
	public void ShowBrandCommandPlanner_ReportsTeamMissingForStaleCurrentTeam()
	{
		var planner = new PlayerShowBrandCommandPlanner(new PlayerGroupRuntime(), new PlayerAllianceRuntime());
		var player = CreatePlayer(1001, "Stale", worldId: 210010000);
		player.TeamMembership = PlayerTeamMembership.Group;
		player.CurrentTeamId = 99001;
		player.CurrentTeamMemberObjectIds = [1001];

		var plan = planner.CreatePlan(player, brandId: 3, targetObjectId: 8001);

		Assert.Equal(PlayerShowBrandCommandPlanStatus.TeamMissing, plan.Status);
		Assert.Null(plan.SoloEchoIntent);
		Assert.Null(plan.GroupUpdatePlan);
		Assert.Null(plan.AllianceUpdatePlan);
	}

	private static Player CreatePlayer(int objectId, string name, int worldId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(worldId, 11, 22, 33, 64),
		};
	}

	private static void AssertGroupChangeIntent(
		PlayerAllianceMemberInfoIntent intent,
		int expectedSubjectObjectId,
		string expectedName)
	{
		Assert.Equal(0, intent.RecipientObjectId);
		Assert.Equal(expectedSubjectObjectId, intent.SubjectObjectId);
		Assert.Equal(PlayerAllianceEvent.MemberGroupChange, intent.Event);
		var plan = Assert.IsType<PlayerAllianceMemberInfoPacketPlan>(intent.PacketPlan);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.MemberGroupChange, plan.RequestedEventKind);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.MemberGroupChange, plan.EffectiveEventKind);
		Assert.True(plan.WritesName);
		Assert.False(plan.WritesAbnormalEffects);
		Assert.False(plan.WritesSlotTimers);
		Assert.Equal(expectedName, plan.PrefixSnapshot.Name);
	}

	private static void AssertReadyIntent(
		PlayerAllianceReadyCheckPacketIntent intent,
		int sequence,
		int recipientObjectId,
		int playerObjectId,
		int statusCode)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(playerObjectId, intent.PlayerObjectId);
		Assert.Equal(statusCode, intent.StatusCode);
		var packet = Assert.IsType<SmAllianceReadyCheck>(intent.CreatePacket());
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(statusCode, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertShowBrandPayload(GameServerPacket packet, params (int BrandId, int TargetObjectId)[] expectedBrands)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedBrands.Length, reader.ReadH());
		foreach (var (brandId, targetObjectId) in expectedBrands)
		{
			Assert.Equal(1, reader.ReadD());
			Assert.Equal(brandId, reader.ReadD());
			Assert.Equal(targetObjectId, reader.ReadD());
		}
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
