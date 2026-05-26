using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportControlPlanServiceTests
{
	[Fact]
	public void CreateCancelPlan_NoopsWhenSkillUseTaskMissing()
	{
		var plan = BindPointTeleportControlPlanService.CreateCancelPlan(
			playerObjectId: 7001,
			locId: 51,
			hasSkillUseTask: false);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportControlPlanStatus.NoAction, plan.Status);
		Assert.False(plan.ShouldCancelSkillUseTask);
		Assert.False(plan.ShouldBroadcast);
		Assert.Null(plan.Packet);
		Assert.Empty(plan.Steps);
		Assert.Contains("cancelTeleport", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateCancelPlan_CancelsTaskThenBroadcastsActionTwo()
	{
		var plan = BindPointTeleportControlPlanService.CreateCancelPlan(
			playerObjectId: 7002,
			locId: 52,
			hasSkillUseTask: true);

		Assert.Equal(BindPointTeleportControlPlanStatus.CancelTeleport, plan.Status);
		Assert.True(plan.ShouldCancelSkillUseTask);
		Assert.True(plan.ShouldBroadcast);
		Assert.Equal(
			[BindPointTeleportControlStep.CancelSkillUseTask, BindPointTeleportControlStep.BroadcastCancel],
			plan.Steps);

		var packet = Assert.IsType<SmBindPointTeleport>(plan.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(7002, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void CreateLoginCooldownPlan_NoopsWhenCooldownMissingOrExpired()
	{
		var missing = BindPointTeleportControlPlanService.CreateLoginCooldownPlan(
			playerObjectId: 7003,
			locId: 53,
			cooldownTimeLeftSeconds: 0);
		var expired = BindPointTeleportControlPlanService.CreateLoginCooldownPlan(
			playerObjectId: 7004,
			locId: 54,
			cooldownTimeLeftSeconds: -1);

		Assert.Equal(BindPointTeleportControlPlanStatus.NoAction, missing.Status);
		Assert.False(missing.ShouldBroadcast);
		Assert.Null(missing.Packet);
		Assert.Equal(BindPointTeleportControlPlanStatus.NoAction, expired.Status);
		Assert.False(expired.ShouldBroadcast);
		Assert.Null(expired.Packet);
	}

	[Fact]
	public void CreateLoginCooldownPlan_BroadcastsAndReceivesActionThreeForActiveCooldown()
	{
		var plan = BindPointTeleportControlPlanService.CreateLoginCooldownPlan(
			playerObjectId: 7005,
			locId: 55,
			cooldownTimeLeftSeconds: 321);

		Assert.Equal(BindPointTeleportControlPlanStatus.BroadcastLoginCooldown, plan.Status);
		Assert.False(plan.ShouldCancelSkillUseTask);
		Assert.True(plan.ShouldBroadcast);
		Assert.Equal([BindPointTeleportControlStep.BroadcastCooldownAndReceive], plan.Steps);

		var packet = Assert.IsType<SmBindPointTeleport>(plan.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(7005, reader.ReadD());
		Assert.Equal(55, reader.ReadD());
		Assert.Equal(321, reader.ReadD());
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
