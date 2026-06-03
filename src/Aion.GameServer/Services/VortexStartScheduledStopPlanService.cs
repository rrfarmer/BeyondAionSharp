namespace Aion.GameServer.Services;

public sealed class VortexStartScheduledStopPlanService
{
	public VortexStartScheduledStopPlan CreatePlan(
		VortexStartInvasionCoordinatorReport startReport,
		int durationHours)
	{
		ArgumentNullException.ThrowIfNull(startReport);

		if (!startReport.Started || startReport.Status != VortexStartInvasionCoordinatorStatus.Planned)
		{
			return new VortexStartScheduledStopPlan(
				VortexStartScheduledStopPlanStatus.NotScheduledAlreadyStarted,
				startReport.LocationId,
				DurationHours: 0,
				DurationSource: "services/VortexService.getDuration",
				JavaSource: "services/VortexService.startInvasion");
		}

		return new VortexStartScheduledStopPlan(
			VortexStartScheduledStopPlanStatus.Planned,
			startReport.LocationId,
			durationHours,
			DurationSource: "configs/main/CustomConfig.VORTEX_DURATION",
			JavaSource: "services/VortexService.startInvasion -> ThreadPoolManager.schedule(stopInvasion, getDuration(), TimeUnit.HOURS)");
	}
}

public enum VortexStartScheduledStopPlanStatus
{
	NotScheduledAlreadyStarted,
	Planned,
}

public sealed record VortexStartScheduledStopPlan(
	VortexStartScheduledStopPlanStatus Status,
	int LocationId,
	int DurationHours,
	string DurationSource,
	string JavaSource)
{
	public bool ShouldScheduleLiveStop => false;
	public bool HasScheduleIntent => Status == VortexStartScheduledStopPlanStatus.Planned;
	public string TimeUnit => "HOURS";
	public string ScheduledMethod => "services/VortexService.stopInvasion";
}
