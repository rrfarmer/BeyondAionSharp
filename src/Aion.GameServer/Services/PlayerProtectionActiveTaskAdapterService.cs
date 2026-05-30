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
	bool IsSpawned = true,
	PlayerKnownListMembershipSnapshot? SourceKnownListSnapshot = null,
	IReadOnlyList<PlayerProtectionActiveTaskRecipientVisibilityFact>? RecipientVisibilityFacts = null
);

public sealed record PlayerProtectionActiveTaskAdapterResult(
	PlayerProtectionActiveTaskAdapterStatus Status,
	PlayerProtectionActiveTaskPlan Plan,
	PlayerProtectionActiveTaskFanoutPlan FanoutPlan,
	PlayerProtectionActiveTaskSightedRecipientTrace SightedRecipientTrace,
	PlayerProtectionActiveTaskReport Report,
	bool MutatedVisualState,
	bool MutatedScheduler,
	bool SentPackets,
	bool ExposesPlanForObservation,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskAdapterService
{
	public static PlayerProtectionActiveTaskAdapterResult Apply(PlayerProtectionActiveTaskAdapterRequest request)
	{
		// Java parity: PlayerController.startProtectionActiveTask and stopProtectionActiveTask mutate the
		// BLINKING visual state immediately, then rely on scheduled expiry and SM_PLAYER_STATE fanout.
		// This adapter exposes the staged plan and only applies the live visual mutation slice when enabled.
		var action =
			request.Action == PlayerProtectionActiveTaskAdapterAction.Start
				? PlayerProtectionActiveTaskFanoutAction.Start
				: PlayerProtectionActiveTaskFanoutAction.Stop;
		var plan =
			request.Action == PlayerProtectionActiveTaskAdapterAction.Start
				? PlayerProtectionActiveTaskPlanService.CreateStartPlan(request.Player)
				: PlayerProtectionActiveTaskPlanService.CreateStopPlan(request.Player, request.HasProtectionActiveTask, request.IsSpawned);
		var fanoutPlan = PlayerProtectionActiveTaskFanoutPlanService.Create(plan, action);
		var sightedRecipientTrace = PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(
			fanoutPlan,
			request.SourceKnownListSnapshot,
			request.RecipientVisibilityFacts
		);

		if (!request.ExecuteLiveVisualMutation)
		{
			return CreateResult(
				PlayerProtectionActiveTaskAdapterStatus.DisabledPlanned,
				plan,
				fanoutPlan,
				sightedRecipientTrace,
				mutatedVisualState: false,
				mutatedScheduler: false,
				sentPackets: false,
				exposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController protection task plan exposed with live visual mutation disabled",
				isLive: false
			);
		}

		return request.Action == PlayerProtectionActiveTaskAdapterAction.Start
			? ApplyStart(request.Player, plan, fanoutPlan, sightedRecipientTrace)
			: ApplyStop(request.Player, plan, fanoutPlan, sightedRecipientTrace);
	}

	private static PlayerProtectionActiveTaskAdapterResult ApplyStart(
		Player player,
		PlayerProtectionActiveTaskPlan plan,
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan,
		PlayerProtectionActiveTaskSightedRecipientTrace sightedRecipientTrace
	)
	{
		if (plan.Status == PlayerProtectionActiveTaskPlanStatus.AlreadyProtected)
		{
			return CreateResult(
				PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected,
				plan,
				fanoutPlan,
				sightedRecipientTrace,
				mutatedVisualState: false,
				mutatedScheduler: false,
				sentPackets: false,
				exposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask already had BLINKING visual state",
				isLive: true
			);
		}

		player.SetVisualState(PlayerVisualStates.Blinking);
		return CreateResult(
			PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted,
			plan,
			fanoutPlan,
			sightedRecipientTrace,
			mutatedVisualState: true,
			mutatedScheduler: false,
			sentPackets: false,
			exposesPlanForObservation: true,
			"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask -> setVisualState(BLINKING); scheduler and SM_PLAYER_STATE fanout remain planned",
			isLive: true
		);
	}

	private static PlayerProtectionActiveTaskAdapterResult ApplyStop(
		Player player,
		PlayerProtectionActiveTaskPlan plan,
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan,
		PlayerProtectionActiveTaskSightedRecipientTrace sightedRecipientTrace
	)
	{
		if (!plan.IsSpawned)
		{
			return CreateResult(
				PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned,
				plan,
				fanoutPlan,
				sightedRecipientTrace,
				mutatedVisualState: false,
				mutatedScheduler: false,
				sentPackets: false,
				exposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController.stopProtectionActiveTask skips visual-state and packet fanout when player.isSpawned() is false",
				isLive: true
			);
		}

		var mutatedVisualState = player.StopProtectionActive();
		return CreateResult(
			PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped,
			plan,
			fanoutPlan,
			sightedRecipientTrace,
			mutatedVisualState,
			mutatedScheduler: false,
			sentPackets: false,
			exposesPlanForObservation: true,
			"com.aionemu.gameserver.controllers.PlayerController.stopProtectionActiveTask -> unsetVisualState(BLINKING); scheduler, SM_PLAYER_STATE fanout, and notifyAIOnMove remain planned",
			isLive: true
		);
	}

	private static PlayerProtectionActiveTaskAdapterResult CreateResult(
		PlayerProtectionActiveTaskAdapterStatus status,
		PlayerProtectionActiveTaskPlan plan,
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan,
		PlayerProtectionActiveTaskSightedRecipientTrace sightedRecipientTrace,
		bool mutatedVisualState,
		bool mutatedScheduler,
		bool sentPackets,
		bool exposesPlanForObservation,
		string javaSource,
		bool isLive
	)
	{
		var report = PlayerProtectionActiveTaskReportService.CreateReport(status, plan, fanoutPlan, mutatedVisualState, isLive);

		return new PlayerProtectionActiveTaskAdapterResult(
			status,
			plan,
			fanoutPlan,
			sightedRecipientTrace,
			report,
			mutatedVisualState,
			mutatedScheduler,
			sentPackets,
			exposesPlanForObservation,
			javaSource,
			isLive
		);
	}
}
