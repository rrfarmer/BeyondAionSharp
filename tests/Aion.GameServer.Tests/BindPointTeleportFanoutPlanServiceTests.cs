using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportFanoutPlanServiceTests
{
	[Theory]
	[InlineData(BindPointTeleportFanoutSource.TeleportStartBroadcast)]
	[InlineData(BindPointTeleportFanoutSource.TeleportCooldownBroadcast)]
	[InlineData(BindPointTeleportFanoutSource.CancelBroadcast)]
	[InlineData(BindPointTeleportFanoutSource.CustomPvpStartBroadcast)]
	[InlineData(BindPointTeleportFanoutSource.CustomPvpCooldownBroadcast)]
	public void CreatePlan_BroadcastPacketTrueIncludesSourcePlayer(BindPointTeleportFanoutSource source)
	{
		var packet = SmBindPointTeleport.Start(playerObjectId: 7001, locId: 42);

		var plan = BindPointTeleportFanoutPlanService.CreatePlan(source, sourcePlayerObjectId: 7001, packet);

		Assert.Equal(BindPointTeleportFanoutPlanStatus.BroadcastVisiblePlayersAndSelf, plan.Status);
		Assert.Equal(source, plan.Source);
		Assert.Equal(7001, plan.SourcePlayerObjectId);
		Assert.Same(packet, plan.Packet);
		Assert.True(plan.IncludeSourcePlayer);
		Assert.Equal("PacketSendUtility.broadcastPacket(player, packet, true)", plan.JavaUtilityMethod);
		Assert.Equal(
			"IGameClientConnectionRegistry.BroadcastToVisiblePlayersAsync(..., includeSourcePlayer: true)",
			plan.CsharpRegistryMethod);
		Assert.False(plan.IsLive);
		Assert.Contains("KnownList", plan.KnownListNote, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_BroadcastPacketAndReceiveIncludesSourcePlayer()
	{
		var packet = SmBindPointTeleport.Cooldown(playerObjectId: 7002, locId: 43, cooldownSeconds: 600);

		var plan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.LoginCooldownBroadcast,
			sourcePlayerObjectId: 7002,
			packet);

		Assert.Equal(BindPointTeleportFanoutPlanStatus.BroadcastVisiblePlayersAndSelf, plan.Status);
		Assert.True(plan.IncludeSourcePlayer);
		Assert.Equal("PacketSendUtility.broadcastPacketAndReceive(player, packet)", plan.JavaUtilityMethod);
		Assert.Contains("BindPointTeleportService.onLogin", plan.JavaSource, StringComparison.Ordinal);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_RecordsSpecificJavaSourceForBindPointBranches()
	{
		var start = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportStartBroadcast,
			sourcePlayerObjectId: 7003,
			SmBindPointTeleport.Start(7003, 44));
		var cooldown = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			sourcePlayerObjectId: 7003,
			SmBindPointTeleport.Cooldown(7003, 44, 600));
		var cancel = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.CancelBroadcast,
			sourcePlayerObjectId: 7003,
			SmBindPointTeleport.Cancel(7003, 44));

		Assert.Contains("action=1", start.JavaSource, StringComparison.Ordinal);
		Assert.Contains("scheduled task", cooldown.JavaSource, StringComparison.Ordinal);
		Assert.Contains("cancelTeleport", cancel.JavaSource, StringComparison.Ordinal);
	}
}
