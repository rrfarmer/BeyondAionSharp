using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SimpleRootSubEffectMovementPlanServiceTests
{
	[Fact]
	public void CreatePlan_ForPlayerSubEffect_StopsMoveUpdatesWorldAndSkipsBroadcastLikeJava()
	{
		var plan = SimpleRootSubEffectMovementPlanService.CreatePlan(new SimpleRootSubEffectMovementPlanInput(
			new ObjectPositionSnapshot(ObjectId: 5001, X: 50, Y: 51, Z: 52, Heading: 61),
			TargetX: 70.5f,
			TargetY: 71.5f,
			TargetZ: 72.5f,
			IsEffectedPlayer: true,
			IsSubEffect: true));

		Assert.Equal(SimpleRootSubEffectMovementPlanStatus.PlannedSubEffectPlayerNoBroadcast, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSetSpellStatusNone);
		Assert.True(plan.ShouldCallPlayerOnStopMove);
		Assert.True(plan.ShouldUpdateWorldPosition);
		Assert.NotNull(plan.UpdatedPosition);
		Assert.Equal(70.5f, plan.UpdatedPosition!.X);
		Assert.Equal(71.5f, plan.UpdatedPosition.Y);
		Assert.Equal(72.5f, plan.UpdatedPosition.Z);
		Assert.Equal(61, plan.UpdatedPosition.Heading);
		Assert.Null(plan.MovementCorrectionPlan);
		Assert.False(plan.ShouldBroadcastPosition);
		Assert.False(plan.ShouldBroadcastAndReceivePosition);
		Assert.Null(plan.PositionPacket);
		Assert.True(plan.ShouldSetEffectedControllerSimpleMoveBack);
		Assert.True(plan.ShouldSetEffectSimpleMoveBack);
		Assert.Contains("SimpleRootEffect.startEffect", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ForNpcSubEffect_UsesMovementCorrectionBroadcastWithoutReceiveLikeJava()
	{
		var plan = SimpleRootSubEffectMovementPlanService.CreatePlan(new SimpleRootSubEffectMovementPlanInput(
			new ObjectPositionSnapshot(ObjectId: 7002, X: 10, Y: 11, Z: 12, Heading: 31),
			TargetX: 100.25f,
			TargetY: 200.5f,
			TargetZ: 300.75f,
			IsEffectedPlayer: false,
			IsSubEffect: true));

		Assert.Equal(SimpleRootSubEffectMovementPlanStatus.PlannedSubEffectObjectBroadcast, plan.Status);
		Assert.True(plan.ShouldSetSpellStatusNone);
		Assert.False(plan.ShouldCallPlayerOnStopMove);
		Assert.True(plan.ShouldUpdateWorldPosition);
		Assert.NotNull(plan.UpdatedPosition);
		Assert.NotNull(plan.MovementCorrectionPlan);
		Assert.Equal(MovementCorrectionPacketPlanStatus.ObjectPositionPacketCreated, plan.MovementCorrectionPlan!.Status);
		Assert.True(plan.ShouldBroadcastPosition);
		Assert.False(plan.ShouldBroadcastAndReceivePosition);
		Assert.NotNull(plan.PositionPacket);
		AssertSmPositionPayload(plan.PositionPacket!, objectId: 7002, x: 100.25f, y: 200.5f, z: 300.75f, heading: 31);
		Assert.True(plan.ShouldSetEffectedControllerSimpleMoveBack);
		Assert.True(plan.ShouldSetEffectSimpleMoveBack);
	}

	[Fact]
	public void CreatePlan_ForNonSubEffectPlayer_SetsAbnormalWithoutWorldUpdateLikeJava()
	{
		var plan = SimpleRootSubEffectMovementPlanService.CreatePlan(new SimpleRootSubEffectMovementPlanInput(
			new ObjectPositionSnapshot(ObjectId: 5001, X: 50, Y: 51, Z: 52, Heading: 61),
			TargetX: 70.5f,
			TargetY: 71.5f,
			TargetZ: 72.5f,
			IsEffectedPlayer: true,
			IsSubEffect: false));

		Assert.Equal(SimpleRootSubEffectMovementPlanStatus.PlannedNoSubEffect, plan.Status);
		Assert.True(plan.ShouldSetSpellStatusNone);
		Assert.True(plan.ShouldCallPlayerOnStopMove);
		Assert.False(plan.ShouldUpdateWorldPosition);
		Assert.Null(plan.UpdatedPosition);
		Assert.Null(plan.MovementCorrectionPlan);
		Assert.False(plan.ShouldBroadcastPosition);
		Assert.True(plan.ShouldSetEffectedControllerSimpleMoveBack);
		Assert.True(plan.ShouldSetEffectSimpleMoveBack);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidObjectBeforeAnySideEffects()
	{
		var plan = SimpleRootSubEffectMovementPlanService.CreatePlan(new SimpleRootSubEffectMovementPlanInput(
			new ObjectPositionSnapshot(ObjectId: 0, X: 10, Y: 11, Z: 12, Heading: 31),
			TargetX: 100.25f,
			TargetY: 200.5f,
			TargetZ: 300.75f,
			IsEffectedPlayer: false,
			IsSubEffect: true));

		Assert.Equal(SimpleRootSubEffectMovementPlanStatus.BlockedInvalidObject, plan.Status);
		Assert.False(plan.ShouldSetSpellStatusNone);
		Assert.False(plan.ShouldCallPlayerOnStopMove);
		Assert.False(plan.ShouldUpdateWorldPosition);
		Assert.Null(plan.UpdatedPosition);
		Assert.Null(plan.MovementCorrectionPlan);
		Assert.False(plan.ShouldSetEffectedControllerSimpleMoveBack);
		Assert.False(plan.ShouldSetEffectSimpleMoveBack);
		Assert.Null(plan.PositionPacket);
	}

	private static void AssertSmPositionPayload(SmPosition packet, int objectId, float x, float y, float z, int heading)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmPosition.PacketOpCode, packet.OpCode);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(x, reader.ReadF());
		Assert.Equal(y, reader.ReadF());
		Assert.Equal(z, reader.ReadF());
		Assert.Equal(heading, reader.ReadC());
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
