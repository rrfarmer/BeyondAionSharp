using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RideRobotEffectPlanServiceTests
{
	[Fact]
	public void CreateStartPlan_SetsRobotIdBroadcastsPacketAndAddsWeaponUnequipObserverLikeJava()
	{
		var plan = RideRobotEffectPlanService.CreateStartPlan(playerObjectId: 8002, weaponSkinRobotId: 185000137);

		Assert.Equal(RideRobotEffectPlanStatus.StartPlanned, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal(185000137, plan.RobotIdToSet);
		Assert.True(plan.ShouldSetPlayerRobotId);
		Assert.True(plan.ShouldBroadcastRobotPacket);
		Assert.True(plan.ShouldAddUnequipObserver);
		Assert.Equal("UNEQUIP", plan.ObserverTypeName);
		Assert.Equal("WEAPON", plan.ObserverEquipmentTypeName);
		Assert.False(plan.ShouldEndRideRobotConditionEffects);
		Assert.Contains("RideRobotEffect.startEffect", plan.JavaSource);
		Assert.NotNull(plan.PacketPlan);
		Assert.Equal(RideRobotPacketPlanStatus.PacketCreated, plan.PacketPlan!.Status);
		AssertSmRideRobotPayload(plan.Packet!, playerObjectId: 8002, robotId: 185000137);
	}

	[Fact]
	public void CreateEndPlan_ResetsRobotIdBroadcastsZeroAndEndsRideRobotConditionEffectsLikeJava()
	{
		var plan = RideRobotEffectPlanService.CreateEndPlan(playerObjectId: 8002);

		Assert.Equal(RideRobotEffectPlanStatus.EndPlanned, plan.Status);
		Assert.Equal(0, plan.RobotIdToSet);
		Assert.True(plan.ShouldSetPlayerRobotId);
		Assert.True(plan.ShouldBroadcastRobotPacket);
		Assert.False(plan.ShouldAddUnequipObserver);
		Assert.Null(plan.ObserverTypeName);
		Assert.Null(plan.ObserverEquipmentTypeName);
		Assert.True(plan.ShouldEndRideRobotConditionEffects);
		Assert.Contains("RideRobotEffect.endEffect", plan.JavaSource);
		Assert.NotNull(plan.PacketPlan);
		Assert.Equal(RideRobotPacketPlanStatus.PacketCreated, plan.PacketPlan!.Status);
		AssertSmRideRobotPayload(plan.Packet!, playerObjectId: 8002, robotId: 0);
	}

	[Fact]
	public void CreateStartPlan_BlocksInvalidPlayerBeforeMutationOrPacketPlanning()
	{
		var plan = RideRobotEffectPlanService.CreateStartPlan(playerObjectId: 0, weaponSkinRobotId: 185000137);

		Assert.Equal(RideRobotEffectPlanStatus.BlockedInvalidPlayer, plan.Status);
		Assert.False(plan.ShouldSetPlayerRobotId);
		Assert.False(plan.ShouldBroadcastRobotPacket);
		Assert.False(plan.ShouldAddUnequipObserver);
		Assert.False(plan.ShouldEndRideRobotConditionEffects);
		Assert.Null(plan.PacketPlan);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreateStartPlan_BlocksMissingWeaponRobotIdBeforeMutationOrPacketPlanning()
	{
		var plan = RideRobotEffectPlanService.CreateStartPlan(playerObjectId: 8002, weaponSkinRobotId: 0);

		Assert.Equal(RideRobotEffectPlanStatus.BlockedMissingWeaponRobot, plan.Status);
		Assert.False(plan.ShouldSetPlayerRobotId);
		Assert.False(plan.ShouldBroadcastRobotPacket);
		Assert.False(plan.ShouldAddUnequipObserver);
		Assert.Null(plan.PacketPlan);
	}

	[Fact]
	public void CreateEndPlan_BlocksInvalidPlayerBeforeResetOrCleanup()
	{
		var plan = RideRobotEffectPlanService.CreateEndPlan(playerObjectId: 0);

		Assert.Equal(RideRobotEffectPlanStatus.BlockedInvalidPlayer, plan.Status);
		Assert.False(plan.ShouldSetPlayerRobotId);
		Assert.False(plan.ShouldBroadcastRobotPacket);
		Assert.False(plan.ShouldEndRideRobotConditionEffects);
		Assert.Null(plan.PacketPlan);
	}

	private static void AssertSmRideRobotPayload(SmRideRobot packet, int playerObjectId, int robotId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmRideRobot.PacketOpCode, packet.OpCode);
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(robotId, reader.ReadD());
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
