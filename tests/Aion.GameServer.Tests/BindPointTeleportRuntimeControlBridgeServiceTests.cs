using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRuntimeControlBridgeServiceTests
{
	[Fact]
	public async Task CreateCancelPlan_MissingSkillUseTaskNoopsWithoutCancelling()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeControlBridgeService(owner);

		var plan = bridge.CreateCancelPlan(playerObjectId: 8101, locId: 6201);

		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldSendPacket);
		Assert.Null(plan.TaskOwnerResult);
		Assert.Equal(BindPointTeleportControlPlanStatus.NoAction, plan.ControlPlan.Status);
		Assert.False(owner.HasSkillUseTask(8101));
	}

	[Fact]
	public async Task CreateCancelPlan_ExistingSkillUseTaskCancelsThenCreatesActionTwoPacket()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeControlBridgeService(owner);
		var callbackCount = 0;
		owner.ScheduleSkillUseTask(
			playerObjectId: 8102,
			locId: 6202,
			_ =>
			{
				Interlocked.Increment(ref callbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.FromSeconds(30));

		var plan = bridge.CreateCancelPlan(playerObjectId: 8102, locId: 6202);

		Assert.True(plan.ShouldSendPacket);
		Assert.NotNull(plan.TaskOwnerResult);
		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.CancelledExistingTask, plan.TaskOwnerResult.Status);
		Assert.True(plan.TaskOwnerResult.RemovedTask);
		Assert.Equal(BindPointTeleportControlPlanStatus.CancelTeleport, plan.ControlPlan.Status);
		Assert.False(owner.HasSkillUseTask(8102));

		var packet = Assert.IsType<SmBindPointTeleport>(plan.ControlPlan.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(8102, reader.ReadD());
		Assert.Equal(0, reader.Remaining);

		await Task.Delay(50);
		Assert.Equal(0, callbackCount);
	}

	[Fact]
	public async Task CreateLoginCooldownPlan_MissingOrExpiredCooldownNoops()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeControlBridgeService(owner);
		owner.AddCooldown(playerObjectId: 8103, locId: 6203, currentTimeMillis: 1_000);

		var missing = bridge.CreateLoginCooldownPlan(playerObjectId: 8104, currentTimeMillis: 2_000);
		var expired = bridge.CreateLoginCooldownPlan(playerObjectId: 8103, currentTimeMillis: 601_000);

		Assert.Equal(BindPointTeleportCooldownPlanStatus.NoCooldown, missing.CooldownPlan?.Status);
		Assert.Equal(BindPointTeleportControlPlanStatus.NoAction, missing.ControlPlan.Status);
		Assert.False(missing.ShouldSendPacket);
		Assert.Equal(BindPointTeleportCooldownPlanStatus.ExpiredCooldown, expired.CooldownPlan?.Status);
		Assert.Equal(BindPointTeleportControlPlanStatus.NoAction, expired.ControlPlan.Status);
		Assert.False(expired.ShouldSendPacket);
		Assert.NotNull(owner.GetCooldown(8103));
	}

	[Fact]
	public async Task CreateLoginCooldownPlan_ActiveCooldownCreatesActionThreePacket()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeControlBridgeService(owner);
		owner.AddCooldown(playerObjectId: 8105, locId: 6205, currentTimeMillis: 1_000);

		var plan = bridge.CreateLoginCooldownPlan(playerObjectId: 8105, currentTimeMillis: 2_499);

		Assert.True(plan.ShouldSendPacket);
		Assert.NotNull(plan.CooldownPlan);
		Assert.Equal(BindPointTeleportCooldownPlanStatus.ActiveCooldown, plan.CooldownPlan.Status);
		Assert.Equal(598, plan.CooldownPlan.TimeLeftSeconds);
		Assert.Equal(BindPointTeleportControlPlanStatus.BroadcastLoginCooldown, plan.ControlPlan.Status);

		var packet = Assert.IsType<SmBindPointTeleport>(plan.ControlPlan.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(8105, reader.ReadD());
		Assert.Equal(6205, reader.ReadD());
		Assert.Equal(598, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static ThreadPoolManager CreateThreadPoolManager()
	{
		return new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
