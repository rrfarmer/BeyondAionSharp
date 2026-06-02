using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupRecruitmentPlanServiceTests
{
	[Fact]
	public void AddRecruitment_StoresSoloEntryThenPlansPostedMessageAndRaceFilteredShowList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var asmodian = CreatePlayer(1001, "OtherRace", "ASMODIANS", "RANGER", 45);
		service.AddRecruitment(asmodian, "Asmo only", groupType: 3, nowEpochSeconds: 111);
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);

		var plan = service.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 222);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Added, plan.Status);
		Assert.NotNull(plan.CurrentRecruitment);
		Assert.Equal(2001, plan.CurrentRecruitment.ObjectId);
		Assert.True(plan.CurrentRecruitment.IsSoloPlayer);
		Assert.Equal("ELYOS", plan.CurrentRecruitment.Race);
		Assert.Equal("Recruiter", plan.CurrentRecruitment.RecruiterName);
		Assert.Equal(1, plan.CurrentRecruitment.Size);
		Assert.Equal(50, plan.CurrentRecruitment.MinLevel);
		Assert.Equal(50, plan.CurrentRecruitment.MaxLevel);
		Assert.Equal(1, plan.CurrentRecruitment.ClassId);
		Assert.Equal(222, plan.CurrentRecruitment.LastUpdate);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(2001, direct.RecipientObjectId);
		Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED", direct.JavaSource);
		Assert.Equal(1400392, Assert.IsType<SmSystemMessage>(direct.Packet).MessageId);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.NotNull(plan.ShowRecruitmentsPlan);
		var show = plan.ShowRecruitmentsPlan!;
		Assert.Equal("ELYOS", show.Race);
		Assert.Equal(222, show.LastUpdate);
		var snapshot = Assert.Single(show.Recruitments);
		Assert.Equal(2001, snapshot.ObjectId);
		Assert.Equal("Need healer", snapshot.Message);
		Assert.Equal("Recruiter", snapshot.RecruiterName);
		Assert.Equal(2, snapshot.GroupType);
		Assert.Equal(1, snapshot.Size);
		Assert.Equal(50, snapshot.MinLevel);
		Assert.Equal(50, snapshot.MaxLevel);
		Assert.Equal(222, snapshot.LastUpdate);
		Assert.Equal(
			Convert.FromHexString("0001000100DE000000D107000000000010024E0065006500640020006800650061006C006500720000005200650063007200750069007400650072000000013232DE000000"),
			SerializeUnencryptedPayload(show.Packet));
	}

	[Fact]
	public void AddRecruitment_UsesTeamSubjectWhenCurrentTeamExists()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = WithTeam(CreatePlayer(2001, "Leader", "ELYOS", "RANGER", 55), PlayerTeamMembership.Group, teamId: 9001);
		var team = new FindGroupRecruitmentSubject(
			ObjectId: 9001,
			Race: "ELYOS",
			IsSoloPlayer: false,
			RecruiterName: "Leader",
			Size: 4,
			MinLevel: 50,
			MaxLevel: 55,
			ClassId: 5);

		var plan = service.AddRecruitment(player, "Group run", groupType: 4, nowEpochSeconds: 333, team);

		Assert.NotNull(plan.CurrentRecruitment);
		var state = plan.CurrentRecruitment!;
		Assert.Equal(9001, state.ObjectId);
		Assert.False(state.IsSoloPlayer);
		Assert.Equal(4, state.Size);
		Assert.Equal(50, state.MinLevel);
		Assert.Equal(55, state.MaxLevel);
		Assert.NotNull(plan.ShowRecruitmentsPlan);
		var snapshot = Assert.Single(plan.ShowRecruitmentsPlan!.Recruitments);
		Assert.Equal(9001, snapshot.ObjectId);
		Assert.False(snapshot.IsSoloPlayer);
		Assert.Equal(4, snapshot.Size);
		Assert.Equal(50, snapshot.MinLevel);
		Assert.Equal(55, snapshot.MaxLevel);
	}

	[Fact]
	public void UpdateRecruitment_ExistingEntryMutatesMessageTypeAndTimestampWithoutPackets()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);
		service.AddRecruitment(player, "Old", groupType: 1, nowEpochSeconds: 100);

		var plan = service.UpdateRecruitment(player, "New", groupType: 6, nowEpochSeconds: 300);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Updated, plan.Status);
		Assert.NotNull(plan.CurrentRecruitment);
		var state = plan.CurrentRecruitment!;
		Assert.Equal("New", state.Message);
		Assert.Equal(6, state.GroupType);
		Assert.Equal(300, state.LastUpdate);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.Null(plan.ShowRecruitmentsPlan);
		var show = service.ShowRecruitments("ELYOS", nowEpochSeconds: 301);
		Assert.Equal("New", Assert.Single(show.Recruitments).Message);
	}

	[Fact]
	public void RemoveRecruitment_MissingEntryDoesNotBroadcast()
	{
		var service = new FindGroupRecruitmentPlanService();

		var plan = service.RemoveRecruitment(404, serverId: 5, unknown1: 6, unknown2: 7, unknown3: 8);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.Status);
		Assert.Null(plan.RemovedRecruitment);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.ShowRecruitmentsPlan);
	}

	[Fact]
	public void RemoveRecruitment_ExistingEntryPlansRaceFilteredWorldBroadcast()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);
		service.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 222);

		var plan = service.RemoveRecruitment(player, serverId: 5, unknown1: 6, unknown2: 7, unknown3: 8);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.Status);
		Assert.NotNull(plan.RemovedRecruitment);
		Assert.Equal(2001, plan.RemovedRecruitment!.ObjectId);
		Assert.NotNull(plan.WorldBroadcastIntent);
		var broadcast = plan.WorldBroadcastIntent!;
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())", broadcast.JavaSource);
		Assert.Equal(Convert.FromHexString("01D107000005060708"), SerializeUnencryptedPayload(broadcast.Packet));
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
	}

	[Fact]
	public void AddApplication_StoresPlayerEntryThenPlansPostedMessageAndRaceFilteredShowList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var asmodian = CreatePlayer(1001, "OtherRace", "ASMODIANS", "RANGER", 45);
		service.AddApplication(asmodian, "Asmo apply", groupType: 3, classId: 5, level: 45, nowEpochSeconds: 111);
		var player = CreatePlayer(2001, "Applicant", "ELYOS", "RANGER", 45);

		var plan = service.AddApplication(player, "Need group", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 222);

		Assert.Equal(FindGroupApplicationPlanStatus.Added, plan.Status);
		Assert.NotNull(plan.CurrentApplication);
		var state = plan.CurrentApplication!;
		Assert.Equal(2001, state.PlayerObjectId);
		Assert.Equal("ELYOS", state.Race);
		Assert.Equal("Need group", state.Message);
		Assert.Equal("Applicant", state.PlayerName);
		Assert.Equal(2, state.GroupType);
		Assert.Equal(5, state.ClassId);
		Assert.Equal(45, state.Level);
		Assert.Equal(222, state.LastUpdate);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(2001, direct.RecipientObjectId);
		Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_SEEK_PARTY_POSTED", direct.JavaSource);
		Assert.Equal(1400393, Assert.IsType<SmSystemMessage>(direct.Packet).MessageId);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.NotNull(plan.ShowApplicationsPlan);
		var show = plan.ShowApplicationsPlan!;
		Assert.Equal("ELYOS", show.Race);
		Assert.Equal(222, show.LastUpdate);
		var snapshot = Assert.Single(show.Applications);
		Assert.Equal(2001, snapshot.PlayerObjectId);
		Assert.Equal("Need group", snapshot.Message);
		Assert.Equal("Applicant", snapshot.PlayerName);
		Assert.Equal(2, snapshot.GroupType);
		Assert.Equal(5, snapshot.ClassId);
		Assert.Equal(45, snapshot.Level);
		Assert.Equal(222, snapshot.LastUpdate);
		Assert.Equal(
			Convert.FromHexString("0401000100DE000000D1070000024E006500650064002000670072006F007500700000004100700070006C006900630061006E0074000000052DDE000000"),
			SerializeUnencryptedPayload(show.Packet));
	}

	[Fact]
	public void UpdateApplication_ExistingEntryMutatesMessageTypeClassLevelAndTimestampWithoutPackets()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Applicant", "ELYOS", "RANGER", 45);
		service.AddApplication(player, "Old", groupType: 1, classId: 5, level: 45, nowEpochSeconds: 100);

		var plan = service.UpdateApplication(player, "New", groupType: 6, classId: 10, level: 51, nowEpochSeconds: 300);

		Assert.Equal(FindGroupApplicationPlanStatus.Updated, plan.Status);
		Assert.NotNull(plan.CurrentApplication);
		var state = plan.CurrentApplication!;
		Assert.Equal("New", state.Message);
		Assert.Equal(6, state.GroupType);
		Assert.Equal(10, state.ClassId);
		Assert.Equal(51, state.Level);
		Assert.Equal(300, state.LastUpdate);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.Null(plan.ShowApplicationsPlan);
		var show = service.ShowApplications("ELYOS", nowEpochSeconds: 301);
		var snapshot = Assert.Single(show.Applications);
		Assert.Equal("New", snapshot.Message);
		Assert.Equal(10, snapshot.ClassId);
		Assert.Equal(51, snapshot.Level);
	}

	[Fact]
	public void RemoveApplication_MissingEntryDoesNotBroadcast()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(404, "Missing", "ELYOS", "GLADIATOR", 50);

		var plan = service.RemoveApplication(player);

		Assert.Equal(FindGroupApplicationPlanStatus.Missing, plan.Status);
		Assert.Null(plan.RemovedApplication);
		Assert.Null(plan.WorldBroadcastIntent);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.ShowApplicationsPlan);
	}

	[Fact]
	public void RemoveApplication_ExistingEntryPlansRaceFilteredWorldBroadcast()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Applicant", "ELYOS", "RANGER", 45);
		service.AddApplication(player, "Need group", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 222);

		var plan = service.RemoveApplication(player);

		Assert.Equal(FindGroupApplicationPlanStatus.Removed, plan.Status);
		Assert.NotNull(plan.RemovedApplication);
		Assert.Equal(2001, plan.RemovedApplication!.PlayerObjectId);
		Assert.NotNull(plan.WorldBroadcastIntent);
		var broadcast = plan.WorldBroadcastIntent!;
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == application.getPlayer().getRace())", broadcast.JavaSource);
		Assert.Equal(Convert.FromHexString("05D1070000"), SerializeUnencryptedPayload(broadcast.Packet));
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 400).Applications);
	}

	[Fact]
	public void OnJoinedTeam_RemovesApplicationAndSoloRecruitmentWithJavaUnknown16()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Applicant", "ELYOS", "RANGER", 45);
		var team = CreateTeamSubject(9001, "Leader", size: 3, minLevel: 40, maxLevel: 50);
		service.AddApplication(player, "Need group", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 222);
		service.AddRecruitment(player, "Solo post", groupType: 4, nowEpochSeconds: 223);

		var plan = service.OnJoinedTeam(player, team, isLeader: false, isFull: false, nowEpochSeconds: 333, serverId: 5);

		Assert.False(plan.DispatchLiveSideEffects);
		Assert.False(plan.InstanceGroupRemoval.ShouldRemove);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, plan.ApplicationRemoval.Status);
		Assert.NotNull(plan.ApplicationRemoval.WorldBroadcastIntent);
		Assert.Equal(Convert.FromHexString("05D1070000"), SerializeUnencryptedPayload(plan.ApplicationRemoval.WorldBroadcastIntent!.Packet));
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.SoloRecruitmentRemoval.Status);
		Assert.NotNull(plan.SoloRecruitmentRemoval.WorldBroadcastIntent);
		Assert.Equal(Convert.FromHexString("01D107000005000010"), SerializeUnencryptedPayload(plan.SoloRecruitmentRemoval.WorldBroadcastIntent!.Packet));
		Assert.Null(plan.TeamRecruitmentAdd);
		Assert.Null(plan.FullTeamRecruitmentRemoval);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 400).Applications);
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
	}

	[Fact]
	public void OnJoinedTeam_ReaddsSoloRecruitmentAsTeamRecruitmentWhenPlayerIsLeader()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = WithTeam(CreatePlayer(2001, "Leader", "ELYOS", "RANGER", 55), PlayerTeamMembership.Group, teamId: 9001);
		var team = CreateTeamSubject(9001, "Leader", size: 4, minLevel: 50, maxLevel: 55);
		service.AddRecruitment(player, "Old solo message", groupType: 6, nowEpochSeconds: 223);

		var plan = service.OnJoinedTeam(player, team, isLeader: true, isFull: false, nowEpochSeconds: 333, serverId: 5);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.SoloRecruitmentRemoval.Status);
		Assert.NotNull(plan.TeamRecruitmentAdd);
		var add = plan.TeamRecruitmentAdd!;
		Assert.Equal(FindGroupRecruitmentPlanStatus.Added, add.Status);
		Assert.NotNull(add.CurrentRecruitment);
		Assert.Equal(9001, add.CurrentRecruitment!.ObjectId);
		Assert.False(add.CurrentRecruitment.IsSoloPlayer);
		Assert.Equal("Old solo message", add.CurrentRecruitment.Message);
		Assert.Equal(6, add.CurrentRecruitment.GroupType);
		Assert.Equal(333, add.CurrentRecruitment.LastUpdate);
		var direct = Assert.Single(add.DirectPacketIntents);
		Assert.Equal(1400392, Assert.IsType<SmSystemMessage>(direct.Packet).MessageId);
		Assert.NotNull(add.ShowRecruitmentsPlan);
		var snapshot = Assert.Single(add.ShowRecruitmentsPlan!.Recruitments);
		Assert.Equal(9001, snapshot.ObjectId);
		Assert.False(snapshot.IsSoloPlayer);
		Assert.Null(plan.FullTeamRecruitmentRemoval);
	}

	[Fact]
	public void OnJoinedTeam_LeaderSoloRecruitmentReaddTakesPriorityOverFullTeamRemovalLikeJava()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = WithTeam(CreatePlayer(2001, "Leader", "ELYOS", "RANGER", 55), PlayerTeamMembership.Group, teamId: 9001);
		var team = CreateTeamSubject(9001, "Leader", size: 6, minLevel: 50, maxLevel: 55);
		service.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 200);
		service.AddApplication(player, "Need team", groupType: 3, classId: 5, level: 55, nowEpochSeconds: 201);
		service.AddRecruitment(player, "Solo leader post", groupType: 6, nowEpochSeconds: 202);
		service.AddRecruitment(
			player,
			"Existing full team post",
			groupType: 9,
			nowEpochSeconds: 203,
			team);

		var plan = service.OnJoinedTeam(player, team, isLeader: true, isFull: true, nowEpochSeconds: 333, serverId: 5);

		Assert.True(plan.InstanceGroupRemoval.ShouldRemove);
		Assert.NotNull(plan.InstanceGroupRemoval.RemovedInstanceGroup);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, plan.ApplicationRemoval.Status);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.SoloRecruitmentRemoval.Status);
		Assert.NotNull(plan.TeamRecruitmentAdd);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Added, plan.TeamRecruitmentAdd!.Status);
		Assert.Null(plan.FullTeamRecruitmentRemoval);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 400).Applications);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 401).InstanceGroups);
		var recruitment = Assert.Single(service.ShowRecruitments("ELYOS", nowEpochSeconds: 402).Recruitments);
		Assert.Equal(9001, recruitment.ObjectId);
		Assert.False(recruitment.IsSoloPlayer);
		Assert.Equal("Solo leader post", recruitment.Message);
		Assert.Equal(6, recruitment.GroupType);
		Assert.Equal(333, recruitment.LastUpdate);
	}

	[Fact]
	public void OnJoinedTeam_RemovesFullTeamRecruitmentWhenNoSoloRecruitmentWasRemoved()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = WithTeam(CreatePlayer(2001, "Member", "ELYOS", "RANGER", 55), PlayerTeamMembership.Group, teamId: 9001);
		var team = CreateTeamSubject(9001, "Leader", size: 6, minLevel: 50, maxLevel: 55);
		service.AddRecruitment(player, "Team post", groupType: 7, nowEpochSeconds: 223, team);

		var plan = service.OnJoinedTeam(player, team, isLeader: false, isFull: true, nowEpochSeconds: 333, serverId: 5);

		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.SoloRecruitmentRemoval.Status);
		Assert.Null(plan.TeamRecruitmentAdd);
		Assert.NotNull(plan.FullTeamRecruitmentRemoval);
		var removal = plan.FullTeamRecruitmentRemoval!;
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, removal.Status);
		Assert.NotNull(removal.WorldBroadcastIntent);
		Assert.Equal(Convert.FromHexString("012923000005000000"), SerializeUnencryptedPayload(removal.WorldBroadcastIntent!.Packet));
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
	}

	[Fact]
	public void OnJoinedTeam_PlansInstanceGroupRemovalOnlyWhenMemberThresholdReached()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Applicant", "ELYOS", "RANGER", 45);
		var team = CreateTeamSubject(9001, "Leader", size: 3, minLevel: 40, maxLevel: 50);

		var belowThreshold = service.OnJoinedTeam(
			player,
			team,
			isLeader: false,
			isFull: false,
			nowEpochSeconds: 333,
			serverId: 5,
			new FindGroupInstanceGroupJoinState(player.ObjectId, MemberCount: 2, MinMembers: 3));
		var reachedThreshold = service.OnJoinedTeam(
			player,
			team,
			isLeader: false,
			isFull: false,
			nowEpochSeconds: 334,
			serverId: 5,
			new FindGroupInstanceGroupJoinState(player.ObjectId, MemberCount: 3, MinMembers: 3));

		Assert.False(belowThreshold.InstanceGroupRemoval.ShouldRemove);
		Assert.True(reachedThreshold.InstanceGroupRemoval.ShouldRemove);
		Assert.Equal("instanceGroups.remove(player.getObjectId()) when members >= minMembers", reachedThreshold.InstanceGroupRemoval.JavaSource);
	}

	[Fact]
	public void OnJoinedTeam_RemovesRegisteredInstanceGroupWhenCurrentTeamReachesMinMembersLikeJavaProxy()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(2001, "Applicant", "ELYOS", "RANGER", 45);
		service.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry", minMembers: 3, nowEpochSeconds: 222);
		var belowThresholdTeam = CreateTeamSubject(9001, "Leader", size: 2, minLevel: 40, maxLevel: 50);
		var reachedThresholdTeam = CreateTeamSubject(9001, "Leader", size: 3, minLevel: 40, maxLevel: 50);

		var belowThreshold = service.OnJoinedTeam(
			player,
			belowThresholdTeam,
			isLeader: false,
			isFull: false,
			nowEpochSeconds: 333,
			serverId: 5);
		var reachedThreshold = service.OnJoinedTeam(
			player,
			reachedThresholdTeam,
			isLeader: false,
			isFull: false,
			nowEpochSeconds: 334,
			serverId: 5);

		Assert.False(belowThreshold.InstanceGroupRemoval.ShouldRemove);
		Assert.Null(belowThreshold.InstanceGroupRemoval.RemovedInstanceGroup);
		Assert.True(reachedThreshold.InstanceGroupRemoval.ShouldRemove);
		Assert.NotNull(reachedThreshold.InstanceGroupRemoval.RemovedInstanceGroup);
		Assert.Equal(0x11223344, reachedThreshold.InstanceGroupRemoval.RemovedInstanceGroup!.InstanceMaskId);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 400).InstanceGroups);
	}

	[Fact]
	public void RegisterInstanceGroup_StoresEntryThenPlansJavaAction14Packet()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "RANGER", 65);

		var plan = service.RegisterInstanceGroup(
			player,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 3,
			nowEpochSeconds: 0x01020305);

		Assert.Equal(FindGroupInstanceGroupPlanStatus.Added, plan.Status);
		Assert.NotNull(plan.CurrentInstanceGroup);
		var state = plan.CurrentInstanceGroup!;
		Assert.Equal(0x01020304, state.RecruiterObjectId);
		Assert.Equal("ELYOS", state.Race);
		Assert.Equal(0x11223344, state.InstanceMaskId);
		Assert.Equal(3, state.MinMembers);
		Assert.Equal("Entry", state.Message);
		Assert.Equal(0x01020305, state.LastUpdate);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(0x01020304, direct.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))", direct.JavaSource);
		Assert.Equal(
			Convert.FromHexString(
				"0E0104030201443322110100000001030000040302010100010000000000414100000503020100000000520065006300720075006900740065007200000045006E007400720079000000"),
			SerializeUnencryptedPayload(direct.Packet));
		Assert.Null(plan.ShowInstanceGroupsPlan);
	}

	[Fact]
	public void ShowInstanceGroups_FiltersByRaceAndUsesJavaAction10Shape()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "RANGER", 65);
		var asmodian = CreatePlayer(0x01020306, "Other", "ASMODIANS", "RANGER", 60);
		service.RegisterInstanceGroup(player, 0x11223344, "Entry", minMembers: 3, nowEpochSeconds: 0x01020305);
		service.RegisterInstanceGroup(asmodian, 0x11223345, "Other", minMembers: 2, nowEpochSeconds: 0x01020306);

		var show = service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 0x01020305);

		Assert.Equal("ELYOS", show.Race);
		Assert.Equal(0x01020305, show.LastUpdate);
		var snapshot = Assert.Single(show.InstanceGroups);
		Assert.Equal(0x01020304, snapshot.GroupEntryId);
		Assert.Equal(0x11223344, snapshot.InstanceMaskId);
		Assert.Equal("Entry", snapshot.Message);
		Assert.Equal(
			Convert.FromHexString(
				"0A010001000503020104030201443322110100000001030000040302010100000000000000414100000503020100000000520065006300720075006900740065007200000045006E007400720079000000"),
			SerializeUnencryptedPayload(show.Packet));
	}

	[Fact]
	public void UpdateInstanceGroup_ExistingEntryMutatesMessageTimestampAndPlansShowList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "RANGER", 65);
		service.RegisterInstanceGroup(player, 0x11223344, "Old", minMembers: 3, nowEpochSeconds: 0x01020305);

		var plan = service.UpdateInstanceGroup(player, "New", nowEpochSeconds: 0x01020306);

		Assert.Equal(FindGroupInstanceGroupPlanStatus.Updated, plan.Status);
		Assert.NotNull(plan.CurrentInstanceGroup);
		Assert.Equal("New", plan.CurrentInstanceGroup!.Message);
		Assert.Equal(0x01020306, plan.CurrentInstanceGroup.LastUpdate);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.NotNull(plan.ShowInstanceGroupsPlan);
		var snapshot = Assert.Single(plan.ShowInstanceGroupsPlan!.InstanceGroups);
		Assert.Equal("New", snapshot.Message);
		Assert.Equal(0x01020306, snapshot.LastUpdate);
	}

	[Fact]
	public void RemoveInstanceGroup_RemovesExistingEntryThenPlansUpdatedShowList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var removedPlayer = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "RANGER", 65);
		var remainingPlayer = CreatePlayer(0x01020308, "Remaining", "ELYOS", "CLERIC", 55);
		service.RegisterInstanceGroup(removedPlayer, 0x11223344, "Removed", minMembers: 3, nowEpochSeconds: 0x01020305);
		service.RegisterInstanceGroup(remainingPlayer, 0x11223345, "Remaining", minMembers: 2, nowEpochSeconds: 0x01020305);

		var plan = service.RemoveInstanceGroup(removedPlayer, nowEpochSeconds: 0x01020306);

		Assert.Equal(FindGroupInstanceGroupPlanStatus.Removed, plan.Status);
		Assert.NotNull(plan.RemovedInstanceGroup);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.NotNull(plan.ShowInstanceGroupsPlan);
		var snapshot = Assert.Single(plan.ShowInstanceGroupsPlan!.InstanceGroups);
		Assert.Equal(0x01020308, snapshot.GroupEntryId);
		Assert.Equal("Remaining", snapshot.Message);
	}

	[Fact]
	public void ShowInstanceGroupMembersInfo_ExistingEntryPlansJavaAction16Packet()
	{
		var service = new FindGroupRecruitmentPlanService();
		var viewer = CreatePlayer(0x01020307, "Viewer", "ELYOS", "RANGER", 65);
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "GLADIATOR", 65);
		recruiter.Position = new WorldPosition(300110000, 0, 0, 0, 0);
		service.RegisterInstanceGroup(recruiter, 0x11223344, "Entry", minMembers: 3, nowEpochSeconds: 0x01020305);

		var plan = service.ShowInstanceGroupMembersInfo(viewer, recruiter.ObjectId, nowEpochSeconds: 0x01020305);

		Assert.Equal(FindGroupInstanceGroupPlanStatus.Shown, plan.Status);
		Assert.NotNull(plan.MemberInfo);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, direct.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(16, List.of(instanceGroup)))", direct.JavaSource);
		Assert.Equal(
			Convert.FromHexString("10010001000503020100000000B050E311040302014100000001000000010000005200650063007200750069007400650072000000"),
			SerializeUnencryptedPayload(direct.Packet));
	}

	[Fact]
	public void SendInstanceApplication_OnlineRecruiterPlansJavaAction11Packet()
	{
		var service = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS", "GLADIATOR", 65);

		var plan = service.SendInstanceApplication(applicant, recruiter);

		Assert.Equal(FindGroupInstanceApplicationPlanStatus.ApplicationSent, plan.Status);
		Assert.Null(plan.InviteIntent);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, direct.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(applicant))", direct.JavaSource);
		Assert.Equal(
			Convert.FromHexString("0B04030201000000000000000000000005410000004100700070006C006900630061006E0074000000"),
			SerializeUnencryptedPayload(direct.Packet));
	}

	[Fact]
	public void SendInstanceApplication_MissingRecruiterDoesNotPlanPacket()
	{
		var service = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);

		var plan = service.SendInstanceApplication(applicant, recruiter: null);

		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingRecipient, plan.Status);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.Null(plan.InviteIntent);
	}

	[Fact]
	public void ShowInstanceGroupsForPortal_PlansActionTwentySixOnlyWhenPortalMasksExist()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);

		var missingPortal = service.ShowInstanceGroupsForPortal(player, portalNpcInstanceMaskIds: null);
		var knownPortal = service.ShowInstanceGroupsForPortal(player, portalNpcInstanceMaskIds: [300110000, 300150000]);

		Assert.Null(missingPortal.EnabledInstanceMaskIds);
		Assert.Null(missingPortal.EnableRegisterForInstancesIntent);
		Assert.Equal([300110000, 300150000], knownPortal.EnabledInstanceMaskIds);
		Assert.NotNull(knownPortal.EnableRegisterForInstancesIntent);
		var intent = knownPortal.EnableRegisterForInstancesIntent!;
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(instanceMaskIds))", intent.JavaSource);
		Assert.Equal(
			SerializeUnencryptedPayload(SmFindGroup.EnableRegisterForInstances([300110000, 300150000])),
			SerializeUnencryptedPayload(intent.Packet));
	}

	[Fact]
	public void PrepareWindowPlansRouteToJavaEquivalentPacketsWithoutLiveDispatch()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020307, "Player", "ELYOS", "CLERIC", 65);
		var group = new FindGroupInstanceGroupWindowSnapshot(0x01020304, 0x11223344);
		var updateSnapshot = new FindGroupInstanceGroupPrepareWindowSnapshot(
			GroupEntryId: 0x01020304,
			InstanceMaskId: 0x11223344,
			Members:
			[
				new FindGroupInstanceGroupPrepareMemberSnapshot(
					PlayerObjectId: 0x01020307,
					Level: 65,
					ClassId: 10,
					IsOnline: true,
					Name: "Player")
			]);

		var enterButton = service.ShowEnterButtonInPrepareForEntryWindow(player, group);
		var showWindow = service.ShowPrepareForEntryWindow(player, group);
		var destroyWindow = service.DestroyPrepareForEntryWindow(player, group, showEnterInstanceMessage: true);
		var updateWindow = service.UpdatePrepareForEntryWindow(player, updateSnapshot);

		AssertPreparePlan(
			enterButton,
			FindGroupPrepareWindowPlanKind.ShowEnterButton,
			player.ObjectId,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(18, List.of(instanceGroup)))",
			SmFindGroup.ShowEnterButtonInPrepareForEntryWindow(group));
		AssertPreparePlan(
			showWindow,
			FindGroupPrepareWindowPlanKind.ShowPrepareWindow,
			player.ObjectId,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(22, List.of(instanceGroup)))",
			SmFindGroup.ShowPrepareForEntryWindow(group));
		AssertPreparePlan(
			destroyWindow,
			FindGroupPrepareWindowPlanKind.DestroyPrepareWindow,
			player.ObjectId,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(23, List.of(instanceGroup), showEnterInstanceMessage))",
			SmFindGroup.DestroyPrepareForEntryWindow(group, showEnterInstanceMessage: true));
		AssertPreparePlan(
			updateWindow,
			FindGroupPrepareWindowPlanKind.UpdatePrepareWindow,
			player.ObjectId,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(24, List.of(instanceGroup)))",
			SmFindGroup.UpdatePrepareForEntryWindow(updateSnapshot));
	}

	[Fact]
	public void OnLogout_RemovesOnlyPlayerObjectIdEntriesWithoutPackets()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);
		service.AddRecruitment(player, "Solo", groupType: 1, nowEpochSeconds: 100);
		service.AddApplication(player, "Apply", groupType: 2, classId: 5, level: 65, nowEpochSeconds: 101);
		service.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 102);

		var plan = service.OnLogout(player);

		Assert.Equal(player.ObjectId, plan.PlayerObjectId);
		Assert.NotNull(plan.RemovedRecruitment);
		Assert.NotNull(plan.RemovedApplication);
		Assert.NotNull(plan.RemovedInstanceGroup);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal(
			"recruitments.remove(player.getObjectId()); applications.remove(player.getObjectId()); instanceGroups.remove(player.getObjectId())",
			plan.JavaSource);
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 201).Applications);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 202).InstanceGroups);
	}

	[Fact]
	public void OnLogout_DoesNotRemoveTeamRecruitmentKeyedByTeamObjectId()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = WithTeam(CreatePlayer(0x01020304, "Leader", "ELYOS", "RANGER", 65), PlayerTeamMembership.Group, teamId: 0x01020309);
		service.AddRecruitment(
			player,
			"Team",
			groupType: 3,
			nowEpochSeconds: 100,
			CreateTeamSubject(0x01020309, "Leader", size: 3, minLevel: 60, maxLevel: 65));

		var plan = service.OnLogout(player);

		Assert.Null(plan.RemovedRecruitment);
		var remaining = Assert.Single(service.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
		Assert.Equal(0x01020309, remaining.ObjectId);
		Assert.False(remaining.IsSoloPlayer);
	}

	[Fact]
	public void ConcurrentMutations_UseJavaConcurrentHashMapStyleStateStores()
	{
		var service = new FindGroupRecruitmentPlanService();
		var players = Enumerable.Range(0, 64)
			.Select(index => CreatePlayer(0x01030000 + index, $"Player{index}", "ELYOS", "RANGER", 65))
			.ToArray();

		Parallel.ForEach(
			players,
			player =>
			{
				service.AddRecruitment(player, $"Recruit {player.ObjectId}", groupType: 1, nowEpochSeconds: player.ObjectId);
				service.AddApplication(player, $"Apply {player.ObjectId}", groupType: 2, classId: 5, level: 65, nowEpochSeconds: player.ObjectId);
				service.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: $"Entry {player.ObjectId}", minMembers: 6, nowEpochSeconds: player.ObjectId);
			});

		Assert.Equal(players.Length, service.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments.Count);
		Assert.Equal(players.Length, service.ShowApplications("ELYOS", nowEpochSeconds: 201).Applications.Count);
		Assert.Equal(players.Length, service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 202).InstanceGroups.Count);

		Parallel.ForEach(players, player => service.OnLogout(player));

		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 203).Recruitments);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 204).Applications);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 205).InstanceGroups);
	}

	[Fact]
	public void ShowPlans_ReturnMaterializedSnapshotsLikeJavaStreamToList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);
		service.AddRecruitment(player, "Recruit original", groupType: 1, nowEpochSeconds: 100);
		service.AddApplication(player, "Apply original", groupType: 2, classId: 5, level: 65, nowEpochSeconds: 101);
		service.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry original", minMembers: 6, nowEpochSeconds: 102);

		var recruitmentShow = service.ShowRecruitments("ELYOS", nowEpochSeconds: 200);
		var applicationShow = service.ShowApplications("ELYOS", nowEpochSeconds: 201);
		var instanceGroupShow = service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 202);

		service.UpdateRecruitment(player, "Recruit changed", groupType: 3, nowEpochSeconds: 300);
		service.UpdateApplication(player, "Apply changed", groupType: 4, classId: 10, level: 66, nowEpochSeconds: 301);
		service.UpdateInstanceGroup(player, "Entry changed", nowEpochSeconds: 302);
		service.OnLogout(player);

		var recruitment = Assert.Single(recruitmentShow.Recruitments);
		Assert.Equal("Recruit original", recruitment.Message);
		Assert.Equal(1, recruitment.GroupType);
		Assert.Equal(100, recruitment.LastUpdate);
		var application = Assert.Single(applicationShow.Applications);
		Assert.Equal("Apply original", application.Message);
		Assert.Equal(2, application.GroupType);
		Assert.Equal(101, application.LastUpdate);
		var instanceGroup = Assert.Single(instanceGroupShow.InstanceGroups);
		Assert.Equal("Entry original", instanceGroup.Message);
		Assert.Equal(102, instanceGroup.LastUpdate);
		Assert.Empty(service.ShowRecruitments("ELYOS", nowEpochSeconds: 400).Recruitments);
		Assert.Empty(service.ShowApplications("ELYOS", nowEpochSeconds: 401).Applications);
		Assert.Empty(service.ShowInstanceGroups("ELYOS", nowEpochSeconds: 402).InstanceGroups);
	}

	[Fact]
	public void SendInstanceApplicationResult_AcceptPlansGroupInviteWhenMinMembersAtMostSix()
	{
		var service = new FindGroupRecruitmentPlanService();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS", "GLADIATOR", 65);
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		service.RegisterInstanceGroup(responder, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 0x01020305);

		var plan = service.SendInstanceApplicationResult(responder, applicant, applicant.ObjectId, instanceApplicationReply: 1);

		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedGroupInvite, plan.Status);
		Assert.Empty(plan.DirectPacketIntents);
		Assert.NotNull(plan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Group, plan.InviteIntent!.Kind);
		Assert.Equal(responder.ObjectId, plan.InviteIntent.InviterObjectId);
		Assert.Equal(applicant.ObjectId, plan.InviteIntent.InvitedObjectId);
		Assert.Equal("PlayerGroupService.inviteToGroup(responder, applicant)", plan.InviteIntent.JavaSource);
	}

	[Fact]
	public void SendInstanceApplicationResult_AcceptPlansAllianceInviteWhenMinMembersAboveSix()
	{
		var service = new FindGroupRecruitmentPlanService();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS", "GLADIATOR", 65);
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		service.RegisterInstanceGroup(responder, instanceMaskId: 0x11223344, message: "Entry", minMembers: 7, nowEpochSeconds: 0x01020305);

		var plan = service.SendInstanceApplicationResult(responder, applicant, applicant.ObjectId, instanceApplicationReply: 1);

		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedAllianceInvite, plan.Status);
		Assert.NotNull(plan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Alliance, plan.InviteIntent!.Kind);
		Assert.Equal("PlayerAllianceService.inviteToAlliance(responder, applicant)", plan.InviteIntent.JavaSource);
	}

	[Fact]
	public void SendInstanceApplicationResult_DeclinePlansLocalizedWhisperWithJavaPacketPayload()
	{
		var service = new FindGroupRecruitmentPlanService();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS", "GLADIATOR", 65);
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);

		var plan = service.SendInstanceApplicationResult(responder, applicant, applicant.ObjectId, instanceApplicationReply: 0);

		Assert.Equal(FindGroupInstanceApplicationPlanStatus.Declined, plan.Status);
		Assert.Null(plan.InviteIntent);
		var direct = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(applicant.ObjectId, direct.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(applicant, new SM_MESSAGE(responder, ChatUtil.l10n(1400217), ChatType.WHISPER))", direct.JavaSource);
		Assert.Equal(
			// Java SM_MESSAGE.writeImpl: chatType, active-player race filter, sender id, sender name, localized message.
			Convert.FromHexString("04010703020152006500730070006F006E006400650072000000240033BB2A000000"),
			SerializeUnencryptedPayload(direct.Packet));
	}

	[Fact]
	public void SendInstanceApplicationResult_AcceptMissingStateDoesNotPlanInvite()
	{
		var service = new FindGroupRecruitmentPlanService();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS", "GLADIATOR", 65);
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);

		var missingApplicant = service.SendInstanceApplicationResult(responder, null, applicant.ObjectId, instanceApplicationReply: 1);
		var missingInstanceGroup = service.SendInstanceApplicationResult(responder, applicant, applicant.ObjectId, instanceApplicationReply: 1);

		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingApplicant, missingApplicant.Status);
		Assert.Empty(missingApplicant.DirectPacketIntents);
		Assert.Null(missingApplicant.InviteIntent);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingInstanceGroup, missingInstanceGroup.Status);
		Assert.Empty(missingInstanceGroup.DirectPacketIntents);
		Assert.Null(missingInstanceGroup.InviteIntent);
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

	private static Player WithTeam(Player player, PlayerTeamMembership membership, int teamId)
	{
		player.TeamMembership = membership;
		player.CurrentTeamId = teamId;
		return player;
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

	private static void AssertPreparePlan(
		FindGroupPrepareWindowPlan plan,
		FindGroupPrepareWindowPlanKind expectedKind,
		int expectedRecipientObjectId,
		string expectedJavaSource,
		GameServerPacket expectedPacket)
	{
		Assert.Equal(expectedKind, plan.Kind);
		Assert.False(plan.DispatchLiveSideEffects);
		var intent = Assert.Single(plan.DirectPacketIntents);
		Assert.Equal(expectedRecipientObjectId, intent.RecipientObjectId);
		Assert.Equal(expectedJavaSource, intent.JavaSource);
		Assert.Equal(SerializeUnencryptedPayload(expectedPacket), SerializeUnencryptedPayload(intent.Packet));
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
