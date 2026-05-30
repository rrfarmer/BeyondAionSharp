using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerDeathWorkflowAdapterStatus
{
	DisabledPlanned,
	EarlyReturnPlanned,
	LiveStateTransitionApplied,
}

public sealed record PlayerDeathWorkflowAdapterRequest(Player Player, PlayerDeathWorkflowFacts Facts, bool ExecuteLiveStateMutation = false);

public sealed record PlayerDeathWorkflowAdapterResult(
	PlayerDeathWorkflowAdapterStatus Status,
	PlayerDeathWorkflowPlan Plan,
	PlayerDeathWorkflowReport Report,
	PlayerDeathStateTransitionResult? StateTransitionResult,
	bool MutatedPlayerState,
	bool SentPackets,
	bool ScheduledTasks,
	bool ExecutedExternalCallbacks,
	bool ExposesPlanForObservation,
	string JavaSource,
	bool IsLive
);

public sealed class PlayerDeathWorkflowAdapterService
{
	private readonly PlayerDeathWorkflowPlanService _planService;

	public PlayerDeathWorkflowAdapterService(PlayerDeathWorkflowPlanService? planService = null)
	{
		_planService = planService ?? new PlayerDeathWorkflowPlanService();
	}

	public PlayerDeathWorkflowAdapterResult Apply(PlayerDeathWorkflowAdapterRequest request)
	{
		// Java parity: PlayerController.onDie is the live outer entry point. This adapter exposes the
		// staged PlayerDeathWorkflowPlan plus report, and only applies the state-transition slice when
		// the caller explicitly opts into live mutation.
		var plan = _planService.CreatePlan(request.Player, request.Facts);
		var report = PlayerDeathWorkflowReportService.CreateReport(plan);
		if (!request.ExecuteLiveStateMutation)
		{
			return new PlayerDeathWorkflowAdapterResult(
				PlayerDeathWorkflowAdapterStatus.DisabledPlanned,
				plan,
				report,
				StateTransitionResult: null,
				MutatedPlayerState: false,
				SentPackets: false,
				ScheduledTasks: false,
				ExecutedExternalCallbacks: false,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController.onDie plan exposed with live state mutation disabled",
				IsLive: false
			);
		}

		if (!plan.Steps.Contains(PlayerDeathWorkflowStep.ApplyPlayerDeathStateTransition))
		{
			return new PlayerDeathWorkflowAdapterResult(
				PlayerDeathWorkflowAdapterStatus.EarlyReturnPlanned,
				plan,
				report,
				StateTransitionResult: null,
				MutatedPlayerState: false,
				SentPackets: false,
				ScheduledTasks: false,
				ExecutedExternalCallbacks: false,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.controllers.PlayerController.onDie returned before super.onDie; no player death state transition executed",
				IsLive: true
			);
		}

		var transition = PlayerDeathStateTransitionService.Apply(request.Player);
		return new PlayerDeathWorkflowAdapterResult(
			PlayerDeathWorkflowAdapterStatus.LiveStateTransitionApplied,
			plan,
			report,
			transition,
			MutatedPlayerState: true,
			SentPackets: false,
			ScheduledTasks: false,
			ExecutedExternalCallbacks: false,
			ExposesPlanForObservation: true,
			"com.aionemu.gameserver.controllers.PlayerController.onDie state transition applied; packet fanout, scheduler, callbacks, rewards, and quest dispatch remain planned",
			IsLive: true
		);
	}
}
