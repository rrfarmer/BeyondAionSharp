using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TargetSelectExecutionPlanServiceTests
{
	[Fact]
	public void CreatePlan_ComposesResolutionAndTargetChangePacketsWhenJavaWouldSetNewTarget()
	{
		var resolution = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 7002,
			SelectTargetOfTarget: false,
			KnownTargetObjectId: 7002,
			KnownTargetSeenByPlayer: true));

		var execution = TargetSelectExecutionPlanService.CreatePlan(resolution, currentPlayerTargetObjectId: 0);

		Assert.Equal(TargetSelectExecutionPlanStatus.TargetChangePacketsCreated, execution.Status);
		Assert.True(execution.ShouldMutatePlayerTargetObjectId);
		Assert.True(execution.ShouldSendOwnerPacket);
		Assert.True(execution.ShouldBroadcastToSightedPlayers);
		Assert.Equal(0, execution.PreviousTargetObjectId);
		Assert.Equal(7002, execution.NewTargetObjectId);
		Assert.Equal(TargetSelectSystemMessage.None, execution.SystemMessage);
		Assert.NotNull(execution.TargetChangePacketPlan);
		Assert.Contains("PlayerController.onTargetChanged", execution.JavaSource);
		AssertTargetSelectedPayload(execution.TargetChangePacketPlan.OwnerPacket!, targetObjectId: 7002);
		AssertTargetUpdatePayload(execution.TargetChangePacketPlan.SightedPlayersPacket!, playerObjectId: 1001, targetObjectId: 7002);
	}

	[Fact]
	public void CreatePlan_ComposesClearTargetPacketsWhenJavaWouldSetNullTarget()
	{
		var resolution = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: false));

		var execution = TargetSelectExecutionPlanService.CreatePlan(resolution, currentPlayerTargetObjectId: 7002);

		Assert.Equal(TargetSelectExecutionPlanStatus.TargetChangePacketsCreated, execution.Status);
		Assert.True(execution.ShouldMutatePlayerTargetObjectId);
		Assert.Equal(7002, execution.PreviousTargetObjectId);
		Assert.Equal(0, execution.NewTargetObjectId);
		AssertTargetSelectedPayload(execution.TargetChangePacketPlan!.OwnerPacket!, targetObjectId: 0);
		AssertTargetUpdatePayload(execution.TargetChangePacketPlan.SightedPlayersPacket!, playerObjectId: 1001, targetObjectId: 0);
	}

	[Fact]
	public void CreatePlan_DoesNotCreatePacketsWhenResolvedTargetMatchesCurrentTarget()
	{
		var resolution = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 7002,
			SelectTargetOfTarget: false,
			KnownTargetObjectId: 7002,
			KnownTargetSeenByPlayer: true));

		var execution = TargetSelectExecutionPlanService.CreatePlan(resolution, currentPlayerTargetObjectId: 7002);

		Assert.Equal(TargetSelectExecutionPlanStatus.TargetUnchanged, execution.Status);
		Assert.False(execution.ShouldMutatePlayerTargetObjectId);
		Assert.False(execution.ShouldSendOwnerPacket);
		Assert.False(execution.ShouldBroadcastToSightedPlayers);
		Assert.Equal(7002, execution.PreviousTargetObjectId);
		Assert.Equal(7002, execution.NewTargetObjectId);
		Assert.Null(execution.TargetChangePacketPlan!.OwnerPacket);
		Assert.Null(execution.TargetChangePacketPlan.SightedPlayersPacket);
	}

	[Fact]
	public void CreatePlan_PreservesAssistSystemMessageAndSkipsTargetChangeWhenJavaReturnsEarly()
	{
		var resolution = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetObjectId: 7002,
			TargetOfTargetObjectId: 7003,
			TargetOfTargetKnownByPlayer: true,
			TargetOfTargetSeenByPlayer: false));

		var execution = TargetSelectExecutionPlanService.CreatePlan(resolution, currentPlayerTargetObjectId: 7002);

		Assert.Equal(TargetSelectExecutionPlanStatus.ReturnedEarlyWithSystemMessage, execution.Status);
		Assert.False(execution.ShouldMutatePlayerTargetObjectId);
		Assert.False(execution.ShouldSendOwnerPacket);
		Assert.False(execution.ShouldBroadcastToSightedPlayers);
		Assert.Equal(7002, execution.PreviousTargetObjectId);
		Assert.Equal(7002, execution.NewTargetObjectId);
		Assert.Equal(TargetSelectSystemMessage.AssistNoUser, execution.SystemMessage);
		Assert.Null(execution.TargetChangePacketPlan);
		Assert.Contains("returned before player.setTarget", execution.JavaSource);
	}

	private static void AssertTargetSelectedPayload(SmTargetSelected packet, int targetObjectId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmTargetSelected.PacketOpCode, packet.OpCode);
		Assert.Equal(targetObjectId, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertTargetUpdatePayload(SmTargetUpdate packet, int playerObjectId, int targetObjectId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmTargetUpdate.PacketOpCode, packet.OpCode);
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(targetObjectId, reader.ReadD());
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
