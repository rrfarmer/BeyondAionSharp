using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcTargetChangePacketPlanServiceTests
{
	[Fact]
	public void CreatePlan_CreatesLookAtObjectPacketAndUsesHeadingTowardNonSelfTargetLikeJava()
	{
		var plan = NpcTargetChangePacketPlanService.CreatePlan(new NpcTargetChangePacketPlanInput(
			NpcObjectId: 5001,
			NewTargetObjectId: 7002,
			CurrentHeading: 11,
			HeadingTowardTarget: 92,
			IsDead: false,
			HasTalkInfo: false));

		Assert.Equal(NpcTargetChangePacketPlanStatus.PacketCreated, plan.Status);
		Assert.True(plan.ShouldClearAttackedCount);
		Assert.True(plan.ShouldRenewLastTargetChangeTime);
		Assert.False(plan.ShouldScheduleThink);
		Assert.True(plan.ShouldBroadcastPacket);
		Assert.Equal(92, plan.SelectedHeading);
		AssertLookAtObjectPayload(plan.Packet!, objectId: 5001, targetObjectId: 7002, heading: 92);
		Assert.Contains("NpcController.onTargetChanged", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_WithCoordinatesCalculatesHeadingTowardNonSelfTargetLikeJavaPositionUtil()
	{
		var plan = NpcTargetChangePacketPlanService.CreatePlan(new NpcTargetChangeCoordinatePacketPlanInput(
			NpcObjectId: 5001,
			NewTargetObjectId: 7002,
			NpcX: 10,
			NpcY: 10,
			TargetX: 11,
			TargetY: 9,
			CurrentHeading: 11,
			IsDead: false,
			HasTalkInfo: false));

		Assert.Equal(NpcTargetChangePacketPlanStatus.PacketCreated, plan.Status);
		Assert.Equal(105, plan.SelectedHeading);
		AssertLookAtObjectPayload(plan.Packet!, objectId: 5001, targetObjectId: 7002, heading: 105);
	}

	[Fact]
	public void CreatePlan_CreatesZeroTargetPacketWhenTargetClearsAndNpcHasNoTalkInfoLikeJava()
	{
		var plan = NpcTargetChangePacketPlanService.CreatePlan(new NpcTargetChangePacketPlanInput(
			NpcObjectId: 5001,
			NewTargetObjectId: 0,
			CurrentHeading: 11,
			HeadingTowardTarget: 92,
			IsDead: false,
			HasTalkInfo: false));

		Assert.Equal(NpcTargetChangePacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.ShouldScheduleThink);
		Assert.True(plan.ShouldBroadcastPacket);
		Assert.Equal(11, plan.SelectedHeading);
		AssertLookAtObjectPayload(plan.Packet!, objectId: 5001, targetObjectId: 0, heading: 11);
	}

	[Fact]
	public void CreatePlan_SchedulesThinkAndDoesNotBroadcastWhenTalkNpcTargetClearsLikeJava()
	{
		var plan = NpcTargetChangePacketPlanService.CreatePlan(new NpcTargetChangePacketPlanInput(
			NpcObjectId: 5001,
			NewTargetObjectId: 0,
			CurrentHeading: 11,
			HeadingTowardTarget: 92,
			IsDead: false,
			HasTalkInfo: true));

		Assert.Equal(NpcTargetChangePacketPlanStatus.ScheduledThinkForTalkNpcTargetClear, plan.Status);
		Assert.True(plan.ShouldClearAttackedCount);
		Assert.True(plan.ShouldRenewLastTargetChangeTime);
		Assert.True(plan.ShouldScheduleThink);
		Assert.Equal(750, plan.ScheduledThinkDelayMilliseconds);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreatePlan_DoesNotBroadcastWhenNpcIsDeadButKeepsPreDeadSideEffectsLikeJava()
	{
		var plan = NpcTargetChangePacketPlanService.CreatePlan(new NpcTargetChangePacketPlanInput(
			NpcObjectId: 5001,
			NewTargetObjectId: 7002,
			CurrentHeading: 11,
			HeadingTowardTarget: 92,
			IsDead: true,
			HasTalkInfo: false));

		Assert.Equal(NpcTargetChangePacketPlanStatus.NoPacketNpcDead, plan.Status);
		Assert.True(plan.ShouldClearAttackedCount);
		Assert.True(plan.ShouldRenewLastTargetChangeTime);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidNpcOwnerBeforeJavaSideEffects()
	{
		var plan = NpcTargetChangePacketPlanService.CreatePlan(new NpcTargetChangePacketPlanInput(
			NpcObjectId: 0,
			NewTargetObjectId: 7002,
			CurrentHeading: 11,
			HeadingTowardTarget: 92,
			IsDead: false,
			HasTalkInfo: false));

		Assert.Equal(NpcTargetChangePacketPlanStatus.BlockedInvalidNpc, plan.Status);
		Assert.False(plan.ShouldClearAttackedCount);
		Assert.False(plan.ShouldRenewLastTargetChangeTime);
		Assert.False(plan.ShouldBroadcastPacket);
	}

	private static void AssertLookAtObjectPayload(SmLookAtObject packet, int objectId, int targetObjectId, int heading)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmLookAtObject.PacketOpCode, packet.OpCode);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(targetObjectId, reader.ReadD());
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
