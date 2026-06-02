using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupConnectionBoundaryDispatchAdapterServiceTests
{
	[Fact]
	public void CreateDisabledPlan_ActionZeroComposesDirectPacketIntentWithoutLiveDispatch()
	{
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(viewer, "Need healer", groupType: 2, nowEpochSeconds: 100);
		var compositionPlan = CreateCompositionPlan(
			findGroupService,
			viewer,
			CreateFindGroupPacket(buffer => buffer.WriteC(0)));

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(0, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowRecruitments, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Null(plan.InvitePlan);
	}

	[Fact]
	public void CreateDisabledPlan_ActionOneComposesWorldBroadcastIntentWithoutLiveDispatch()
	{
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(recruiter, "Need healer", groupType: 2, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(1);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteC(5);
				buffer.WriteC(6);
				buffer.WriteC(7);
				buffer.WriteC(8);
			});
		var compositionPlan = CreateCompositionPlan(findGroupService, recruiter, packet);

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		var broadcast = Assert.Single(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal(nameof(SmFindGroup), broadcast.Packet.GetType().Name);
		Assert.Null(plan.InvitePlan);
	}

	[Fact]
	public void CreateDisabledPlan_ActionThreeRecordsNoSideEffects()
	{
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(recruiter, "Old", groupType: 2, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(3);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteC(5);
				buffer.WriteC(6);
				buffer.WriteC(7);
				buffer.WriteC(8);
				buffer.WriteS("New");
				buffer.WriteC(4);
			});
		var compositionPlan = CreateCompositionPlan(findGroupService, recruiter, packet);

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateRecruitment, plan.IntentPlan.ClientActionKind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Null(plan.InvitePlan);
		var snapshot = Assert.Single(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 101).Recruitments);
		Assert.Equal("New", snapshot.Message);
	}

	[Fact]
	public void CreateDisabledPlan_ActionTwentyPreservesParsedButNoRunImplNoOp()
	{
		var player = CreatePlayer(0x01020304, "Player", "ELYOS");
		var compositionPlan = CreateCompositionPlan(
			new FindGroupRecruitmentPlanService(),
			player,
			CreateFindGroupPacket(buffer => buffer.WriteC(20)));

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ParsedButNoJavaRunImpl, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(20, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ParsedButNoRunImpl, plan.IntentPlan.ClientActionKind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Null(plan.InvitePlan);
	}

	[Fact]
	public void CreateDisabledPlan_MissingActivePlayerRecordsBlockedBoundaryWithoutLiveDispatch()
	{
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));
		var compositionPlan = CreateCompositionPlan(
			new FindGroupRecruitmentPlanService(),
			activePlayer: null,
			packet);

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.SkippedMissingActivePlayer, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Null(plan.IntentPlan.ClientActionKind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Null(plan.InvitePlan);
	}

	[Fact]
	public void CreateDisabledPlan_ActionTwelveInviteWithoutRuntimeRecordsBlockedMissingRuntime()
	{
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(responder, 0x11223344, "Entry", minMembers: 6, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});
		var compositionPlan = CreateCompositionPlan(
			findGroupService,
			responder,
			packet,
			Resolve(responder, applicant));

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.BlockedMissingInviteRuntime, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.NotNull(plan.IntentPlan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Group, plan.IntentPlan.InviteIntent!.Kind);
		Assert.Null(plan.InvitePlan);
	}

	[Fact]
	public void CreateDisabledPlan_ActionTwelveInviteWithRuntimeComposesDisabledInvitePlan()
	{
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(responder, 0x11223344, "Entry", minMembers: 6, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});
		var resolver = Resolve(responder, applicant);
		var compositionPlan = CreateCompositionPlan(findGroupService, responder, packet, resolver);

		var plan = new FindGroupConnectionBoundaryDispatchAdapterService().CreateDisabledPlan(
			compositionPlan,
			resolver,
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.NotNull(plan.IntentPlan.InviteIntent);
		Assert.NotNull(plan.InvitePlan);
		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.GroupInvitePlanned, plan.InvitePlan!.Status);
		Assert.Equal(GroupInviteRequestStatus.Requested, plan.InvitePlan.GroupInviteRequest?.Status);
		Assert.Equal(SmQuestionWindow.PartyInvite, plan.InvitePlan.GroupInviteRequest?.QuestionWindow?.Code);
	}

	private static FindGroupConnectionClientActionCompositionPlan CreateCompositionPlan(
		FindGroupRecruitmentPlanService findGroupService,
		Player? activePlayer,
		CmFindGroup packet,
		Func<int, Player?>? resolvePlayer = null)
	{
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		return compositionService.CreateDisabledPlan(
			activePlayer,
			packet,
			nowEpochSeconds: 0x01020305,
			resolvePlayer);
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		var packet = GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(77, writePayload),
			GameConnectionState.InGame);
		return Assert.IsType<CmFindGroup>(packet);
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static Player CreatePlayer(int objectId, string name, string race)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = "RANGER",
			Level = 65,
			Position = new WorldPosition(210010000, 11, 22, 33, 0),
		};
	}

	private static Func<int, Player?> Resolve(params Player[] players)
	{
		return objectId => players.FirstOrDefault(player => player.ObjectId == objectId);
	}
}
