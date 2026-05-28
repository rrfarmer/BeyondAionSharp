using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FearConfuseEndEffectPlanServiceTests
{
	[Fact]
	public void CreatePlan_ForConfusePlayerModelsUnsetAbortAndBroadcastReceiveLikeJava()
	{
		var plan = FearConfuseEndEffectPlanService.CreatePlan(new FearConfuseEndEffectPlanInput(
			FearConfuseEffectKind.Confuse,
			new ObjectPositionSnapshot(ObjectId: 5001, X: 123.5f, Y: -45.25f, Z: 98.75f, Heading: 105),
			IsEffectedNpc: false));

		Assert.Equal(FearConfuseEndEffectPlanStatus.Planned, plan.Status);
		Assert.Equal("CONFUSE", plan.AbnormalStateName);
		Assert.True(plan.ShouldUnsetAbnormal);
		Assert.True(plan.ShouldAbortMove);
		Assert.True(plan.ShouldBroadcastPosition);
		Assert.True(plan.ShouldBroadcastAndReceivePosition);
		Assert.False(plan.ShouldSetNpcIdle);
		Assert.False(plan.ShouldNotifyNpcAttackEvent);
		Assert.Contains("ConfuseEffect.endEffect", plan.JavaSource);
		AssertSmPositionPayload(plan.PositionPacket!, objectId: 5001, x: 123.5f, y: -45.25f, z: 98.75f, heading: 105);
	}

	[Fact]
	public void CreatePlan_ForFearNpcModelsNpcAiCleanupAfterPositionCorrectionLikeJava()
	{
		var plan = FearConfuseEndEffectPlanService.CreatePlan(new FearConfuseEndEffectPlanInput(
			FearConfuseEffectKind.Fear,
			new ObjectPositionSnapshot(ObjectId: 7002, X: 10, Y: 11, Z: 12, Heading: 31),
			IsEffectedNpc: true));

		Assert.Equal(FearConfuseEndEffectPlanStatus.Planned, plan.Status);
		Assert.Equal("FEAR", plan.AbnormalStateName);
		Assert.True(plan.ShouldUnsetAbnormal);
		Assert.True(plan.ShouldAbortMove);
		Assert.True(plan.ShouldBroadcastPosition);
		Assert.True(plan.ShouldBroadcastAndReceivePosition);
		Assert.True(plan.ShouldSetNpcIdle);
		Assert.True(plan.ShouldNotifyNpcAttackEvent);
		Assert.Contains("FearEffect.endEffect", plan.JavaSource);
		AssertSmPositionPayload(plan.PositionPacket!, objectId: 7002, x: 10, y: 11, z: 12, heading: 31);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidEffectedObjectBeforeSideEffects()
	{
		var plan = FearConfuseEndEffectPlanService.CreatePlan(new FearConfuseEndEffectPlanInput(
			FearConfuseEffectKind.Fear,
			new ObjectPositionSnapshot(ObjectId: 0, X: 10, Y: 11, Z: 12, Heading: 31),
			IsEffectedNpc: true));

		Assert.Equal(FearConfuseEndEffectPlanStatus.BlockedInvalidObject, plan.Status);
		Assert.False(plan.ShouldUnsetAbnormal);
		Assert.False(plan.ShouldAbortMove);
		Assert.False(plan.ShouldBroadcastPosition);
		Assert.False(plan.ShouldBroadcastAndReceivePosition);
		Assert.False(plan.ShouldSetNpcIdle);
		Assert.False(plan.ShouldNotifyNpcAttackEvent);
		Assert.Equal(MovementCorrectionPacketPlanStatus.BlockedInvalidObject, plan.MovementCorrectionPlan.Status);
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
