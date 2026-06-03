using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRemovalRiftEntryUpdatePreviewServiceTests
{
	[Fact]
	public async Task PreviewAsync_InvaderRemovalBuildsDisabledReportWithoutAutoDispatch()
	{
		var portal = CreateVortexPortal();
		var removal = CreateInvaderRemoval(portal, passedPlayerCount: 2);
		var service = new VortexRemovalRiftEntryUpdatePreviewService();

		var preview = await service.PreviewAsync(
			removal,
			isMasterController: true,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 400010000),
			],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRemovalRiftEntryUpdatePreviewStatus.Previewed, preview.Status);
		Assert.Equal(0, preview.LocationId);
		Assert.Equal(1002, preview.RemovedPlayerObjectId);
		Assert.True(preview.RemovedPassedPlayer);
		Assert.True(preview.ReadyForDispatch);
		Assert.False(preview.SendsPackets);
		var report = Assert.IsType<VortexRemovalRiftEntryUpdateReport>(preview.Report);
		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.ReadyNoDispatch, report.Status);
		Assert.False(report.DidCallDispatch);
		Assert.False(report.SendsPackets);
		Assert.Equal([210060000, 120080000], report.WorldIds);
		Assert.Equal([100, 101], report.TargetPlayerObjectIds);
		Assert.Equal(2, portal.UsedEntries);
	}

	[Fact]
	public async Task PreviewAsync_DefenderRemovalBuildsDisabledReportWithoutAutoDispatch()
	{
		var portal = CreateVortexPortal();
		var removal = CreateDefenderRemoval(portal, passedPlayerCount: 1);
		var service = new VortexRemovalRiftEntryUpdatePreviewService();

		var preview = await service.PreviewAsync(
			removal,
			isMasterController: false,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
			],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRemovalRiftEntryUpdatePreviewStatus.Previewed, preview.Status);
		Assert.Equal(1004, preview.RemovedPlayerObjectId);
		Assert.False(preview.RemovedPassedPlayer);
		Assert.True(preview.ReadyForDispatch);
		Assert.False(preview.SendsPackets);
		var report = Assert.IsType<VortexRemovalRiftEntryUpdateReport>(preview.Report);
		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.ReadyNoDispatch, report.Status);
		Assert.Equal([120080000], report.WorldIds);
		Assert.Equal([101], report.TargetPlayerObjectIds);
		Assert.Equal(1, portal.UsedEntries);
	}

	[Fact]
	public async Task PreviewAsync_MissingOrNoRemovalDoesNotBuildReport()
	{
		var service = new VortexRemovalRiftEntryUpdatePreviewService();

		var missing = await service.PreviewAsync(
			(VortexInvaderRemovalResult?)null,
			isMasterController: true,
			[]);
		var noRemoval = await service.PreviewAsync(
			new VortexInvaderRemovalResult(
				Removed: false,
				PlayerObjectId: 1002,
				LocationId: 0,
				RemovedPassedPlayer: false,
				WasOnline: true,
				WasInInvasionWorld: false,
				JavaSource: "services/VortexService.removeInvaderPlayer"),
			isMasterController: true,
			[]);

		Assert.Equal(VortexRemovalRiftEntryUpdatePreviewStatus.MissingRemoval, missing.Status);
		Assert.Equal(VortexRemovalRiftEntryUpdatePreviewStatus.NoRemoval, noRemoval.Status);
		Assert.Null(missing.Report);
		Assert.Null(noRemoval.Report);
		Assert.False(missing.ReadyForDispatch);
		Assert.False(noRemoval.ReadyForDispatch);
		Assert.False(missing.SendsPackets);
		Assert.False(noRemoval.SendsPackets);
	}

	[Fact]
	public async Task PreviewAsync_MissingActivePortalPreservesReportGuardState()
	{
		var removal = CreateInvaderRemoval(activePortal: null, passedPlayerCount: 2);
		var service = new VortexRemovalRiftEntryUpdatePreviewService();

		var preview = await service.PreviewAsync(
			removal,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		Assert.Equal(VortexRemovalRiftEntryUpdatePreviewStatus.Previewed, preview.Status);
		Assert.False(preview.ReadyForDispatch);
		Assert.False(preview.SendsPackets);
		var report = Assert.IsType<VortexRemovalRiftEntryUpdateReport>(preview.Report);
		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.MissingActivePortal, report.Status);
		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.MissingPortal, report.PipelinePlan?.Status);
	}

	private static VortexInvaderRemovalResult CreateInvaderRemoval(
		RiftPortalState? activePortal,
		int passedPlayerCount)
	{
		return new VortexInvaderRemovalResult(
			Removed: true,
			PlayerObjectId: 1002,
			LocationId: 0,
			RemovedPassedPlayer: true,
			WasOnline: true,
			WasInInvasionWorld: true,
			JavaSource: "services/VortexService.removeInvaderPlayer -> services/vortex/Invasion.kickPlayer",
			PassedPlayerSyncPlan: CreateSyncPlan(passedPlayerCount),
			ActivePortal: activePortal);
	}

	private static VortexDefenderRemovalResult CreateDefenderRemoval(
		RiftPortalState? activePortal,
		int passedPlayerCount)
	{
		return new VortexDefenderRemovalResult(
			Removed: true,
			PlayerObjectId: 1004,
			LocationId: 0,
			RemovedPassedPlayer: false,
			WasOnline: true,
			JavaSource: "services/VortexService.removeDefenderPlayer -> services/vortex/Invasion.kickPlayer",
			PassedPlayerSyncPlan: CreateSyncPlan(passedPlayerCount),
			ActivePortal: activePortal);
	}

	private static VortexPassedPlayerSyncPlan CreateSyncPlan(int passedPlayerCount)
	{
		return new VortexPassedPlayerSyncPlan(
			LocationId: 0,
			PassedPlayerCount: passedPlayerCount,
			UsePassedPlayerCount: true,
			JavaSource: "services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)");
	}

	private static RiftPortalState CreateVortexPortal()
	{
		var definition = new RiftDefinition(
			1170,
			"MARCHUTAN",
			"MARCHUTAN_AM",
			"MARCHUTAN_AS",
			2,
			45,
			65,
			"ASMODIANS",
			IsVortex: true);
		var template = new NpcTemplateSummary(831143, "Vortex", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 831143,
			Template: template,
			Position: new WorldPosition(210060000, 10, 20, 30, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 831144,
			Template: template,
			Position: new WorldPosition(120080000, 40, 50, 60, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 9200);
	}
}
