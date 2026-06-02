using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupInstanceApplicationDirectDispatchPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_ActionElevenDirectPacketIntentIsPlannedWithoutLiveDispatch()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020304, "Applicant");
		var recruiter = CreatePlayer(0x01020307, "Recruiter");
		var applicationPlan = findGroupService.SendInstanceApplication(applicant, recruiter);

		var plan = FindGroupInstanceApplicationDirectDispatchPlanService.CreateDisabledPlan(
			applicationPlan,
			Resolve(applicant, recruiter));

		Assert.Equal(FindGroupInstanceApplicationDirectDispatchStatus.DirectPacketPlanned, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Same(applicationPlan, plan.ApplicationPlan);
		Assert.Empty(plan.MissingRecipientObjectIds);
		var direct = Assert.Single(plan.DirectPackets);
		Assert.Equal(recruiter.ObjectId, direct.RecipientObjectId);
		Assert.Equal("SmFindGroup", direct.PacketType);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(applicant))", direct.JavaSource);
	}

	[Fact]
	public void CreateDisabledPlan_DeclinedActionTwelveWhisperIntentIsPlannedWithoutLiveDispatch()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var responder = CreatePlayer(0x01020307, "Responder");
		var applicant = CreatePlayer(0x01020304, "Applicant");
		var applicationPlan = findGroupService.SendInstanceApplicationResult(
			responder,
			applicant,
			applicant.ObjectId,
			instanceApplicationReply: 0);

		var plan = FindGroupInstanceApplicationDirectDispatchPlanService.CreateDisabledPlan(
			applicationPlan,
			Resolve(responder, applicant));

		Assert.Equal(FindGroupInstanceApplicationDirectDispatchStatus.DirectPacketPlanned, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.Declined, applicationPlan.Status);
		var direct = Assert.Single(plan.DirectPackets);
		Assert.Equal(applicant.ObjectId, direct.RecipientObjectId);
		Assert.Equal("SmMessage", direct.PacketType);
		Assert.Equal(
			"PacketSendUtility.sendPacket(applicant, new SM_MESSAGE(responder, ChatUtil.l10n(1400217), ChatType.WHISPER))",
			direct.JavaSource);
	}

	[Fact]
	public void CreateDisabledPlan_MissingRecipientSkipsWithoutLiveDispatch()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020304, "Applicant");
		var recruiter = CreatePlayer(0x01020307, "Recruiter");
		var applicationPlan = findGroupService.SendInstanceApplication(applicant, recruiter);

		var plan = FindGroupInstanceApplicationDirectDispatchPlanService.CreateDisabledPlan(
			applicationPlan,
			Resolve(applicant));

		Assert.Equal(FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingRecipient, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal([recruiter.ObjectId], plan.MissingRecipientObjectIds);
		Assert.Empty(plan.DirectPackets);
	}

	[Fact]
	public void CreateDisabledPlan_DeclinedActionTwelveMissingApplicantRecipientRecordsFailureWithoutLiveDispatch()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var responder = CreatePlayer(0x01020307, "Responder");
		var applicant = CreatePlayer(0x01020304, "Applicant");
		var applicationPlan = findGroupService.SendInstanceApplicationResult(
			responder,
			applicant,
			applicant.ObjectId,
			instanceApplicationReply: 0);

		var plan = FindGroupInstanceApplicationDirectDispatchPlanService.CreateDisabledPlan(
			applicationPlan,
			Resolve(responder));

		Assert.Equal(FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingRecipient, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Same(applicationPlan, plan.ApplicationPlan);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.Declined, plan.ApplicationPlan!.Status);
		Assert.Equal([applicant.ObjectId], plan.MissingRecipientObjectIds);
		Assert.Empty(plan.DirectPackets);
	}

	[Fact]
	public void CreateDisabledPlan_MissingDirectPacketIntentSkipsWithoutLiveDispatch()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020304, "Applicant");
		var applicationPlan = findGroupService.SendInstanceApplication(applicant, recruiter: null);

		var plan = FindGroupInstanceApplicationDirectDispatchPlanService.CreateDisabledPlan(
			applicationPlan,
			Resolve(applicant));

		Assert.Equal(FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingDirectPacketIntent, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Empty(plan.DirectPackets);
	}

	[Fact]
	public void CreateDisabledPlan_MissingPlanSkipsWithoutLiveDispatch()
	{
		var plan = FindGroupInstanceApplicationDirectDispatchPlanService.CreateDisabledPlan(
			applicationPlan: null,
			_ => null);

		Assert.Equal(FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingPlan, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Empty(plan.DirectPackets);
	}

	private static Func<int, Player?> Resolve(params Player[] players)
	{
		return objectId => players.FirstOrDefault(player => player.ObjectId == objectId);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 65,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}
}
