using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskSideEffectOperationPlanServiceTests
{
	[Fact]
	public void Create_StartOrdersVisualAttackFanoutAndSchedulerSideEffects()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var request = new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true);
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(request);

		var plan = PlayerProtectionActiveTaskSideEffectOperationPlanService.Create(request, adapterResult);

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Start, plan.Action);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StartProtection, plan.PlanStatus);
		Assert.True(plan.SchedulesDelayedStop);
		Assert.True(plan.CancelsKnownCreatureCasts);
		Assert.True(plan.ClearsKnownPlayerTargets);
		Assert.False(plan.CancelsExistingStopTask);
		Assert.False(plan.NotifiesAiOnMove);
		Assert.True(plan.HasLiveVisualMutationOnly);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSideEffectOperation.CheckProtectionActive,
				PlayerProtectionActiveTaskSideEffectOperation.SetBlinkingVisualState,
				PlayerProtectionActiveTaskSideEffectOperation.CancelCastOnKnownCreatures,
				PlayerProtectionActiveTaskSideEffectOperation.RemoveTargetFromKnownPlayers,
				PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState,
				PlayerProtectionActiveTaskSideEffectOperation.ScheduleDelayedStopTask,
				PlayerProtectionActiveTaskSideEffectOperation.StoreProtectionTask,
			],
			plan.Rows.Select(row => row.Operation));
		Assert.Equal(PlayerProtectionActiveTaskSideEffectOperationStatus.LiveVisualMutation, plan.Rows[1].Status);
		Assert.Contains(plan.Rows, row => row.JavaOperation.Contains("AttackUtil.cancelCastOn"));
		Assert.Contains(plan.Rows, row => row.JavaOperation.Contains("ThreadPoolManager"));
		Assert.True(player.IsProtectionActive());
	}

	[Fact]
	public void Create_AlreadyProtectedStartStopsAfterConditionCheck()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var request = new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true);
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(request);

		var plan = PlayerProtectionActiveTaskSideEffectOperationPlanService.Create(request, adapterResult);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.AlreadyProtected, plan.PlanStatus);
		Assert.False(plan.SchedulesDelayedStop);
		Assert.False(plan.CancelsKnownCreatureCasts);
		Assert.False(plan.ClearsKnownPlayerTargets);
		Assert.Single(plan.Rows);
		Assert.Equal(PlayerProtectionActiveTaskSideEffectOperation.CheckProtectionActive, plan.Rows[0].Operation);
		Assert.Contains("Already protected", plan.Rows[0].Notes);
	}

	[Fact]
	public void Create_StopOrdersCancelTaskSpawnedGuardVisualFanoutAndAiMove()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var request = new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true);
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(request);

		var plan = PlayerProtectionActiveTaskSideEffectOperationPlanService.Create(request, adapterResult);

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Stop, plan.Action);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtection, plan.PlanStatus);
		Assert.True(plan.CancelsExistingStopTask);
		Assert.True(plan.NotifiesAiOnMove);
		Assert.False(plan.SkipsAiMoveNotificationForFlightPath);
		Assert.True(plan.HasLiveVisualMutationOnly);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask,
				PlayerProtectionActiveTaskSideEffectOperation.CheckSpawned,
				PlayerProtectionActiveTaskSideEffectOperation.UnsetBlinkingVisualState,
				PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState,
				PlayerProtectionActiveTaskSideEffectOperation.NotifyAiOnMove,
			],
			plan.Rows.Select(row => row.Operation));
		Assert.Equal(PlayerProtectionActiveTaskSideEffectOperationStatus.LiveVisualMutation, plan.Rows[2].Status);
		Assert.Contains(plan.Rows, row => row.JavaSource.Contains("MovementNotifyTask"));
		Assert.False(player.IsProtectionActive());
	}

	[Fact]
	public void Create_UnspawnedStopStopsAfterCancelTaskAndSpawnedGuard()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var request = new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: false,
			IsSpawned: false);
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(request);

		var plan = PlayerProtectionActiveTaskSideEffectOperationPlanService.Create(request, adapterResult);

		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned, plan.PlanStatus);
		Assert.True(plan.CancelsExistingStopTask);
		Assert.False(plan.NotifiesAiOnMove);
		Assert.False(plan.HasLiveVisualMutationOnly);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask,
				PlayerProtectionActiveTaskSideEffectOperation.CheckSpawned,
			],
			plan.Rows.Select(row => row.Operation));
		Assert.Contains("Unspawned", plan.Rows[1].Notes);
		Assert.True(player.IsProtectionActive());
	}

	[Fact]
	public void Create_StopSkipsAiMoveNotificationWhileUsingFlightPath()
	{
		var player = new Player { ObjectId = PlayerObjectId, FlightPathType = PlayerFlightPathType.Windstream };
		player.SetVisualState(PlayerVisualStates.Blinking);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		var request = new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true);
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(request);

		var plan = PlayerProtectionActiveTaskSideEffectOperationPlanService.Create(request, adapterResult);

		Assert.False(plan.NotifiesAiOnMove);
		Assert.True(plan.SkipsAiMoveNotificationForFlightPath);
		Assert.Equal(PlayerProtectionActiveTaskSideEffectOperation.SkipNotifyAiOnMoveForFlightPath, plan.Rows[^1].Operation);
		Assert.Equal(PlayerProtectionActiveTaskSideEffectOperationStatus.SkippedBranch, plan.Rows[^1].Status);
		Assert.Contains("flight transporter or windstream", plan.Rows[^1].JavaSource);
	}

	private const int PlayerObjectId = 1001;
}
