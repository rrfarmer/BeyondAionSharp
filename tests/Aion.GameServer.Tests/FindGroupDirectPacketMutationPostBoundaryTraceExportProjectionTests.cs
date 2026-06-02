using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionTests
{
	[Fact]
	public void CreateExportFromDisabledPlan_ProjectsActionTwoRecruitmentMutationPost()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "CLERIC", 65);
		var hidden = CreatePlayer(0x01020306, "Hidden", "ASMODIANS", "RANGER", 61);
		findGroupService.AddRecruitment(hidden, "Hidden entry", groupType: 4, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});
		var compositionPlan = CreateCompositionPlan(findGroupService, recruiter, packet, nowEpochSeconds: 200);

		var projection = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.Created, projection.Status);
		Assert.Contains("disabled CM_FIND_GROUP boundary plan", projection.Reason, StringComparison.Ordinal);
		var export = projection.Export;
		Assert.Equal(1, export.SchemaVersion);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", export.TraceName);
		Assert.Equal(FindGroupDirectPacketMutationPostTraceSource.CSharp, export.TraceSource);
		Assert.Equal(2, export.Action);
		Assert.True(export.BoundaryAccepted);
		Assert.Equal(recruiter.ObjectId, export.ActivePlayerObjectId);
		Assert.Equal("ELYOS", export.ActivePlayerRace);
		Assert.Equal(200, export.ServerEpochSeconds);
		Assert.Equal(FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, export.MutationKind);
		Assert.Equal(recruiter.ObjectId, export.MutatedEntryObjectId);
		Assert.True(export.StateMutationRecordedBeforeDirectPackets);
		Assert.Equal(recruiter.ObjectId, export.PostedSystemMessageRecipientObjectId);
		Assert.Equal("SmSystemMessage", export.PostedSystemMessageType);
		Assert.Equal(1400392, export.PostedSystemMessageId);
		Assert.Equal(recruiter.ObjectId, export.RefreshedListRecipientObjectId);
		Assert.Equal("SmFindGroup", export.RefreshedListPacketType);
		Assert.Equal(0, export.RefreshedListAction);
		Assert.Equal([recruiter.ObjectId], export.VisibleEntryObjectIdsAfterMutation);
		Assert.False(export.ExecutorInvokedFromBoundary);
		Assert.False(export.RegistrySendsObservedInOrder);
		Assert.Equal(0, export.WorldBroadcastCount);
		Assert.Equal(0, export.InviteDispatchCount);
	}

	[Fact]
	public void CreateExportFromDisabledPlan_ProjectsActionSixApplicationMutationPost()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		var hidden = CreatePlayer(0x01020306, "Hidden", "ASMODIANS", "CLERIC", 61);
		findGroupService.AddApplication(hidden, "Hidden app", groupType: 2, classId: 10, level: 61, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(6);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need group");
				buffer.WriteC(2);
				buffer.WriteC(5);
				buffer.WriteC(65);
			});
		var compositionPlan = CreateCompositionPlan(findGroupService, applicant, packet, nowEpochSeconds: 201);

		var projection = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.Created, projection.Status);
		var export = projection.Export;
		Assert.Equal(6, export.Action);
		Assert.Equal(201, export.ServerEpochSeconds);
		Assert.Equal(FindGroupDirectPacketMutationPostTraceMutationKind.Application, export.MutationKind);
		Assert.Equal(applicant.ObjectId, export.MutatedEntryObjectId);
		Assert.True(export.StateMutationRecordedBeforeDirectPackets);
		Assert.Equal(applicant.ObjectId, export.PostedSystemMessageRecipientObjectId);
		Assert.Equal("SmSystemMessage", export.PostedSystemMessageType);
		Assert.Equal(1400393, export.PostedSystemMessageId);
		Assert.Equal(applicant.ObjectId, export.RefreshedListRecipientObjectId);
		Assert.Equal("SmFindGroup", export.RefreshedListPacketType);
		Assert.Equal(4, export.RefreshedListAction);
		Assert.Equal([applicant.ObjectId], export.VisibleEntryObjectIdsAfterMutation);
		Assert.False(export.ExecutorInvokedFromBoundary);
		Assert.False(export.RegistrySendsObservedInOrder);
		Assert.Equal(0, export.WorldBroadcastCount);
		Assert.Equal(0, export.InviteDispatchCount);
	}

	[Fact]
	public void CreateExportFromDisabledPlan_RejectsUnsupportedShowListAction()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "CLERIC", 65);
		var compositionPlan = CreateCompositionPlan(
			findGroupService,
			player,
			CreateFindGroupPacket(buffer => buffer.WriteC(0)),
			nowEpochSeconds: 202);

		var projection = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.UnsupportedAction, projection.Status);
		Assert.Equal(0, projection.Export.Action);
		Assert.Contains("actions 2 and 6", projection.Reason, StringComparison.Ordinal);
		Assert.False(projection.Export.BoundaryAccepted);
	}

	[Fact]
	public void CreateExportFromDisabledPlan_RejectsMissingActivePlayer()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});
		var compositionPlan = new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(findGroupService))
			.CreateDisabledPlan(activePlayer: null, packet, nowEpochSeconds: 203);

		var projection = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.MissingActivePlayer, projection.Status);
		Assert.Equal(2, projection.Export.Action);
		Assert.Contains("active player", projection.Reason, StringComparison.Ordinal);
		Assert.False(projection.Export.BoundaryAccepted);
	}

	private static FindGroupConnectionClientActionCompositionPlan CreateCompositionPlan(
		FindGroupRecruitmentPlanService findGroupService,
		Player player,
		CmFindGroup packet,
		int nowEpochSeconds)
	{
		return new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(findGroupService))
			.CreateDisabledPlan(player, packet, nowEpochSeconds);
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		writePayload(buffer);
		var packet = new CmFindGroup(opCode: 0, validStates: new HashSet<GameConnectionState>());
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
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
