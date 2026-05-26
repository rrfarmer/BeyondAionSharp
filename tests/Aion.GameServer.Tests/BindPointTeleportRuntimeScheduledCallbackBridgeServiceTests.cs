using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRuntimeScheduledCallbackBridgeServiceTests
{
	[Fact]
	public async Task ScheduleMetadataCallback_OperationNotReadyDoesNotScheduleTask()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeScheduledCallbackBridgeService(owner);
		var operationPlan = BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId: 8301,
			locId: 6401,
			hotspotExists: false,
			pricePlan: null,
			requirementsPlan: null);

		var plan = bridge.ScheduleMetadataCallback(
			playerObjectId: 8301,
			operationPlan,
			CreateCallbackPlan());

		Assert.Equal(BindPointTeleportRuntimeScheduledCallbackBridgeStatus.NotScheduledOperationNotReady, plan.Status);
		Assert.False(plan.ShouldScheduleTask);
		Assert.False(plan.ScheduledTask);
		Assert.Null(plan.TaskOwnerResult);
		Assert.False(owner.HasSkillUseTask(8301));
	}

	[Fact]
	public async Task ScheduleMetadataCallback_ReadyOperationWithoutCallbackPlanRecordsGap()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeScheduledCallbackBridgeService(owner);

		var plan = bridge.ScheduleMetadataCallback(
			playerObjectId: 8302,
			CreateReadyOperationPlan(playerObjectId: 8302, locId: 6402),
			callbackPlan: null);

		Assert.Equal(BindPointTeleportRuntimeScheduledCallbackBridgeStatus.NotScheduledMissingCallbackPlan, plan.Status);
		Assert.False(plan.ShouldScheduleTask);
		Assert.False(plan.ScheduledTask);
		Assert.Null(plan.TaskOwnerResult);
		Assert.False(owner.HasSkillUseTask(8302));
	}

	[Fact]
	public async Task ScheduleMetadataCallback_ReadyOperationSchedulesJavaSkillUseTaskSlot()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeScheduledCallbackBridgeService(owner);
		var callbackPlan = CreateCallbackPlan();

		var plan = bridge.ScheduleMetadataCallback(
			playerObjectId: 8303,
			CreateReadyOperationPlan(playerObjectId: 8303, locId: 6403),
			callbackPlan);

		Assert.Equal(BindPointTeleportRuntimeScheduledCallbackBridgeStatus.ScheduledMetadataCallback, plan.Status);
		Assert.True(plan.ShouldScheduleTask);
		Assert.True(plan.ScheduledTask);
		Assert.Same(callbackPlan, plan.CallbackPlan);
		Assert.NotNull(plan.TaskOwnerResult);
		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.ScheduledNewTask, plan.TaskOwnerResult.Status);
		Assert.Equal(10_000, plan.TaskOwnerResult.Plan.DelayMilliseconds);
		Assert.True(owner.HasSkillUseTask(8303));
		Assert.Equal(1, owner.PendingSkillUseTaskCount);
		Assert.Contains("TaskId.SKILL_USE", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ScheduleMetadataCallback_ReplacementCancelsExistingMetadataTask()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeScheduledCallbackBridgeService(owner);
		bridge.ScheduleMetadataCallback(
			playerObjectId: 8304,
			CreateReadyOperationPlan(playerObjectId: 8304, locId: 6404),
			CreateCallbackPlan(),
			delay: TimeSpan.FromSeconds(30));

		var replacement = bridge.ScheduleMetadataCallback(
			playerObjectId: 8304,
			CreateReadyOperationPlan(playerObjectId: 8304, locId: 6405),
			CreateCallbackPlan(),
			delay: TimeSpan.FromSeconds(30));

		Assert.Equal(BindPointTeleportRuntimeScheduledCallbackBridgeStatus.ScheduledMetadataCallback, replacement.Status);
		Assert.NotNull(replacement.TaskOwnerResult);
		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.ReplacedExistingTask, replacement.TaskOwnerResult.Status);
		Assert.True(replacement.TaskOwnerResult.HadExistingTask);
		Assert.True(replacement.TaskOwnerResult.CancelledExistingTask);
		Assert.True(owner.HasSkillUseTask(8304));
		Assert.Equal(1, owner.PendingSkillUseTaskCount);
	}

	[Fact]
	public async Task ScheduleMetadataCallback_ZeroDelayInvokesMetadataOnlyCallback()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var bridge = new BindPointTeleportRuntimeScheduledCallbackBridgeService(owner);
		var callbackPlan = CreateCallbackPlan();
		var observed = new TaskCompletionSource<BindPointTeleportScheduledCallbackPlan>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		bridge.ScheduleMetadataCallback(
			playerObjectId: 8305,
			CreateReadyOperationPlan(playerObjectId: 8305, locId: 6405),
			callbackPlan,
			(plan, _) =>
			{
				observed.SetResult(plan);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.Zero);

		var observedPlan = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.Same(callbackPlan, observedPlan);
		Assert.True(owner.HasSkillUseTask(8305));
		Assert.Equal(1, owner.PendingSkillUseTaskCount);
	}

	private static BindPointTeleportOperationPlan CreateReadyOperationPlan(int playerObjectId, int locId)
	{
		var pricePlan = BindPointTeleportPricePlanService.CreatePlan(
			hotspotId: locId,
			playerX: 100,
			playerY: 200,
			playerZ: 300,
			hotspotX: 100,
			hotspotY: 200,
			hotspotZ: 300,
			hotspotBasePrice: 1,
			priceSentByGameClient: 1);
		var requirementsPlan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: locId,
			playerWorldId: 210010000,
			hotspotWorldId: 210010000,
			playerRace: "ELYOS",
			hotspotRace: "ELYOS",
			currentKinah: 10_000,
			requiredPrice: pricePlan.FinalPrice);
		return BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId,
			locId,
			hotspotExists: true,
			pricePlan,
			requirementsPlan);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan()
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1,
			currentKinah: 10_000);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId: 8300,
			locId: 6400,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			sourcePlayerObjectId: 8300,
			SmBindPointTeleport.Cooldown(playerObjectId: 8300, locId: 6400, cooldownSeconds: 600));
		var movementPlan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			new BindPointTeleportDestinationFact(
				WorldId: 210010000,
				X: 100,
				Y: 200,
				Z: 300,
				Heading: 60,
				CurrentWorldId: 210010000,
				CurrentInstanceId: 1),
			playerIsDead: false,
			playerIsAboutToDie: false);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan);
	}

	private static ThreadPoolManager CreateThreadPoolManager()
	{
		return new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
	}
}
