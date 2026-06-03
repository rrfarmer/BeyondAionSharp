using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRemovalRiftEntryUpdateReportServiceTests
{
	[Fact]
	public async Task CreateReportAsync_DisabledBridgeComposesReadyInvaderRemovalWithoutDispatching()
	{
		var portal = CreateVortexPortal();
		var removal = CreateInvaderRemoval(portal, passedPlayerCount: 2);
		var service = new VortexRemovalRiftEntryUpdateReportService();

		var report = await service.CreateReportAsync(
			removal,
			isMasterController: true,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 400010000),
			],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.ReadyNoDispatch, report.Status);
		Assert.Equal(0, report.LocationId);
		Assert.Equal(1002, report.RemovedPlayerObjectId);
		Assert.True(report.RemovedPassedPlayer);
		Assert.True(report.ReadyForDispatch);
		Assert.False(report.DidCallDispatch);
		Assert.False(report.SendsPackets);
		Assert.Same(portal, report.ActivePortal);
		Assert.Equal([210060000, 120080000], report.WorldIds);
		Assert.Equal([100, 101], report.TargetPlayerObjectIds);
		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.Ready, report.PipelinePlan?.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.DisabledNoDispatch, report.BridgeResult?.Status);
		Assert.Equal(2, portal.UsedEntries);
	}

	[Fact]
	public async Task CreateReportAsync_ComposesReadyDefenderRemovalWithActivePortalMetadata()
	{
		var portal = CreateVortexPortal();
		var removal = CreateDefenderRemoval(portal, passedPlayerCount: 1);
		var service = new VortexRemovalRiftEntryUpdateReportService();

		var report = await service.CreateReportAsync(
			removal,
			isMasterController: false,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
			],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.ReadyNoDispatch, report.Status);
		Assert.Equal(1004, report.RemovedPlayerObjectId);
		Assert.False(report.RemovedPassedPlayer);
		Assert.True(report.ReadyForDispatch);
		Assert.False(report.DidCallDispatch);
		Assert.Same(portal, report.ActivePortal);
		Assert.Equal([120080000], report.WorldIds);
		Assert.Equal([101], report.TargetPlayerObjectIds);
		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.DisabledNoDispatch, report.BridgeResult?.Status);
		Assert.Equal(1, portal.UsedEntries);
	}

	[Fact]
	public async Task CreateReportAsync_MissingOrUnremovedInputsDoNotBuildPipeline()
	{
		var service = new VortexRemovalRiftEntryUpdateReportService();

		var missing = await service.CreateReportAsync(
			(VortexInvaderRemovalResult?)null,
			isMasterController: true,
			[]);
		var noRemoval = await service.CreateReportAsync(
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

		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.MissingRemoval, missing.Status);
		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.NoRemoval, noRemoval.Status);
		Assert.Null(missing.PipelinePlan);
		Assert.Null(noRemoval.PipelinePlan);
		Assert.Null(missing.BridgeResult);
		Assert.Null(noRemoval.BridgeResult);
		Assert.False(missing.ReadyForDispatch);
		Assert.False(noRemoval.ReadyForDispatch);
	}

	[Fact]
	public async Task CreateReportAsync_MissingSyncPlanDoesNotBuildPipeline()
	{
		var removal = new VortexInvaderRemovalResult(
			Removed: true,
			PlayerObjectId: 1002,
			LocationId: 0,
			RemovedPassedPlayer: true,
			WasOnline: true,
			WasInInvasionWorld: true,
			JavaSource: "services/vortex/Invasion.kickPlayer",
			ActivePortal: CreateVortexPortal());
		var service = new VortexRemovalRiftEntryUpdateReportService();

		var report = await service.CreateReportAsync(
			removal,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.MissingSyncPlan, report.Status);
		Assert.Null(report.PipelinePlan);
		Assert.Null(report.BridgeResult);
		Assert.False(report.ReadyForDispatch);
		Assert.False(report.DidCallDispatch);
	}

	[Fact]
	public async Task CreateReportAsync_MissingActivePortalBuildsGuardPipelineAndBridgeReport()
	{
		var removal = CreateInvaderRemoval(activePortal: null, passedPlayerCount: 2);
		var service = new VortexRemovalRiftEntryUpdateReportService();

		var report = await service.CreateReportAsync(
			removal,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.MissingActivePortal, report.Status);
		Assert.False(report.ReadyForDispatch);
		Assert.False(report.DidCallDispatch);
		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.MissingPortal, report.PipelinePlan?.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.NotReady, report.BridgeResult?.Status);
		Assert.Empty(report.WorldIds);
		Assert.Empty(report.TargetPlayerObjectIds);
	}

	[Fact]
	public async Task CreateReportAsync_NoTargetPlayersBuildsNotReadyReportWithoutDispatch()
	{
		var portal = CreateVortexPortal();
		var removal = CreateInvaderRemoval(portal, passedPlayerCount: 2);
		var service = new VortexRemovalRiftEntryUpdateReportService();

		var report = await service.CreateReportAsync(
			removal,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 400010000)],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRemovalRiftEntryUpdateReportStatus.NotReady, report.Status);
		Assert.False(report.ReadyForDispatch);
		Assert.False(report.DidCallDispatch);
		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.NoTargetPlayers, report.PipelinePlan?.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionDispatchBridgeStatus.NotReady, report.BridgeResult?.Status);
		Assert.Equal([210060000, 120080000], report.WorldIds);
		Assert.Empty(report.TargetPlayerObjectIds);
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
