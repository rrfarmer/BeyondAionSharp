using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketShowListBoundaryTraceExportProjectionTests
{
	[Fact]
	public void CreateExportFromDisabledPlan_ProjectsActionZeroRecruitmentShowList()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS", "CLERIC", 65);
		var visible = CreatePlayer(0x01020305, "Visible", "ELYOS", "GLADIATOR", 60);
		var hidden = CreatePlayer(0x01020306, "Hidden", "ASMODIANS", "RANGER", 61);
		findGroupService.AddRecruitment(visible, "Visible entry", groupType: 3, nowEpochSeconds: 100);
		findGroupService.AddRecruitment(hidden, "Hidden entry", groupType: 4, nowEpochSeconds: 100);
		var compositionPlan = CreateCompositionPlan(findGroupService, viewer, action: 0, nowEpochSeconds: 200);

		var projection = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketShowListBoundaryTraceExportProjectionStatus.Created, projection.Status);
		Assert.Contains("disabled CM_FIND_GROUP boundary plan", projection.Reason, StringComparison.Ordinal);
		var export = projection.Export;
		Assert.Equal(1, export.SchemaVersion);
		Assert.Equal("cm-find-group-direct-show-list-boundary", export.TraceName);
		Assert.Equal(FindGroupDirectPacketShowListTraceSource.CSharp, export.TraceSource);
		Assert.Equal(0, export.Action);
		Assert.True(export.BoundaryAccepted);
		Assert.Equal(viewer.ObjectId, export.ActivePlayerObjectId);
		Assert.Equal("ELYOS", export.ActivePlayerRace);
		Assert.Equal(200, export.ServerEpochSeconds);
		Assert.Equal(FindGroupDirectPacketShowListTraceListKind.Recruitments, export.ListKind);
		Assert.Equal([visible.ObjectId], export.VisibleEntryObjectIds);
		Assert.Equal(viewer.ObjectId, export.DirectPacketRecipientObjectId);
		Assert.Equal("SmFindGroup", export.DirectPacketType);
		Assert.Equal(0, export.DirectPacketAction);
		Assert.False(export.ExecutorInvokedFromBoundary);
		Assert.False(export.RegistrySendObserved);
		Assert.Equal(0, export.WorldBroadcastCount);
		Assert.Equal(0, export.InviteDispatchCount);
	}

	[Fact]
	public void CreateExportFromDisabledPlan_ProjectsActionFourApplicationShowList()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS", "CLERIC", 65);
		var visible = CreatePlayer(0x01020305, "Visible", "ELYOS", "GLADIATOR", 60);
		var hidden = CreatePlayer(0x01020306, "Hidden", "ASMODIANS", "RANGER", 61);
		findGroupService.AddApplication(visible, "Visible app", groupType: 2, classId: 1, level: 60, nowEpochSeconds: 100);
		findGroupService.AddApplication(hidden, "Hidden app", groupType: 2, classId: 5, level: 61, nowEpochSeconds: 100);
		var compositionPlan = CreateCompositionPlan(findGroupService, viewer, action: 4, nowEpochSeconds: 201);

		var projection = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketShowListBoundaryTraceExportProjectionStatus.Created, projection.Status);
		var export = projection.Export;
		Assert.Equal(4, export.Action);
		Assert.Equal(201, export.ServerEpochSeconds);
		Assert.Equal(FindGroupDirectPacketShowListTraceListKind.Applications, export.ListKind);
		Assert.Equal([visible.ObjectId], export.VisibleEntryObjectIds);
		Assert.Equal(viewer.ObjectId, export.DirectPacketRecipientObjectId);
		Assert.Equal("SmFindGroup", export.DirectPacketType);
		Assert.Equal(4, export.DirectPacketAction);
		Assert.False(export.ExecutorInvokedFromBoundary);
		Assert.False(export.RegistrySendObserved);
		Assert.Equal(0, export.WorldBroadcastCount);
		Assert.Equal(0, export.InviteDispatchCount);
	}

	[Fact]
	public void CreateExportFromDisabledPlan_RejectsUnsupportedMutationAction()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "CLERIC", 65);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(player.ObjectId);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});
		var compositionPlan = new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(findGroupService))
			.CreateDisabledPlan(player, packet, nowEpochSeconds: 202);

		var projection = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketShowListBoundaryTraceExportProjectionStatus.UnsupportedAction, projection.Status);
		Assert.Equal(2, projection.Export.Action);
		Assert.Contains("actions 0 and 4", projection.Reason, StringComparison.Ordinal);
		Assert.False(projection.Export.BoundaryAccepted);
	}

	[Fact]
	public void CreateExportFromDisabledPlan_RejectsMissingActivePlayer()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));
		var compositionPlan = new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(findGroupService))
			.CreateDisabledPlan(activePlayer: null, packet, nowEpochSeconds: 203);

		var projection = FindGroupDirectPacketShowListBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketShowListBoundaryTraceExportProjectionStatus.MissingActivePlayer, projection.Status);
		Assert.Equal(0, projection.Export.Action);
		Assert.Contains("active player", projection.Reason, StringComparison.Ordinal);
		Assert.False(projection.Export.BoundaryAccepted);
	}

	private static FindGroupConnectionClientActionCompositionPlan CreateCompositionPlan(
		FindGroupRecruitmentPlanService findGroupService,
		Player viewer,
		int action,
		int nowEpochSeconds)
	{
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC((byte)action));
		return new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(findGroupService))
			.CreateDisabledPlan(viewer, packet, nowEpochSeconds);
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
