using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupClientActionPlanServiceTests
{
	[Fact]
	public void Plan_RoutesRecruitmentActionsZeroThroughThreeToJavaEquivalentPlannerCalls()
	{
		var service = CreateService();
		var player = CreatePlayer(2001, "Recruiter", "ELYOS", "GLADIATOR", 50);

		var showEmpty = service.Plan(player, new FindGroupClientAction(0), nowEpochSeconds: 100);
		var add = service.Plan(
			player,
			new FindGroupClientAction(2, PlayerOrTeamId: 7001, Message: "Need healer", GroupType: 3),
			nowEpochSeconds: 101);
		var update = service.Plan(
			player,
			new FindGroupClientAction(
				3,
				PlayerOrTeamId: 7001,
				Message: "Need tank",
				GroupType: 4,
				ServerId: 9,
				Unknown1: 8,
				Unknown2: 7,
				Unknown3: 6),
			nowEpochSeconds: 102);
		var remove = service.Plan(
			player,
			new FindGroupClientAction(1, PlayerOrTeamId: 7001, ServerId: 9, Unknown1: 8, Unknown2: 7, Unknown3: 6),
			nowEpochSeconds: 103);

		Assert.False(showEmpty.DispatchLiveSideEffects);
		Assert.Equal(FindGroupClientActionPlanKind.ShowRecruitments, showEmpty.Kind);
		Assert.NotNull(showEmpty.RecruitmentShowPlan);
		Assert.Empty(showEmpty.RecruitmentShowPlan!.Recruitments);
		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, add.Kind);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Added, add.RecruitmentMutationPlan!.Status);
		Assert.Equal("Need healer", add.RecruitmentMutationPlan.CurrentRecruitment!.Message);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateRecruitment, update.Kind);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Updated, update.RecruitmentMutationPlan!.Status);
		Assert.Equal("Need tank", update.RecruitmentMutationPlan.CurrentRecruitment!.Message);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveRecruitment, remove.Kind);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, remove.RecruitmentMutationPlan!.Status);
		Assert.NotNull(remove.RecruitmentMutationPlan.WorldBroadcastIntent);
	}

	[Fact]
	public void Plan_RoutesApplicationActionsFourThroughSevenToJavaEquivalentPlannerCalls()
	{
		var service = CreateService();
		var player = CreatePlayer(3001, "Applicant", "ELYOS", "RANGER", 45);

		var showEmpty = service.Plan(player, new FindGroupClientAction(4), nowEpochSeconds: 200);
		var add = service.Plan(
			player,
			new FindGroupClientAction(6, PlayerOrTeamId: 99, Message: "LFG", GroupType: 2, ClassId: 5, Level: 45),
			nowEpochSeconds: 201);
		var update = service.Plan(
			player,
			new FindGroupClientAction(7, PlayerOrTeamId: 99, Message: "Still LFG", GroupType: 6, ClassId: 5, Level: 46),
			nowEpochSeconds: 202);
		var remove = service.Plan(player, new FindGroupClientAction(5, PlayerOrTeamId: 99), nowEpochSeconds: 203);

		Assert.Equal(FindGroupClientActionPlanKind.ShowApplications, showEmpty.Kind);
		Assert.NotNull(showEmpty.ApplicationShowPlan);
		Assert.Empty(showEmpty.ApplicationShowPlan!.Applications);
		Assert.Equal(FindGroupClientActionPlanKind.AddApplication, add.Kind);
		Assert.Equal(FindGroupApplicationPlanStatus.Added, add.ApplicationMutationPlan!.Status);
		Assert.Equal("LFG", add.ApplicationMutationPlan.CurrentApplication!.Message);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateApplication, update.Kind);
		Assert.Equal(FindGroupApplicationPlanStatus.Updated, update.ApplicationMutationPlan!.Status);
		Assert.Equal("Still LFG", update.ApplicationMutationPlan.CurrentApplication!.Message);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveApplication, remove.Kind);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, remove.ApplicationMutationPlan!.Status);
		Assert.NotNull(remove.ApplicationMutationPlan.WorldBroadcastIntent);
	}

	[Fact]
	public void Plan_RoutesInstanceGroupActionsEightNineTenThirteenFifteenAndSeventeen()
	{
		var service = CreateService();
		var player = CreatePlayer(4001, "Recruiter", "ELYOS", "CLERIC", 65);

		var register = service.Plan(
			player,
			new FindGroupClientAction(8, InstanceMaskId: 0x11223344, Message: "Entry", MinMembers: 6),
			nowEpochSeconds: 300);
		var update = service.Plan(
			player,
			new FindGroupClientAction(17, PlayerOrTeamId: 999, InstanceMaskId: 0x11223344, Message: "Updated"),
			nowEpochSeconds: 301);
		var show = service.Plan(player, new FindGroupClientAction(10), nowEpochSeconds: 302);
		var showUpdate = service.Plan(player, new FindGroupClientAction(13), nowEpochSeconds: 303);
		var memberInfo = service.Plan(
			player,
			new FindGroupClientAction(15, PlayerOrTeamId: player.ObjectId, InstanceMaskId: 0x11223344),
			nowEpochSeconds: 304);
		var remove = service.Plan(
			player,
			new FindGroupClientAction(9, PlayerOrTeamId: player.ObjectId, InstanceMaskId: 0x11223344),
			nowEpochSeconds: 305);

		Assert.Equal(FindGroupClientActionPlanKind.RegisterInstanceGroup, register.Kind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Added, register.InstanceGroupMutationPlan!.Status);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateInstanceGroup, update.Kind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Updated, update.InstanceGroupMutationPlan!.Status);
		Assert.Equal("Updated", update.InstanceGroupMutationPlan.CurrentInstanceGroup!.Message);
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroups, show.Kind);
		Assert.Single(show.InstanceGroupShowPlan!.InstanceGroups);
		Assert.NotNull(show.InstanceGroupClientShowPlan);
		Assert.Null(show.InstanceGroupClientShowPlan!.EnableRegisterForInstancesIntent);
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroupsUpdate, showUpdate.Kind);
		Assert.Single(showUpdate.InstanceGroupShowPlan!.InstanceGroups);
		Assert.NotNull(showUpdate.InstanceGroupClientShowPlan);
		Assert.True(showUpdate.InstanceGroupClientShowPlan!.IsUpdate);
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroupMembersInfo, memberInfo.Kind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Shown, memberInfo.InstanceGroupMemberInfoPlan!.Status);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveInstanceGroup, remove.Kind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Removed, remove.InstanceGroupMutationPlan!.Status);
	}

	[Fact]
	public void Plan_ActionTenPlansMaskListOnlyForNonUpdateWhenFormAnywhereIsEnabled()
	{
		var service = CreateService();
		var player = CreatePlayer(4101, "Recruiter", "ELYOS", "CLERIC", 65);
		service.Plan(
			player,
			new FindGroupClientAction(8, InstanceMaskId: 0x11223344, Message: "Entry", MinMembers: 6),
			nowEpochSeconds: 350);

		var nonUpdate = service.Plan(
			player,
			new FindGroupClientAction(10),
			nowEpochSeconds: 351,
			formInstanceGroupAnywhere: true,
			targetNpcInstanceMaskIds: [300110000],
			allRecruitableInstanceMaskIds: [300110000, 300150000]);
		var update = service.Plan(
			player,
			new FindGroupClientAction(13),
			nowEpochSeconds: 352,
			formInstanceGroupAnywhere: true,
			targetNpcInstanceMaskIds: [300110000],
			allRecruitableInstanceMaskIds: [300110000, 300150000]);
		var fallback = service.Plan(
			player,
			new FindGroupClientAction(10),
			nowEpochSeconds: 353,
			formInstanceGroupAnywhere: true,
			targetNpcInstanceMaskIds: null,
			allRecruitableInstanceMaskIds: [300110000, 300150000]);

		Assert.NotNull(nonUpdate.InstanceGroupClientShowPlan);
		Assert.False(nonUpdate.InstanceGroupClientShowPlan!.IsUpdate);
		Assert.True(nonUpdate.InstanceGroupClientShowPlan.FormInstanceGroupAnywhere);
		Assert.Equal([300110000], nonUpdate.InstanceGroupClientShowPlan.EnabledInstanceMaskIds);
		var maskIntent = Assert.IsType<FindGroupDirectPacketIntent>(nonUpdate.InstanceGroupClientShowPlan.EnableRegisterForInstancesIntent);
		Assert.Equal(player.ObjectId, maskIntent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(instanceMaskIds))", maskIntent.JavaSource);
		Assert.Single(nonUpdate.InstanceGroupShowPlan!.InstanceGroups);
		Assert.NotNull(update.InstanceGroupClientShowPlan);
		Assert.Null(update.InstanceGroupClientShowPlan!.EnabledInstanceMaskIds);
		Assert.Null(update.InstanceGroupClientShowPlan.EnableRegisterForInstancesIntent);
		Assert.Equal([300110000, 300150000], fallback.InstanceGroupClientShowPlan!.EnabledInstanceMaskIds);
	}

	[Fact]
	public void Plan_RoutesInstanceApplicationActionsElevenAndTwelveThroughResolver()
	{
		var service = CreateService();
		var recruiter = CreatePlayer(5001, "Recruiter", "ELYOS", "TEMPLAR", 65);
		var applicant = CreatePlayer(5002, "Applicant", "ELYOS", "RANGER", 63);
		var players = new Dictionary<int, Player>
		{
			[recruiter.ObjectId] = recruiter,
			[applicant.ObjectId] = applicant,
		};
		Player? Resolve(int objectId) => players.GetValueOrDefault(objectId);
		service.Plan(
			recruiter,
			new FindGroupClientAction(8, InstanceMaskId: 0x11223344, Message: "Entry", MinMembers: 6),
			nowEpochSeconds: 400);

		var application = service.Plan(
			applicant,
			new FindGroupClientAction(11, PlayerOrTeamId: recruiter.ObjectId, InstanceMaskId: 0x11223344),
			nowEpochSeconds: 401,
			resolvePlayer: Resolve);
		var result = service.Plan(
			recruiter,
			new FindGroupClientAction(12, PlayerOrTeamId: applicant.ObjectId, InstanceApplicationReply: 1),
			nowEpochSeconds: 402,
			resolvePlayer: Resolve);

		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplication, application.Kind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.ApplicationSent, application.InstanceApplicationPlan!.Status);
		Assert.Equal(recruiter.ObjectId, Assert.Single(application.InstanceApplicationPlan.DirectPacketIntents).RecipientObjectId);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, result.Kind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedGroupInvite, result.InstanceApplicationPlan!.Status);
		Assert.NotNull(result.InstanceApplicationPlan.InviteIntent);
		Assert.Equal(applicant.ObjectId, result.InstanceApplicationPlan.InviteIntent!.InvitedObjectId);
	}

	[Fact]
	public void Plan_DocumentsParsedActionsWithoutJavaRunImplAndUnknownActionsAsNonDispatching()
	{
		var service = CreateService();
		var player = CreatePlayer(6001, "Player", "ELYOS", "GLADIATOR", 55);

		var enterPrepareWindow = service.Plan(player, new FindGroupClientAction(20), nowEpochSeconds: 500);
		var ban = service.Plan(
			player,
			new FindGroupClientAction(25, PlayerOrTeamId: 1, InstanceMaskId: 2, BannedPlayerId: 3),
			nowEpochSeconds: 501);
		var unknown = service.Plan(player, new FindGroupClientAction(99), nowEpochSeconds: 502);

		Assert.Equal(FindGroupClientActionPlanKind.ParsedButNoRunImpl, enterPrepareWindow.Kind);
		Assert.False(enterPrepareWindow.DispatchLiveSideEffects);
		Assert.Equal(FindGroupClientActionPlanKind.ParsedButNoRunImpl, ban.Kind);
		Assert.False(ban.DispatchLiveSideEffects);
		Assert.Equal(FindGroupClientActionPlanKind.UnknownAction, unknown.Kind);
		Assert.False(unknown.DispatchLiveSideEffects);
	}

	private static FindGroupClientActionPlanService CreateService()
	{
		return new FindGroupClientActionPlanService(new FindGroupRecruitmentPlanService());
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
}
