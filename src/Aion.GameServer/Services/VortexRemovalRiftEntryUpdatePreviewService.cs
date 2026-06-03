namespace Aion.GameServer.Services;

public enum VortexRemovalRiftEntryUpdatePreviewStatus
{
	MissingRemoval,
	NoRemoval,
	Previewed,
}

public sealed record VortexRemovalRiftEntryUpdatePreview(
	VortexRemovalRiftEntryUpdatePreviewStatus Status,
	int LocationId,
	int RemovedPlayerObjectId,
	bool RemovedPassedPlayer,
	VortexRemovalRiftEntryUpdateReport? Report,
	bool ReadyForDispatch,
	bool SendsPackets,
	string JavaSource);

public sealed class VortexRemovalRiftEntryUpdatePreviewService(
	VortexRemovalRiftEntryUpdateReportService? reportService = null)
{
	private readonly VortexRemovalRiftEntryUpdateReportService _reportService =
		reportService ?? new VortexRemovalRiftEntryUpdateReportService();

	public Task<VortexRemovalRiftEntryUpdatePreview> PreviewAsync(
		VortexInvaderRemovalResult? removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock = null,
		CancellationToken cancellationToken = default)
	{
		return removal == null
			? Task.FromResult(CreateResult(
				VortexRemovalRiftEntryUpdatePreviewStatus.MissingRemoval,
				(VortexInvaderRemovalResult?)null,
				null))
			: PreviewInvaderAsync(removal, isMasterController, onlinePlayers, clock, cancellationToken);
	}

	public Task<VortexRemovalRiftEntryUpdatePreview> PreviewAsync(
		VortexDefenderRemovalResult? removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock = null,
		CancellationToken cancellationToken = default)
	{
		return removal == null
			? Task.FromResult(CreateResult(
				VortexRemovalRiftEntryUpdatePreviewStatus.MissingRemoval,
				(VortexDefenderRemovalResult?)null,
				null))
			: PreviewDefenderAsync(removal, isMasterController, onlinePlayers, clock, cancellationToken);
	}

	private async Task<VortexRemovalRiftEntryUpdatePreview> PreviewInvaderAsync(
		VortexInvaderRemovalResult removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock,
		CancellationToken cancellationToken)
	{
		if (!removal.Removed)
			return CreateResult(VortexRemovalRiftEntryUpdatePreviewStatus.NoRemoval, removal, null);

		var report = await _reportService.CreateReportAsync(
			removal,
			isMasterController,
			onlinePlayers,
			clock,
			cancellationToken);
		return CreateResult(VortexRemovalRiftEntryUpdatePreviewStatus.Previewed, removal, report);
	}

	private async Task<VortexRemovalRiftEntryUpdatePreview> PreviewDefenderAsync(
		VortexDefenderRemovalResult removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock,
		CancellationToken cancellationToken)
	{
		if (!removal.Removed)
			return CreateResult(VortexRemovalRiftEntryUpdatePreviewStatus.NoRemoval, removal, null);

		var report = await _reportService.CreateReportAsync(
			removal,
			isMasterController,
			onlinePlayers,
			clock,
			cancellationToken);
		return CreateResult(VortexRemovalRiftEntryUpdatePreviewStatus.Previewed, removal, report);
	}

	private static VortexRemovalRiftEntryUpdatePreview CreateResult(
		VortexRemovalRiftEntryUpdatePreviewStatus status,
		VortexInvaderRemovalResult? removal,
		VortexRemovalRiftEntryUpdateReport? report)
	{
		return new VortexRemovalRiftEntryUpdatePreview(
			status,
			removal?.LocationId ?? 0,
			removal?.PlayerObjectId ?? 0,
			removal?.RemovedPassedPlayer ?? false,
			report,
			report?.ReadyForDispatch ?? false,
			report?.SendsPackets ?? false,
			"services/VortexService.removeInvaderPlayer -> services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)");
	}

	private static VortexRemovalRiftEntryUpdatePreview CreateResult(
		VortexRemovalRiftEntryUpdatePreviewStatus status,
		VortexDefenderRemovalResult? removal,
		VortexRemovalRiftEntryUpdateReport? report)
	{
		return new VortexRemovalRiftEntryUpdatePreview(
			status,
			removal?.LocationId ?? 0,
			removal?.PlayerObjectId ?? 0,
			removal?.RemovedPassedPlayer ?? false,
			report,
			report?.ReadyForDispatch ?? false,
			report?.SendsPackets ?? false,
			"services/VortexService.removeDefenderPlayer -> services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)");
	}
}
