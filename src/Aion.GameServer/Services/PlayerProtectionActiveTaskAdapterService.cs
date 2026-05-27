using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskAdapterAction
{
	Start,
	Stop,
}

public enum PlayerProtectionActiveTaskAdapterStatus
{
	DisabledPlanned,
	LiveVisualStarted,
	AlreadyProtected,
	LiveVisualStopped,
	LiveVisualStopUnspawned,
}

public sealed record PlayerProtectionActiveTaskAdapterRequest(
	Player Player,
	PlayerProtectionActiveTaskAdapterAction Action,
	bool ExecuteLiveVisualMutation = false,
	bool HasProtectionActiveTask = false,
	bool IsSpawned = true);

public sealed record PlayerProtectionActiveTaskAdapterResult(
	PlayerProtectionActiveTaskAdapterStatus Status,
	PlayerProtectionActiveTaskPlan Plan,
	bool MutatedVisualState,
	bool MutatedScheduler,
	bool SentPackets,
	bool ExposesPlanForObservation,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskAdapterService
{
	public static PlayerProtectionActiveTaskAdapterResult Apply(PlayerProtectionActiveTaskAdapterRequest request)
	{
		var plan = request.Action == PlayerProtectionActiveTaskAdapterAction.Start
			? PlayerProtectionActiveTaskPlanService.CreateStartPlan(request.Player)
			: PlayerProtectionActiveTaskPlanService.CreateStopPlan(
				request.Player,
				request.HasProtectionActiveTask,
				request.IsSpawned);

		if (!request.ExecuteLiveVisualMutation)
		{
			return new PlayerProtectionActiveTaskAdapterResult(
				PlayerProtectionActiveTaskAdapterStatus.DisabledPlanned,
				plan,
				MutatedVisualState: false,
				MutatedScheduler: false,
				SentPackets: false,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController protection task plan exposed with live visual mutation disabled",
				IsLive: false);
		}

		return request.Action == PlayerProtectionActiveTaskAdapterAction.Start
			? ApplyStart(request.Player, plan)
			: ApplyStop(request.Player, plan);
	}

	private static PlayerProtectionActiveTaskAdapterResult ApplyStart(
		Player player,
		PlayerProtectionActiveTaskPlan plan)
	{
		if (plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected)
		{
			return new PlayerProtectionActiveTaskAdapterResult(
				PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected,
				plan,
				MutatedVisualState: false,
				MutatedScheduler: false,
				SentPackets: false,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask already had BLINKING visual state",
				IsLive: true);
		}

		player.SetVisualState(PlayerVisualStates.Blinking);
		return new PlayerProtectionActiveTaskAdapterResult(
			PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted,
			plan,
			MutatedVisualState: true,
			MutatedScheduler: false,
			SentPackets: false,
			ExposesPlanForObservation: true,
			"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask -> setVisualState(BLINKING); scheduler and SM_PLAYER_STATE fanout remain planned",
			IsLive: true);
	}

	private static PlayerProtectionActiveTaskAdapterResult ApplyStop(
		Player player,
		PlayerProtectionActiveTaskPlan plan)
	{
		if (!plan.IsSpawned)
		{
			return new PlayerProtectionActiveTaskAdapterResult(
				PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned,
				plan,
				MutatedVisualState: false,
				MutatedScheduler: false,
				SentPackets: false,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController.stopProtectionActiveTask skips visual-state and packet fanout when player.isSpawned() is false",
				IsLive: true);
		}

		var mutatedVisualState = player.StopProtectionActive();
		return new PlayerProtectionActiveTaskAdapterResult(
			PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped,
			plan,
			mutatedVisualState,
			MutatedScheduler: false,
			SentPackets: false,
			ExposesPlanForObservation: true,
			"com.aionemu.gameserver.controllers.PlayerController.stopProtectionActiveTask -> unsetVisualState(BLINKING); scheduler, SM_PLAYER_STATE fanout, and notifyAIOnMove remain planned",
			IsLive: true);
	}
}
