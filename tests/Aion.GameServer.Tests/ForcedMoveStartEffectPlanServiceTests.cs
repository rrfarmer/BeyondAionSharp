using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ForcedMoveStartEffectPlanServiceTests
{
	[Fact]
	public void CreatePlan_ForPulledPlayer_UsesEffectorPacketAndStopsMovement()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.Pulled,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 8002, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: true,
				IsReflected: false,
				EffectorObjectId: 7001,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.PlannedPlayerPacket, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal(7001, plan.CancelCurrentSkillSourceObjectId);
		Assert.False(plan.ShouldRemoveParalyzeEffects);
		Assert.False(plan.ShouldRemoveStunEffects);
		Assert.True(plan.ShouldCallPlayerOnStopGliding);
		Assert.True(plan.ShouldCallPlayerOnStopMove);
		Assert.True(plan.ShouldUpdateWorldPosition);
		Assert.Equal("PULLED", plan.AbnormalStateName);
		Assert.True(plan.ShouldSetEffectedControllerAbnormal);
		Assert.True(plan.ShouldSetEffectAbnormal);
		Assert.NotNull(plan.ForcedMovePacketPlan);
		Assert.Equal(ForcedMovePacketPlanStatus.PacketCreated, plan.ForcedMovePacketPlan!.Status);
		AssertSmForcedMovePayload(plan.ForcedMovePacketPlan.Packet!, sourceObjectId: 7001, targetObjectId: 8002, x: 12.5f, y: -8.25f, z: 3.75f);
	}

	[Fact]
	public void CreatePlan_ForReflectedPulledPlayer_UsesOriginalEffectedPacketSourceAndSkipsMotionStops()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.Pulled,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 8002, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: true,
				IsReflected: true,
				EffectorObjectId: 7001,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.PlannedPlayerPacket, plan.Status);
		Assert.Null(plan.CancelCurrentSkillSourceObjectId);
		Assert.False(plan.ShouldCallPlayerOnStopGliding);
		Assert.False(plan.ShouldCallPlayerOnStopMove);
		Assert.NotNull(plan.ForcedMovePacketPlan);
		AssertSmForcedMovePayload(plan.ForcedMovePacketPlan!.Packet!, sourceObjectId: 9003, targetObjectId: 8002, x: 12.5f, y: -8.25f, z: 3.75f);
	}

	[Fact]
	public void CreatePlan_ForOpenAerialNpc_RemovesParalyzeAndSkipsForcedMovePacket()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.OpenAerial,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 8002, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: false,
				IsReflected: false,
				EffectorObjectId: 7001,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.PlannedNpcNoPacket, plan.Status);
		Assert.Equal(7001, plan.CancelCurrentSkillSourceObjectId);
		Assert.True(plan.ShouldRemoveParalyzeEffects);
		Assert.False(plan.ShouldRemoveStunEffects);
		Assert.False(plan.ShouldCallPlayerOnStopGliding);
		Assert.False(plan.ShouldCallPlayerOnStopMove);
		Assert.True(plan.ShouldUpdateWorldPosition);
		Assert.Equal("OPENAERIAL", plan.AbnormalStateName);
		Assert.True(plan.ShouldSetEffectedControllerAbnormal);
		Assert.True(plan.ShouldSetEffectAbnormal);
		Assert.Null(plan.ForcedMovePacketPlan);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidEffectedBeforeWorldOrPacketPlanning()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.OpenAerial,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 0, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: true,
				IsReflected: false,
				EffectorObjectId: 7001,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.BlockedInvalidEffected, plan.Status);
		Assert.Null(plan.CancelCurrentSkillSourceObjectId);
		Assert.False(plan.ShouldRemoveParalyzeEffects);
		Assert.False(plan.ShouldRemoveStunEffects);
		Assert.False(plan.ShouldCallPlayerOnStopGliding);
		Assert.False(plan.ShouldCallPlayerOnStopMove);
		Assert.False(plan.ShouldUpdateWorldPosition);
		Assert.Null(plan.UpdatedPosition);
		Assert.Null(plan.ForcedMovePacketPlan);
		Assert.False(plan.ShouldSetEffectedControllerAbnormal);
		Assert.False(plan.ShouldSetEffectAbnormal);
	}

	[Fact]
	public void CreatePlan_ForStaggerPlayer_RemovesParalyzeAndCreatesForcedMovePacketLikeJava()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.Stagger,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 8002, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: true,
				IsReflected: false,
				EffectorObjectId: 7001,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.PlannedPlayerPacket, plan.Status);
		Assert.Equal(7001, plan.CancelCurrentSkillSourceObjectId);
		Assert.True(plan.ShouldRemoveParalyzeEffects);
		Assert.False(plan.ShouldRemoveStunEffects);
		Assert.True(plan.ShouldCallPlayerOnStopGliding);
		Assert.True(plan.ShouldCallPlayerOnStopMove);
		Assert.True(plan.ShouldUpdateWorldPosition);
		Assert.Equal("STAGGER", plan.AbnormalStateName);
		Assert.True(plan.ShouldSetEffectedControllerAbnormal);
		Assert.True(plan.ShouldSetEffectAbnormal);
		Assert.Contains("StaggerEffect.startEffect", plan.JavaSource);
		AssertSmForcedMovePayload(plan.ForcedMovePacketPlan!.Packet!, sourceObjectId: 7001, targetObjectId: 8002, x: 12.5f, y: -8.25f, z: 3.75f);
	}

	[Fact]
	public void CreatePlan_ForStumbleNpc_RemovesParalyzeAndStunButSkipsPlayerPacketLikeJava()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.Stumble,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 8002, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: false,
				IsReflected: false,
				EffectorObjectId: 7001,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.PlannedNpcNoPacket, plan.Status);
		Assert.Equal(7001, plan.CancelCurrentSkillSourceObjectId);
		Assert.True(plan.ShouldRemoveParalyzeEffects);
		Assert.True(plan.ShouldRemoveStunEffects);
		Assert.False(plan.ShouldCallPlayerOnStopGliding);
		Assert.False(plan.ShouldCallPlayerOnStopMove);
		Assert.True(plan.ShouldUpdateWorldPosition);
		Assert.Equal("STUMBLE", plan.AbnormalStateName);
		Assert.True(plan.ShouldSetEffectedControllerAbnormal);
		Assert.True(plan.ShouldSetEffectAbnormal);
		Assert.Null(plan.ForcedMovePacketPlan);
		Assert.Contains("StumbleEffect.startEffect", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ForStaggerPlayerWithInvalidEffector_BlocksBeforeWorldAndAbnormalMutation()
	{
		var plan = ForcedMoveStartEffectPlanService.CreatePlan(
			new ForcedMoveStartEffectPlanInput(
				EffectKind: ForcedMoveEffectKind.Stagger,
				EffectedCurrentPosition: new ObjectPositionSnapshot(ObjectId: 8002, X: 5, Y: 6, Z: 7, Heading: 105),
				TargetX: 12.5f,
				TargetY: -8.25f,
				TargetZ: 3.75f,
				IsEffectedPlayer: true,
				IsReflected: false,
				EffectorObjectId: 0,
				OriginalEffectedObjectId: 9003
			)
		);

		Assert.Equal(ForcedMoveStartEffectPlanStatus.BlockedInvalidPacketSource, plan.Status);
		Assert.Equal(0, plan.CancelCurrentSkillSourceObjectId);
		Assert.True(plan.ShouldRemoveParalyzeEffects);
		Assert.False(plan.ShouldRemoveStunEffects);
		Assert.True(plan.ShouldCallPlayerOnStopGliding);
		Assert.True(plan.ShouldCallPlayerOnStopMove);
		Assert.False(plan.ShouldUpdateWorldPosition);
		Assert.Null(plan.UpdatedPosition);
		Assert.Equal(ForcedMovePacketPlanStatus.BlockedInvalidSource, plan.ForcedMovePacketPlan!.Status);
		Assert.False(plan.ShouldSetEffectedControllerAbnormal);
		Assert.False(plan.ShouldSetEffectAbnormal);
	}

	private static void AssertSmForcedMovePayload(SmForcedMove packet, int sourceObjectId, int targetObjectId, float x, float y, float z)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmForcedMove.PacketOpCode, packet.OpCode);
		Assert.Equal(sourceObjectId, reader.ReadD());
		Assert.Equal(targetObjectId, reader.ReadD());
		Assert.Equal(16, reader.ReadC());
		Assert.Equal(x, reader.ReadF());
		Assert.Equal(y, reader.ReadF());
		Assert.Equal(z, reader.ReadF());
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
