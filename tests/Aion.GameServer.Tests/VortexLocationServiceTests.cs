using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexLocationServiceTests
{
	[Fact]
	public async Task GetLocationByWorld_MapsJavaVortexWorldIds()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-location-world-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var service = new VortexLocationService(context);

			var theobomos = service.GetLocationByWorld(210060000);
			var brusthonin = service.GetLocationByWorld(220050000);

			Assert.NotNull(theobomos);
			Assert.Equal(0, theobomos.Id);
			Assert.Equal(new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0), theobomos.StartPoint);
			Assert.NotNull(brusthonin);
			Assert.Equal(1, brusthonin.Id);
			Assert.Equal(new WorldPosition(220050000, 2242.0f, 2797.0f, 75.4f, 0), brusthonin.StartPoint);
			Assert.Null(service.GetLocationByWorld(110070000));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task GetLocationByRift_MapsJavaVortexMasterNpcIds()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-location-rift-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var service = new VortexLocationService(context);

			var kaisinelDestination = service.GetLocationByRift(831141);
			var marchutanDestination = service.GetLocationByRift(831143);

			Assert.NotNull(kaisinelDestination);
			Assert.Equal(1, kaisinelDestination.Id);
			Assert.Equal(new WorldPosition(220050000, 2242.0f, 2797.0f, 75.4f, 0), service.GetStartPointByRift(831141));
			Assert.NotNull(marchutanDestination);
			Assert.Equal(0, marchutanDestination.Id);
			Assert.Equal(new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0), service.GetStartPointByRift(831143));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartInvasion_CanCarryActivePortalReferenceLikeJavaVortexController()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-active-portal-start-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();

			var snapshot = runtime.StartInvasion(location, portal);

			Assert.True(snapshot.HasActivePortal);
			Assert.Same(portal, snapshot.ActivePortal);
			Assert.Same(portal, Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id)).ActivePortal);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartInvasionWithResult_RepeatedStartPreservesStateLikeJavaDoubleStartGuard()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-double-start-result-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var initialPortal = CreateVortexPortal(location);
			var replacementPortal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);
			var defender = CreatePlayer(1004, isOnline: false, location.InvasionWorldId);

			var started = runtime.StartInvasionWithResult(location, initialPortal);
			Assert.True(runtime.AddInvader(location.Id, invader));
			Assert.True(runtime.AddDefender(location.Id, defender));
			var repeated = runtime.StartInvasionWithResult(location, replacementPortal);

			Assert.True(started.Started);
			Assert.Equal(VortexStartInvasionStatus.Started, started.Status);
			Assert.Equal("services/vortex/DimensionalVortex.start -> services/vortex/Invasion.startInvasion", started.JavaSource);
			Assert.False(repeated.Started);
			Assert.Equal(VortexStartInvasionStatus.AlreadyStarted, repeated.Status);
			Assert.Equal("services/vortex/DimensionalVortex.start", repeated.JavaSource);
			Assert.Same(initialPortal, repeated.Snapshot.ActivePortal);
			Assert.Equal([1002], repeated.Snapshot.InvaderObjectIds);
			Assert.Equal([1004], repeated.Snapshot.DefenderObjectIds);
			var current = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Same(initialPortal, current.ActivePortal);
			Assert.NotSame(replacementPortal, current.ActivePortal);
			Assert.Equal([1002], current.InvaderObjectIds);
			Assert.Equal([1004], current.DefenderObjectIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartInvasion_RepeatedSnapshotCallPreservesActivePortalAndParticipants()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-double-start-snapshot-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var initialPortal = CreateVortexPortal(location);
			var replacementPortal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);

			var first = runtime.StartInvasion(location, initialPortal);
			Assert.True(runtime.AddInvader(location.Id, invader));
			var repeated = runtime.StartInvasion(location, replacementPortal);

			Assert.Same(initialPortal, first.ActivePortal);
			Assert.Same(initialPortal, repeated.ActivePortal);
			Assert.Equal([1002], repeated.InvaderObjectIds);
			Assert.Same(initialPortal, Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id)).ActivePortal);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartSideEffectPlan_PreservesJavaStartOrderWithoutExecutingLiveEffects()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-plan-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var start = runtime.StartInvasionWithResult(location, portal);
			var planner = new VortexStartInvasionSideEffectPlanService();
			var existingPeaceNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831500,
				Template: new NpcTemplateSummary(831500, "Existing vortex peace", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.HomePoint);
			var invasionSpawn = VortexStartInvasionSpawnSnapshot.FromVortexSpawn(
				CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"));

			var plan = planner.CreatePlan(
				start,
				[VortexStartSpawnedNpcSnapshot.FromWorldNpc(existingPeaceNpc)],
				[invasionSpawn]);

			Assert.Equal(VortexStartInvasionSideEffectPlanStatus.Planned, plan.Status);
			Assert.False(plan.ShouldExecuteLiveSideEffects);
			Assert.Same(start, plan.StartResult);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(1, plan.DespawnNpcCount);
			Assert.Equal(1, plan.InvasionSpawnCount);
			Assert.Equal("services/vortex/Invasion.startInvasion", plan.JavaSource);
			Assert.Equal(
				[
					VortexStartInvasionSideEffectStepKind.SetActiveVortex,
					VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpcs,
					VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpc,
					VortexStartInvasionSideEffectStepKind.SpawnInvasionNpc,
					VortexStartInvasionSideEffectStepKind.InitRiftGenerator,
					VortexStartInvasionSideEffectStepKind.UpdateDefenderAlliance,
				],
				plan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Equal(831500, plan.OrderedSteps[2].NpcId);
			var spawnStep = plan.OrderedSteps.Single(step => step.Kind == VortexStartInvasionSideEffectStepKind.SpawnInvasionNpc);
			Assert.Equal(VortexStateType.Invasion, spawnStep.VortexState);
			Assert.Equal(831600, Assert.IsType<NpcSpawnSummary>(spawnStep.Spawn).NpcId);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartSideEffectPlan_AlreadyStartedReturnsGuardMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-plan-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var planner = new VortexStartInvasionSideEffectPlanService();
			runtime.StartInvasionWithResult(location);

			var repeated = runtime.StartInvasionWithResult(location);
			var plan = planner.CreatePlan(
				repeated,
				[VortexStartSpawnedNpcSnapshot.FromWorldNpc(new WorldNpc(
					ObjectId: 7201,
					TemplateId: 831500,
					Template: new NpcTemplateSummary(831500, "Ignored", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
					Position: location.HomePoint))],
				[VortexStartInvasionSpawnSnapshot.FromVortexSpawn(
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "ignored"))]);

			Assert.Equal(VortexStartInvasionSideEffectPlanStatus.AlreadyStarted, plan.Status);
			Assert.False(plan.ShouldExecuteLiveSideEffects);
			Assert.Same(repeated, plan.StartResult);
			Assert.Empty(plan.OrderedSteps);
			Assert.Equal(0, plan.DespawnNpcCount);
			Assert.Equal(0, plan.InvasionSpawnCount);
			Assert.Equal("services/vortex/DimensionalVortex.start", plan.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public void InvasionSpawnSelection_SelectsJavaInvasionRowsForVortexLocation()
	{
		var selector = new VortexInvasionSpawnSnapshotSelectionService();
		var table = new NpcVortexSpawnTable(
			[
				CreateVortexSpawn(0, 0, 0, VortexStateType.Peace, 831500, "peace-a"),
				CreateVortexSpawn(0, 0, 1, VortexStateType.Invasion, 831600, "invasion-a"),
				CreateVortexSpawn(0, 1, 0, VortexStateType.Invasion, 831601, "invasion-b"),
				CreateVortexSpawn(1, 0, 0, VortexStateType.Invasion, 831700, "other-location"),
			]);
		var peaceSpawn = CreateVortexSpawn(0, 0, 0, VortexStateType.Peace, 831500, "peace-a");

		var selected = selector.SelectInvasionSpawns(0, table);

		Assert.Equal([831600, 831601], selected.Select(spawn => spawn.Spawn.NpcId).ToArray());
		Assert.All(selected, spawn => Assert.Equal(VortexStateType.Invasion, spawn.State));
		Assert.Throws<ArgumentException>(() => VortexStartInvasionSpawnSnapshot.FromVortexSpawn(peaceSpawn));
	}

	[Fact]
	public async Task StartSnapshotCollector_PreparesRuntimeStaticRequestWithDefenderAllianceMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-collector-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var collector = new VortexStartInvasionRuntimeSnapshotCollectorService();
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831500,
				Template: new NpcTemplateSummary(831500, "Existing vortex peace", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.HomePoint);
			var defender = CreatePlayer(1004, isOnline: true, location.InvasionWorldId, location.DefendersRace);
			var offlineDefender = CreatePlayer(1006, isOnline: false, location.InvasionWorldId, location.DefendersRace);
			var existingDefender = CreatePlayer(1007, isOnline: true, location.InvasionWorldId, location.DefendersRace);
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId, location.InvadersRace);
			Assert.True(offlineDefender.ResponseRequester.PutRequest(
				SmQuestionWindow.VortexDefenderInvitation,
				new QuestionResponseRequest(9001, QuestionResponseRequestKind.Unknown)));
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Peace, 831500, "static-peace"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Invasion, 831600, "static-invasion"),
				]);

			var request = collector.PrepareWithStaticInvasionSpawns(
				location,
				table,
				[spawnedNpc],
				[invader, defender, offlineDefender, existingDefender],
				existingDefenders: [new VortexDefenderAddPlayerSnapshot(1007, IsInGroup: false, IsInAlliance: false)],
				defenderAlliance: VortexDefenderAllianceSnapshot.Open);

			Assert.True(request.HasAnySnapshot);
			Assert.Equal([831500], request.SpawnedNpcSnapshots.Select(npc => npc.NpcId).ToArray());
			Assert.Equal([831600], request.InvasionSpawnSnapshots.Select(spawn => spawn.Spawn.NpcId).ToArray());
			var defenderPlan = Assert.IsType<VortexDefenderAllianceUpdatePlan>(request.DefenderAllianceUpdatePlan);
			Assert.Equal(location.Id, defenderPlan.LocationId);
			Assert.Equal([1004, 1006, 1007], defenderPlan.DefenderObjectIds);
			Assert.Equal([1002], defenderPlan.SkippedObjectIds);
			Assert.True(defenderPlan.WouldCallUpdateDefenders);
			Assert.False(defenderPlan.ShouldMutateLiveAlliance);
			var batchPlan = Assert.IsType<VortexDefenderInvitationBatchPlan>(request.DefenderInvitationBatchPlan);
			Assert.Equal([1004, 1006, 1007], batchPlan.DefenderObjectIds);
			Assert.Equal([1007], batchPlan.ExistingDefenderObjectIds);
			Assert.Equal(3, batchPlan.InvitationPlanCount);
			Assert.Equal(1, batchPlan.QuestionWindowIntentCount);
			Assert.Equal(1, batchPlan.RequestNotStoredCount);
			Assert.Equal(1, batchPlan.AlreadyDefenderCount);
			Assert.Equal(0, batchPlan.AllianceFullCount);
			Assert.False(batchPlan.ShouldMutateLiveRequest);
			Assert.False(batchPlan.ShouldSendLivePacket);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartCoordinator_StaticInvasionSpawnsEnrichPlanOnlyAfterStartGuardSucceeds()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-coordinator-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var selector = new CountingInvasionSpawnSelector();
			var coordinator = new VortexStartInvasionCoordinatorService(
				runtime,
				new VortexStartInvasionSideEffectPlanService(),
				selector);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Peace, 831500, "static-peace"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Invasion, 831600, "static-invasion"),
					CreateVortexSpawn(location.Id + 1, 0, 0, VortexStateType.Invasion, 831700, "other-location"),
				]);

			var report = coordinator.StartInvasion(
				location,
				portal,
				VortexStartInvasionSnapshotRequest.Empty,
				table);

			Assert.Equal(VortexStartInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Started);
			Assert.True(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			Assert.Equal(1, selector.CallCount);
			Assert.Equal([location.Id], selector.LocationIds);
			Assert.Same(portal, report.StartResult.Snapshot.ActivePortal);
			Assert.Equal(1, report.SideEffectPlan.InvasionSpawnCount);
			Assert.Equal(
				[
					VortexStartInvasionSideEffectStepKind.SetActiveVortex,
					VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpcs,
					VortexStartInvasionSideEffectStepKind.SpawnInvasionNpc,
					VortexStartInvasionSideEffectStepKind.InitRiftGenerator,
					VortexStartInvasionSideEffectStepKind.UpdateDefenderAlliance,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Equal(831600, Assert.IsType<NpcSpawnSummary>(
				report.SideEffectPlan.OrderedSteps.Single(step => step.Kind == VortexStartInvasionSideEffectStepKind.SpawnInvasionNpc).Spawn).NpcId);
			Assert.Equal(
				"services/VortexService.startInvasion -> services/vortex/DimensionalVortex.start -> services/vortex/Invasion.startInvasion",
				report.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartCoordinator_PreparedRuntimeStaticRequestCarriesDefenderAllianceUpdateMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-coordinator-prepared-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStartInvasionCoordinatorService(
				runtime,
				new VortexStartInvasionSideEffectPlanService());
			var collector = new VortexStartInvasionRuntimeSnapshotCollectorService();
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831500,
				Template: new NpcTemplateSummary(831500, "Existing vortex peace", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.HomePoint);
			var defender = CreatePlayer(1004, isOnline: true, location.InvasionWorldId, location.DefendersRace);
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId, location.InvadersRace);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "static-invasion"),
				]);
			var request = collector.PrepareWithStaticInvasionSpawns(
				location,
				table,
				[spawnedNpc],
				[invader, defender]);

			var report = coordinator.StartInvasion(location, request);

			Assert.Equal(VortexStartInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Started);
			Assert.True(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			Assert.True(report.SideEffectPlan.HasDefenderAllianceUpdatePlan);
			Assert.True(report.SideEffectPlan.HasDefenderInvitationBatchPlan);
			Assert.Same(request.DefenderAllianceUpdatePlan, report.SideEffectPlan.DefenderAllianceUpdatePlan);
			Assert.Same(request.DefenderInvitationBatchPlan, report.SideEffectPlan.DefenderInvitationBatchPlan);
			Assert.Equal(1, report.SideEffectPlan.DespawnNpcCount);
			Assert.Equal(1, report.SideEffectPlan.InvasionSpawnCount);
			Assert.Equal(1, report.SideEffectPlan.DefenderUpdatePlayerCount);
			Assert.Equal(1, report.SideEffectPlan.SkippedZonePlayerCount);
			Assert.Equal(1, report.SideEffectPlan.DefenderInvitationPlanCount);
			Assert.Equal(1, report.SideEffectPlan.DefenderQuestionWindowIntentCount);
			Assert.Equal(0, report.SideEffectPlan.DefenderRequestNotStoredCount);
			Assert.Equal(0, report.SideEffectPlan.AlreadyDefenderUpdateCount);
			Assert.Equal(0, report.SideEffectPlan.DefenderAllianceFullUpdateCount);
			Assert.Equal([1004], Assert.IsType<VortexDefenderAllianceUpdatePlan>(
				report.SideEffectPlan.DefenderAllianceUpdatePlan).DefenderObjectIds);
			Assert.Equal(
				[
					VortexStartInvasionSideEffectStepKind.SetActiveVortex,
					VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpcs,
					VortexStartInvasionSideEffectStepKind.DespawnExistingVortexNpc,
					VortexStartInvasionSideEffectStepKind.SpawnInvasionNpc,
					VortexStartInvasionSideEffectStepKind.InitRiftGenerator,
					VortexStartInvasionSideEffectStepKind.UpdateDefenderAlliance,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartCoordinator_DuplicateStartSkipsStaticSelectorAndPreservesRuntimeState()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-coordinator-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var initialPortal = CreateVortexPortal(location);
			var replacementPortal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			runtime.StartInvasion(location, initialPortal);
			var selector = new CountingInvasionSpawnSelector();
			var coordinator = new VortexStartInvasionCoordinatorService(
				runtime,
				new VortexStartInvasionSideEffectPlanService(),
				selector);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "ignored"),
				]);

			var report = coordinator.StartInvasion(
				location,
				replacementPortal,
				VortexStartInvasionSnapshotRequest.Empty,
				table);

			Assert.Equal(VortexStartInvasionCoordinatorStatus.AlreadyStarted, report.Status);
			Assert.False(report.Started);
			Assert.False(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.Empty(report.SideEffectPlan.OrderedSteps);
			Assert.Equal(0, selector.CallCount);
			Assert.Empty(selector.LocationIds);
			Assert.Same(initialPortal, report.StartResult.Snapshot.ActivePortal);
			Assert.Same(initialPortal, Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id)).ActivePortal);
			Assert.Equal("services/VortexService.startInvasion", report.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartScheduledStopPlan_PlansJavaDurationOnlyAfterCoordinatorStartSucceeds()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-schedule-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStartInvasionCoordinatorService(
				runtime,
				new VortexStartInvasionSideEffectPlanService());
			var scheduler = new VortexStartScheduledStopPlanService();
			var startReport = coordinator.StartInvasion(location);

			var plan = scheduler.CreatePlan(startReport, durationHours: 2);

			Assert.Equal(VortexStartScheduledStopPlanStatus.Planned, plan.Status);
			Assert.True(plan.HasScheduleIntent);
			Assert.False(plan.ShouldScheduleLiveStop);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(2, plan.DurationHours);
			Assert.Equal("HOURS", plan.TimeUnit);
			Assert.Equal("configs/main/CustomConfig.VORTEX_DURATION", plan.DurationSource);
			Assert.Equal("services/VortexService.stopInvasion", plan.ScheduledMethod);
			Assert.Equal(
				"services/VortexService.startInvasion -> ThreadPoolManager.schedule(stopInvasion, getDuration(), TimeUnit.HOURS)",
				plan.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartScheduledStopPlan_DuplicateStartOmitsScheduleIntent()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-start-schedule-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStartInvasionCoordinatorService(
				runtime,
				new VortexStartInvasionSideEffectPlanService());
			var scheduler = new VortexStartScheduledStopPlanService();
			coordinator.StartInvasion(location);

			var duplicateReport = coordinator.StartInvasion(location);
			var plan = scheduler.CreatePlan(duplicateReport, durationHours: 2);

			Assert.Equal(VortexStartScheduledStopPlanStatus.NotScheduledAlreadyStarted, plan.Status);
			Assert.False(plan.HasScheduleIntent);
			Assert.False(plan.ShouldScheduleLiveStop);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(0, plan.DurationHours);
			Assert.Equal("HOURS", plan.TimeUnit);
			Assert.Equal("services/VortexService.getDuration", plan.DurationSource);
			Assert.Equal("services/VortexService.stopInvasion", plan.ScheduledMethod);
			Assert.Equal("services/VortexService.startInvasion", plan.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RiftGeneratorLookupPlan_SelectsLastJavaGeneratorNpcAndPlansDeathObserver()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-generator-plan-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var planner = new VortexRiftGeneratorLookupPlanService();
			var firstGenerator = new VortexStartSpawnedNpcSnapshot(ObjectId: 7301, NpcId: VortexRiftGeneratorLookupPlanService.GeneratorNpcIdA);
			var secondGenerator = new VortexStartSpawnedNpcSnapshot(ObjectId: 7302, NpcId: VortexRiftGeneratorLookupPlanService.GeneratorNpcIdB);

			var plan = planner.CreatePlan(
				location,
				[
					new VortexStartSpawnedNpcSnapshot(ObjectId: 7201, NpcId: 831600),
					firstGenerator,
					new VortexStartSpawnedNpcSnapshot(ObjectId: 7202, NpcId: 831601),
					secondGenerator,
				]);

			Assert.Equal(VortexRiftGeneratorLookupPlanStatus.Planned, plan.Status);
			Assert.True(plan.HasGenerator);
			Assert.False(plan.ShouldAttachLiveDeathObserver);
			Assert.True(plan.WouldStopInvasionOnGeneratorDeath);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(location.HomePoint.WorldId, plan.HomeWorldId);
			Assert.Equal([7301, 7302], plan.CandidateGenerators.Select(candidate => candidate.ObjectId).ToArray());
			Assert.Same(secondGenerator, plan.SelectedGenerator);
			Assert.Equal(string.Empty, plan.JavaExceptionMessage);
			Assert.Equal(
				"services/vortex/DimensionalVortex.initRiftGenerator -> Npc.getObserveController().attach(DeathObserver)",
				plan.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RiftGeneratorLookupPlan_MissingGeneratorRecordsJavaExceptionMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-generator-missing-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var planner = new VortexRiftGeneratorLookupPlanService();

			var plan = planner.CreatePlan(
				location,
				[
					new VortexStartSpawnedNpcSnapshot(ObjectId: 7201, NpcId: 831600),
					new VortexStartSpawnedNpcSnapshot(ObjectId: 7202, NpcId: 831601),
				]);

			Assert.Equal(VortexRiftGeneratorLookupPlanStatus.MissingGenerator, plan.Status);
			Assert.False(plan.HasGenerator);
			Assert.False(plan.ShouldAttachLiveDeathObserver);
			Assert.False(plan.WouldStopInvasionOnGeneratorDeath);
			Assert.Empty(plan.CandidateGenerators);
			Assert.Null(plan.SelectedGenerator);
			Assert.Equal($"No generator was found in loc:{location.Id}", plan.JavaExceptionMessage);
			Assert.Equal("services/vortex/DimensionalVortex.initRiftGenerator", plan.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public void InvaderUpdateAddPlayerPlan_SkipsExistingInvaderBeforeAddPlayer()
	{
		var planner = new VortexInvaderUpdateAddPlayerPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders: [invader],
			invaderAlliance: VortexInvaderAllianceSnapshot.Open);

		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader, plan.Status);
		Assert.Same(invader, plan.Invader);
		Assert.Equal([1002], plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderAllianceSnapshot.Open, plan.InvaderAlliance);
		Assert.False(plan.WouldCallAddPlayer);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateInvaderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.False(plan.WouldPutParticipant);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.Equal("services/vortex/Invasion.updateInvaders", plan.JavaSource);
	}

	[Fact]
	public void InvaderUpdateAddPlayerPlan_RecordsFirstInvaderWithoutAllianceMutation()
	{
		var planner = new VortexInvaderUpdateAddPlayerPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(invader);

		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.RecordFirstInvader, plan.Status);
		Assert.Equal(VortexInvaderAllianceSnapshot.Missing, plan.InvaderAlliance);
		Assert.Empty(plan.ExistingInvaderObjectIds);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateInvaderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.Equal("services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)", plan.JavaSource);
	}

	[Fact]
	public void InvaderUpdateAddPlayerPlan_AddsToExistingNonDisbandedAlliance()
	{
		var planner = new VortexInvaderUpdateAddPlayerPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: true, IsInAlliance: false);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders: [new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false)],
			invaderAlliance: VortexInvaderAllianceSnapshot.Open);

		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.AddToExistingAlliance, plan.Status);
		Assert.Equal([1001], plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderAllianceSnapshot.Open, plan.InvaderAlliance);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.True(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateInvaderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveInvaders);
	}

	[Fact]
	public void InvaderUpdateAddPlayerPlan_CreatesOffenceAllianceForSecondInvader()
	{
		var planner = new VortexInvaderUpdateAddPlayerPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: true, IsInAlliance: true);
		var otherInvader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: true);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders: [otherInvader],
			invaderAlliance: VortexInvaderAllianceSnapshot.Missing);

		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.CreateInvaderAlliance, plan.Status);
		Assert.Equal([1001], plan.ExistingInvaderObjectIds);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.True(plan.WouldCreateInvaderAlliance);
		Assert.Equal(PlayerAllianceTeamType.AllianceOffence, plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.False(plan.WouldWarn);
		Assert.Equal([1002, 1001], plan.RemovalPlans.Select(removal => removal.PlayerObjectId).ToArray());
		Assert.Equal([true, false], plan.RemovalPlans.Select(removal => removal.WouldRemoveGroup).ToArray());
		Assert.Equal([false, true], plan.RemovalPlans.Select(removal => removal.WouldRemoveAlliance).ToArray());
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveInvaders);
	}

	[Fact]
	public void InvaderUpdateAddPlayerPlan_TooManyInvadersWithoutAllianceWarnsAndSkipsParticipantPut()
	{
		var planner = new VortexInvaderUpdateAddPlayerPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders:
			[
				new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false),
				new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1003, IsInGroup: false, IsInAlliance: false),
			],
			invaderAlliance: VortexInvaderAllianceSnapshot.Disbanded);

		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.MissingAllianceTooManyParticipants, plan.Status);
		Assert.Equal([1001, 1003], plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderAllianceSnapshot.Disbanded, plan.InvaderAlliance);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateInvaderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.False(plan.WouldPutParticipant);
		Assert.True(plan.WouldWarn);
		Assert.Equal("Couldn't add invader:1002 to invaders (alliance not initialized). Current participants: 2", plan.WarningMessage);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveInvaders);
	}

	[Fact]
	public void InvaderUpdateInvadersPlan_SkipsAlreadyInvaderBeforeAddPlayerLikeJava()
	{
		var planner = new VortexInvaderUpdateInvadersPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders: [invader],
			invaderAlliance: VortexInvaderAllianceSnapshot.Open);

		Assert.Equal(VortexInvaderUpdateInvadersPlanStatus.AlreadyInvader, plan.Status);
		Assert.Equal(1002, plan.PlayerObjectId);
		Assert.Equal([1002], plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderAllianceSnapshot.Open, plan.InvaderAlliance);
		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader, plan.AddPlayerPlan.Status);
		Assert.False(plan.WouldCallAddPlayer);
		Assert.False(plan.WouldPutParticipant);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.Equal("services/vortex/Invasion.updateInvaders", plan.JavaSource);
	}

	[Fact]
	public void InvaderUpdateInvadersPlan_ComposesRecordFirstInvaderAddPlayer()
	{
		var planner = new VortexInvaderUpdateInvadersPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(invader);

		Assert.Equal(VortexInvaderUpdateInvadersPlanStatus.AddPlayerPlanned, plan.Status);
		Assert.Empty(plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderAllianceSnapshot.Missing, plan.InvaderAlliance);
		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.RecordFirstInvader, plan.AddPlayerPlan.Status);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateInvaderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.Equal("services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)", plan.JavaSource);
	}

	[Fact]
	public void InvaderUpdateInvadersPlan_ComposesOffenceAllianceCreationForSecondInvader()
	{
		var planner = new VortexInvaderUpdateInvadersPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: true, IsInAlliance: true);
		var otherInvader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: true);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders: [otherInvader],
			invaderAlliance: VortexInvaderAllianceSnapshot.Missing);

		Assert.Equal(VortexInvaderUpdateInvadersPlanStatus.AddPlayerPlanned, plan.Status);
		Assert.Equal([1001], plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.CreateInvaderAlliance, plan.AddPlayerPlan.Status);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.True(plan.WouldCreateInvaderAlliance);
		Assert.Equal(PlayerAllianceTeamType.AllianceOffence, plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.Equal([1002, 1001], plan.AddPlayerPlan.RemovalPlans.Select(removal => removal.PlayerObjectId).ToArray());
		Assert.Equal([true, false], plan.AddPlayerPlan.RemovalPlans.Select(removal => removal.WouldRemoveGroup).ToArray());
		Assert.Equal([false, true], plan.AddPlayerPlan.RemovalPlans.Select(removal => removal.WouldRemoveAlliance).ToArray());
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
	}

	[Fact]
	public void InvaderUpdateInvadersPlan_ComposesAddPlayerWarningWhenAllianceMissingWithManyInvaders()
	{
		var planner = new VortexInvaderUpdateInvadersPlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			invader,
			existingInvaders:
			[
				new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false),
				new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1003, IsInGroup: false, IsInAlliance: false),
			],
			invaderAlliance: VortexInvaderAllianceSnapshot.Disbanded);

		Assert.Equal(VortexInvaderUpdateInvadersPlanStatus.AddPlayerWarning, plan.Status);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.MissingAllianceTooManyParticipants, plan.AddPlayerPlan.Status);
		Assert.False(plan.WouldPutParticipant);
		Assert.True(plan.WouldWarn);
		Assert.Equal([1001, 1003], plan.ExistingInvaderObjectIds);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
	}

	[Fact]
	public void InvaderPassedPortalUpdatePlan_BlocksWhenInactiveOrNoPassedPlayerLikeJavaZoneEntry()
	{
		var planner = new VortexInvaderPassedPortalUpdatePlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var inactive = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: false,
			passedPlayerObjectIds: new HashSet<int> { 1002 });
		var missingPass = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			passedPlayerObjectIds: new HashSet<int>());

		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.InactiveVortex, inactive.Status);
		Assert.True(inactive.IsNewZonePlayer);
		Assert.False(inactive.HasActiveInvasion);
		Assert.True(inactive.HadPassedPortal);
		Assert.False(inactive.HasInvaderUpdatePlan);
		Assert.False(inactive.WouldCallAddPlayer);
		Assert.False(inactive.ShouldMutateLiveInvaders);
		Assert.Equal("model/vortex/VortexLocation.onEnterZone", inactive.JavaSource);
		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.MissingPassedPlayer, missingPass.Status);
		Assert.True(missingPass.HasActiveInvasion);
		Assert.False(missingPass.HadPassedPortal);
		Assert.Empty(missingPass.PassedPlayerObjectIds);
		Assert.False(missingPass.HasInvaderUpdatePlan);
		Assert.False(missingPass.WouldCallAddPlayer);
	}

	[Fact]
	public void InvaderPassedPortalUpdatePlan_BlocksNonNewZonePlayerOrNonInvaderRace()
	{
		var planner = new VortexInvaderPassedPortalUpdatePlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var existingZonePlayer = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			isNewZonePlayer: false);
		var defenderRace = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			isInvaderRace: false);

		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.NotNewZonePlayer, existingZonePlayer.Status);
		Assert.False(existingZonePlayer.IsNewZonePlayer);
		Assert.True(existingZonePlayer.HadPassedPortal);
		Assert.False(existingZonePlayer.HasInvaderUpdatePlan);
		Assert.False(existingZonePlayer.WouldCallAddPlayer);
		Assert.False(existingZonePlayer.ShouldRecordZonePlayer);
		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.NonInvaderRace, defenderRace.Status);
		Assert.False(defenderRace.IsInvaderRace);
		Assert.True(defenderRace.HadPassedPortal);
		Assert.False(defenderRace.HasInvaderUpdatePlan);
		Assert.False(defenderRace.WouldCallAddPlayer);
	}

	[Fact]
	public void InvaderPassedPortalUpdatePlan_SkipsExistingInvaderBeforeAddPlayer()
	{
		var planner = new VortexInvaderPassedPortalUpdatePlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			existingInvaders: [invader],
			invaderAlliance: VortexInvaderAllianceSnapshot.Open);

		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.AlreadyInvader, plan.Status);
		Assert.Equal(0, plan.LocationId);
		Assert.Equal(1002, plan.PlayerObjectId);
		Assert.Equal([1002], plan.PassedPlayerObjectIds);
		Assert.Equal([1002], plan.ExistingInvaderObjectIds);
		Assert.Equal(VortexInvaderAllianceSnapshot.Open, plan.InvaderAlliance);
		Assert.True(plan.HadPassedPortal);
		Assert.True(plan.HasInvaderUpdatePlan);
		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader, plan.InvaderUpdatePlan?.Status);
		Assert.False(plan.WouldCallAddPlayer);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.Equal("model/vortex/VortexLocation.onEnterZone -> services/vortex/Invasion.addPlayer(player, true)", plan.JavaSource);
	}

	[Fact]
	public void InvaderPassedPortalUpdatePlan_SelectsInvaderAddPlanForPassedNewInvader()
	{
		var planner = new VortexInvaderPassedPortalUpdatePlanService();
		var invader = new VortexInvaderUpdatePlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			passedPlayerObjectIds: new HashSet<int> { 1002, 1003 },
			existingInvaders: [],
			invaderAlliance: VortexInvaderAllianceSnapshot.Missing);

		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.UpdatePlanned, plan.Status);
		Assert.Equal([1002, 1003], plan.PassedPlayerObjectIds);
		Assert.Empty(plan.ExistingInvaderObjectIds);
		Assert.True(plan.IsNewZonePlayer);
		Assert.True(plan.HasActiveInvasion);
		Assert.True(plan.IsInvaderRace);
		Assert.True(plan.HadPassedPortal);
		Assert.True(plan.HasInvaderUpdatePlan);
		Assert.Equal(VortexInvaderUpdateAddPlayerPlanStatus.RecordFirstInvader, plan.InvaderUpdatePlan?.Status);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.True(plan.InvaderUpdatePlan?.WouldPutParticipant);
		Assert.False(plan.ShouldRecordZonePlayer);
		Assert.False(plan.ShouldMutateLiveInvaders);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
	}

	[Fact]
	public void DefenderAddPlayerTransitionPlan_RecordsFirstDefenderWithoutAllianceMutation()
	{
		var planner = new VortexDefenderAddPlayerTransitionPlanService();
		var player = new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1004, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(player);

		Assert.Equal(VortexDefenderAddPlayerTransitionPlanStatus.RecordFirstDefender, plan.Status);
		Assert.Same(player, plan.Player);
		Assert.Equal(VortexDefenderAllianceSnapshot.Missing, plan.DefenderAlliance);
		Assert.Empty(plan.ExistingDefenderObjectIds);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateDefenderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveDefenders);
		Assert.Equal("services/vortex/Invasion.addPlayer(player, false)", plan.JavaSource);
	}

	[Fact]
	public void DefenderAddPlayerTransitionPlan_AddsToExistingNonDisbandedAlliance()
	{
		var planner = new VortexDefenderAddPlayerTransitionPlanService();
		var player = new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1004, IsInGroup: true, IsInAlliance: false);

		var plan = planner.CreatePlan(
			player,
			existingDefenders: [new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false)],
			defenderAlliance: VortexDefenderAllianceSnapshot.Open);

		Assert.Equal(VortexDefenderAddPlayerTransitionPlanStatus.AddToExistingAlliance, plan.Status);
		Assert.Equal([1001], plan.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, plan.DefenderAlliance);
		Assert.True(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateDefenderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.WouldWarn);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderAddPlayerTransitionPlan_CreatesDefenceAllianceForSecondDefender()
	{
		var planner = new VortexDefenderAddPlayerTransitionPlanService();
		var player = new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1004, IsInGroup: true, IsInAlliance: true);
		var otherPlayer = new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: true);

		var plan = planner.CreatePlan(
			player,
			existingDefenders: [otherPlayer],
			defenderAlliance: VortexDefenderAllianceSnapshot.Missing);

		Assert.Equal(VortexDefenderAddPlayerTransitionPlanStatus.CreateDefenderAlliance, plan.Status);
		Assert.Equal([1001], plan.ExistingDefenderObjectIds);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.True(plan.WouldCreateDefenderAlliance);
		Assert.Equal(PlayerAllianceTeamType.AllianceDefence, plan.CreatedAllianceTeamType);
		Assert.True(plan.WouldPutParticipant);
		Assert.False(plan.WouldWarn);
		Assert.Equal([1004, 1001], plan.RemovalPlans.Select(removal => removal.PlayerObjectId).ToArray());
		Assert.Equal([true, false], plan.RemovalPlans.Select(removal => removal.WouldRemoveGroup).ToArray());
		Assert.Equal([false, true], plan.RemovalPlans.Select(removal => removal.WouldRemoveAlliance).ToArray());
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderAddPlayerTransitionPlan_TooManyDefendersWithoutAllianceWarnsAndSkipsParticipantPut()
	{
		var planner = new VortexDefenderAddPlayerTransitionPlanService();
		var player = new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1004, IsInGroup: false, IsInAlliance: false);

		var plan = planner.CreatePlan(
			player,
			existingDefenders:
			[
				new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false),
				new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false),
			],
			defenderAlliance: VortexDefenderAllianceSnapshot.Disbanded);

		Assert.Equal(VortexDefenderAddPlayerTransitionPlanStatus.MissingAllianceTooManyParticipants, plan.Status);
		Assert.Equal([1001, 1002], plan.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderAllianceSnapshot.Disbanded, plan.DefenderAlliance);
		Assert.False(plan.WouldAddToExistingAlliance);
		Assert.False(plan.WouldCreateDefenderAlliance);
		Assert.Null(plan.CreatedAllianceTeamType);
		Assert.False(plan.WouldPutParticipant);
		Assert.True(plan.WouldWarn);
		Assert.Equal("Couldn't add defender:1004 to defenders (alliance not initialized). Current participants: 2", plan.WarningMessage);
		Assert.Empty(plan.RemovalPlans);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderInvitationAcceptancePlan_GroupResponderRemovesGroupBeforeAddingDefender()
	{
		var planner = new VortexDefenderInvitationAcceptancePlanService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: true,
			IsInAlliance: true);

		var plan = planner.CreatePlan(responder, VortexDefenderAllianceSnapshot.Open);

		Assert.Equal(VortexDefenderInvitationAcceptancePlanStatus.AcceptancePlanned, plan.Status);
		Assert.Same(responder, plan.Responder);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, plan.DefenderAlliance);
		Assert.True(plan.WouldRemoveGroup);
		Assert.False(plan.WouldRemoveAlliance);
		Assert.True(plan.WouldAddDefender);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveDefenders);
		Assert.Equal(
			"services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest",
			plan.JavaSource);
	}

	[Fact]
	public void DefenderInvitationAcceptancePlan_AllianceResponderRemovesAllianceWhenNotGrouped()
	{
		var planner = new VortexDefenderInvitationAcceptancePlanService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: false,
			IsInAlliance: true);

		var plan = planner.CreatePlan(responder, VortexDefenderAllianceSnapshot.Missing);

		Assert.Equal(VortexDefenderInvitationAcceptancePlanStatus.AcceptancePlanned, plan.Status);
		Assert.Equal(VortexDefenderAllianceSnapshot.Missing, plan.DefenderAlliance);
		Assert.False(plan.WouldRemoveGroup);
		Assert.True(plan.WouldRemoveAlliance);
		Assert.True(plan.WouldAddDefender);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderInvitationAcceptancePlan_FullAllianceBlocksAddAfterRemovalCheck()
	{
		var planner = new VortexDefenderInvitationAcceptancePlanService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: true,
			IsInAlliance: false);

		var plan = planner.CreatePlan(responder, VortexDefenderAllianceSnapshot.Full);

		Assert.Equal(VortexDefenderInvitationAcceptancePlanStatus.DefenderAllianceFull, plan.Status);
		Assert.Equal(VortexDefenderAllianceSnapshot.Full, plan.DefenderAlliance);
		Assert.True(plan.WouldRemoveGroup);
		Assert.False(plan.WouldRemoveAlliance);
		Assert.False(plan.WouldAddDefender);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderUpdateDefendersPlan_ComposesInvitationRequestAndQuestionWindowIntent()
	{
		var planner = new VortexDefenderUpdateDefendersPlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var plan = planner.CreateInvitationPlan(
			defender,
			existingDefenders: [new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false)],
			defenderAlliance: VortexDefenderAllianceSnapshot.Open);

		Assert.Equal(VortexDefenderUpdateDefendersPlanStatus.InvitationPlanned, plan.Status);
		Assert.Equal(VortexDefenderUpdateDefendersPlanStage.Invitation, plan.Stage);
		Assert.Equal(1004, plan.PlayerObjectId);
		Assert.Equal([1001], plan.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, plan.DefenderAlliance);
		Assert.True(plan.HasInvitationPlan);
		Assert.Equal(VortexDefenderInvitationPlanStatus.InvitationPlanned, plan.InvitationPlan?.Status);
		Assert.True(plan.WouldInstallRequest);
		Assert.True(plan.HasQuestionWindowIntent);
		Assert.False(plan.HasAcceptancePlan);
		Assert.False(plan.HasAddPlayerPlan);
		Assert.False(plan.ShouldMutateLiveRequest);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.Equal("services/vortex/Invasion.updateDefenders", plan.JavaSource);
	}

	[Fact]
	public void DefenderInvitationRequestSlotSnapshot_UsesPlayerResponseRequesterForJavaQuestionId()
	{
		var planner = new VortexDefenderInvitationRequestSlotSnapshotService();
		var defender = CreatePlayer(1004, isOnline: true, worldId: 210060000);

		var open = planner.CreateSnapshot(defender);
		Assert.True(defender.ResponseRequester.PutRequest(
			SmQuestionWindow.VortexDefenderInvitation,
			new QuestionResponseRequest(9001, QuestionResponseRequestKind.Unknown)));
		var occupied = planner.CreateSnapshot(defender);

		Assert.Equal(1004, open.PlayerObjectId);
		Assert.Equal(SmQuestionWindow.VortexDefenderInvitation, open.QuestionId);
		Assert.Equal(VortexDefenderInvitationPlanService.DefenderQuestionId, open.QuestionId);
		Assert.True(open.RequestSlotAvailable);
		Assert.Equal(0, open.ActiveRequestCount);
		Assert.Equal("model/gameobjects/player/ResponseRequester.putRequest", open.JavaSource);
		Assert.Equal(1004, occupied.PlayerObjectId);
		Assert.False(occupied.RequestSlotAvailable);
		Assert.Equal(1, occupied.ActiveRequestCount);
	}

	[Fact]
	public void DefenderInvitationRequestPayloadPlan_CreatesQuestionResponseRequestOnlyForQuestionWindowIntent()
	{
		var invitationPlanner = new VortexDefenderInvitationPlanService();
		var payloadPlanner = new VortexDefenderInvitationRequestPayloadPlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");
		var plannedInvitation = invitationPlanner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int> { 1001 },
			alliance: VortexDefenderAllianceSnapshot.Open);
		var blockedInvitation = invitationPlanner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int> { 1004 },
			alliance: VortexDefenderAllianceSnapshot.Open);

		var created = payloadPlanner.CreatePlan(plannedInvitation);
		var notCreated = payloadPlanner.CreatePlan(blockedInvitation);

		Assert.Equal(VortexDefenderInvitationRequestPayloadPlanStatus.Created, created.Status);
		Assert.True(created.WouldCreateRequest);
		Assert.False(created.ShouldRegisterLiveRequest);
		Assert.False(created.ShouldSendLivePacket);
		Assert.Equal(1004, created.RequesterObjectId);
		Assert.Equal(1004, created.DefenderObjectId);
		Assert.Equal(SmQuestionWindow.VortexDefenderInvitation, created.QuestionId);
		var request = Assert.IsType<QuestionResponseRequest>(created.Request);
		Assert.Equal(1004, request.RequesterObjectId);
		Assert.Equal(QuestionResponseRequestKind.VortexDefenderInvitation, request.Kind);
		var payload = Assert.IsType<PendingVortexDefenderInvitationRequest>(request.Payload);
		Assert.Equal(1004, payload.RequesterObjectId);
		Assert.Equal(SmQuestionWindow.VortexDefenderInvitation, payload.QuestionId);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, payload.DefenderAlliance);
		Assert.Equal([1001], payload.ExistingDefenderObjectIds);
		Assert.Equal(
			"services/vortex/Invasion.updateDefenders -> model/gameobjects/player/RequestResponseHandler",
			created.JavaSource);

		Assert.Equal(VortexDefenderInvitationRequestPayloadPlanStatus.NotCreated, notCreated.Status);
		Assert.False(notCreated.WouldCreateRequest);
		Assert.Null(notCreated.Request);
		Assert.False(notCreated.ShouldRegisterLiveRequest);
	}

	[Fact]
	public void DefenderInvitationRegistrationReport_GatesQuestionWindowOnJavaPutRequestResult()
	{
		var invitationPlanner = new VortexDefenderInvitationPlanService();
		var reportPlanner = new VortexDefenderInvitationRegistrationReportService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");
		var registeredInvitation = invitationPlanner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int> { 1001 },
			alliance: VortexDefenderAllianceSnapshot.Open);
		var rejectedInvitation = invitationPlanner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int>(),
			alliance: VortexDefenderAllianceSnapshot.Open,
			requestSlotAvailable: false);
		var skippedInvitation = invitationPlanner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int> { 1004 },
			alliance: VortexDefenderAllianceSnapshot.Open);

		var registered = reportPlanner.CreateReport(registeredInvitation);
		var rejected = reportPlanner.CreateReport(rejectedInvitation);
		var skipped = reportPlanner.CreateReport(skippedInvitation);

		Assert.Equal(VortexDefenderInvitationRegistrationReportStatus.Registered, registered.Status);
		Assert.True(registered.Registered);
		Assert.True(registered.WouldAttemptRequestRegistration);
		Assert.True(registered.SimulatedPutRequestResult);
		Assert.True(registered.WouldSendQuestionWindow);
		Assert.True(registered.HasPayload);
		Assert.Equal(1004, registered.DefenderObjectId);
		Assert.Equal(SmQuestionWindow.VortexDefenderInvitation, registered.QuestionId);
		Assert.Equal(0, registered.QuestionWindowSenderId);
		Assert.Equal(0, registered.QuestionWindowRangeOrCooldownSeconds);
		Assert.False(registered.ShouldRegisterLiveRequest);
		Assert.False(registered.ShouldSendLivePacket);
		Assert.Equal(
			"services/vortex/Invasion.updateDefenders -> model/gameobjects/player/ResponseRequester.putRequest",
			registered.JavaSource);

		Assert.Equal(VortexDefenderInvitationRegistrationReportStatus.RequestRejected, rejected.Status);
		Assert.True(rejected.Rejected);
		Assert.True(rejected.WouldAttemptRequestRegistration);
		Assert.False(rejected.SimulatedPutRequestResult);
		Assert.False(rejected.WouldSendQuestionWindow);
		Assert.False(rejected.HasPayload);
		Assert.False(rejected.RequestSlotAvailable);
		Assert.Equal(SmQuestionWindow.VortexDefenderInvitation, rejected.QuestionId);
		Assert.Equal(0, rejected.QuestionWindowSenderId);
		Assert.Equal(0, rejected.QuestionWindowRangeOrCooldownSeconds);
		Assert.False(rejected.ShouldRegisterLiveRequest);
		Assert.False(rejected.ShouldSendLivePacket);

		Assert.Equal(VortexDefenderInvitationRegistrationReportStatus.Skipped, skipped.Status);
		Assert.True(skipped.Skipped);
		Assert.False(skipped.WouldAttemptRequestRegistration);
		Assert.False(skipped.SimulatedPutRequestResult);
		Assert.False(skipped.WouldSendQuestionWindow);
		Assert.False(skipped.HasPayload);
		Assert.Equal(SmQuestionWindow.VortexDefenderInvitation, skipped.QuestionId);
		Assert.Null(skipped.QuestionWindowSenderId);
		Assert.Null(skipped.QuestionWindowRangeOrCooldownSeconds);
		Assert.False(skipped.ShouldRegisterLiveRequest);
		Assert.False(skipped.ShouldSendLivePacket);
	}

	[Fact]
	public void DefenderInvitationResponseDispatchPlan_MapsZeroToDenyAndNonZeroToAcceptLikeJavaHandle()
	{
		var planner = new VortexDefenderInvitationResponseDispatchPlanService();
		var request = new PendingVortexDefenderInvitationRequest(
			RequesterObjectId: 1004,
			QuestionId: SmQuestionWindow.VortexDefenderInvitation,
			DefenderAlliance: VortexDefenderAllianceSnapshot.Open,
			ExistingDefenderObjectIds: [1001]);
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: true,
			IsInAlliance: true);

		var deny = planner.CreatePlan(request, responder, responseCode: 0);
		var accept = planner.CreatePlan(request, responder, responseCode: 7);

		Assert.Equal(VortexDefenderInvitationResponseDispatchPlanStatus.Denied, deny.Status);
		Assert.True(deny.Denied);
		Assert.False(deny.Accepted);
		Assert.False(deny.HasAcceptancePlan);
		Assert.Null(deny.AcceptancePlan);
		Assert.Equal(0, deny.ResponseCode);
		Assert.Equal(1004, deny.RequesterObjectId);
		Assert.Equal(1004, deny.ResponderObjectId);
		Assert.False(deny.ShouldRemoveLiveRequest);
		Assert.Equal(
			"model/gameobjects/player/RequestResponseHandler.handle -> model/gameobjects/player/RequestResponseHandler.denyRequest",
			deny.JavaSource);

		Assert.Equal(VortexDefenderInvitationResponseDispatchPlanStatus.Accepted, accept.Status);
		Assert.True(accept.Accepted);
		Assert.False(accept.Denied);
		Assert.True(accept.HasAcceptancePlan);
		Assert.Equal(7, accept.ResponseCode);
		var acceptance = Assert.IsType<VortexDefenderInvitationAcceptancePlan>(accept.AcceptancePlan);
		Assert.Equal(VortexDefenderInvitationAcceptancePlanStatus.AcceptancePlanned, acceptance.Status);
		Assert.True(accept.AcceptancePlan?.WouldRemoveGroup);
		Assert.True(accept.AcceptancePlan?.WouldAddDefender);
		Assert.False(accept.ShouldRemoveLiveRequest);
		Assert.False(accept.ShouldMutateLiveGroup);
		Assert.False(accept.ShouldMutateLiveAlliance);
		Assert.False(accept.ShouldMutateLiveDefenders);
		Assert.Equal(
			"model/gameobjects/player/RequestResponseHandler.handle -> services/vortex/Invasion.updateDefenders.acceptRequest",
			accept.JavaSource);
	}

	[Fact]
	public void DefenderInvitationResponseConsumptionReport_ConsumesRegistryDispatchLikeJavaRespond()
	{
		var registry = new QuestionResponseRegistry();
		var planner = new VortexDefenderInvitationResponseConsumptionReportService();
		var pendingRequest = new PendingVortexDefenderInvitationRequest(
			RequesterObjectId: 1004,
			QuestionId: SmQuestionWindow.VortexDefenderInvitation,
			DefenderAlliance: VortexDefenderAllianceSnapshot.Open,
			ExistingDefenderObjectIds: [1001]);
		var request = new QuestionResponseRequest(
			RequesterObjectId: 1004,
			QuestionResponseRequestKind.VortexDefenderInvitation,
			pendingRequest);
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: true,
			IsInAlliance: true);
		Assert.True(registry.PutRequest(SmQuestionWindow.VortexDefenderInvitation, request));

		var denyDispatch = registry.Respond(SmQuestionWindow.VortexDefenderInvitation, responseCode: 0);
		var deny = planner.CreateReport(
			SmQuestionWindow.VortexDefenderInvitation,
			responseCode: 0,
			denyDispatch,
			responder);

		Assert.Equal(0, registry.Count);
		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.Denied, deny.Status);
		Assert.True(deny.Denied);
		Assert.False(deny.Accepted);
		Assert.True(deny.RequestRemovedByRegistry);
		Assert.True(deny.WouldInvokeHandler);
		Assert.True(deny.HasVortexPayload);
		Assert.True(deny.HasDispatchPlan);
		Assert.False(deny.ShouldRemoveLiveRequest);
		Assert.False(deny.ShouldMutateLiveGroup);
		Assert.False(deny.ShouldMutateLiveAlliance);
		Assert.False(deny.ShouldMutateLiveDefenders);
		Assert.Equal(
			"model/gameobjects/player/ResponseRequester.respond -> model/gameobjects/player/RequestResponseHandler.handle",
			deny.JavaSource);
		Assert.Equal(VortexDefenderInvitationResponseDispatchPlanStatus.Denied, deny.DispatchPlan?.Status);

		Assert.True(registry.PutRequest(SmQuestionWindow.VortexDefenderInvitation, request));
		var acceptDispatch = registry.Respond(SmQuestionWindow.VortexDefenderInvitation, responseCode: 7);
		var accept = planner.CreateReport(
			SmQuestionWindow.VortexDefenderInvitation,
			responseCode: 7,
			acceptDispatch,
			responder);

		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.Accepted, accept.Status);
		Assert.True(accept.Accepted);
		Assert.True(accept.RequestRemovedByRegistry);
		Assert.True(accept.WouldInvokeHandler);
		Assert.True(accept.HasDispatchPlan);
		Assert.Equal(7, accept.ResponseCode);
		Assert.Equal(VortexDefenderInvitationResponseDispatchPlanStatus.Accepted, accept.DispatchPlan?.Status);
		Assert.True(accept.DispatchPlan?.HasAcceptancePlan);
		Assert.False(accept.ShouldRemoveLiveRequest);
	}

	[Fact]
	public void DefenderInvitationResponseConsumptionReport_ReportsMissingAndNonVortexDispatches()
	{
		var planner = new VortexDefenderInvitationResponseConsumptionReportService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: false,
			IsInAlliance: false);
		var nonVortexDispatch = new QuestionResponseDispatch(
			SmQuestionWindow.UnionInviteMe,
			ResponseCode: 1,
			Accepted: true,
			new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite));
		var missingPayloadDispatch = new QuestionResponseDispatch(
			SmQuestionWindow.VortexDefenderInvitation,
			ResponseCode: 1,
			Accepted: true,
			new QuestionResponseRequest(1004, QuestionResponseRequestKind.VortexDefenderInvitation));

		var missing = planner.CreateReport(
			SmQuestionWindow.VortexDefenderInvitation,
			responseCode: 1,
			dispatch: null,
			responder);
		var nonVortex = planner.CreateReport(
			SmQuestionWindow.UnionInviteMe,
			responseCode: 1,
			nonVortexDispatch,
			responder);
		var missingPayload = planner.CreateReport(
			SmQuestionWindow.VortexDefenderInvitation,
			responseCode: 1,
			missingPayloadDispatch,
			responder);

		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.RequestMissing, missing.Status);
		Assert.False(missing.RequestRemovedByRegistry);
		Assert.False(missing.WouldInvokeHandler);
		Assert.False(missing.HasVortexPayload);
		Assert.False(missing.HasDispatchPlan);
		Assert.False(missing.ShouldRemoveLiveRequest);

		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.NonVortexRequest, nonVortex.Status);
		Assert.True(nonVortex.RequestRemovedByRegistry);
		Assert.False(nonVortex.WouldInvokeHandler);
		Assert.False(nonVortex.HasVortexPayload);
		Assert.False(nonVortex.HasDispatchPlan);
		Assert.False(nonVortex.ShouldRemoveLiveRequest);

		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.PayloadMissing, missingPayload.Status);
		Assert.True(missingPayload.RequestRemovedByRegistry);
		Assert.False(missingPayload.WouldInvokeHandler);
		Assert.False(missingPayload.HasVortexPayload);
		Assert.False(missingPayload.HasDispatchPlan);
		Assert.False(missingPayload.ShouldRemoveLiveRequest);
	}

	[Fact]
	public void DefenderInvitationBatchPlan_ComposesOneInvitationPlanPerDefenderUpdateCandidate()
	{
		var updatePlan = new VortexDefenderAllianceUpdatePlan(
			VortexDefenderAllianceUpdatePlanStatus.Planned,
			LocationId: 0,
			DefendersRace: "ELYOS",
			DefenderUpdatePlayers:
			[
				new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS"),
				new VortexZonePlayerSnapshot(PlayerObjectId: 1006, Race: "ELYOS"),
				new VortexZonePlayerSnapshot(PlayerObjectId: 1007, Race: "ELYOS"),
			],
			SkippedPlayers:
			[
				new VortexZonePlayerSnapshot(PlayerObjectId: 1002, Race: "ASMODIANS"),
			],
			JavaSource: "services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders");
		var planner = new VortexDefenderInvitationBatchPlanService();

		var batch = planner.CreatePlan(
			updatePlan,
			existingDefenders: [new VortexDefenderAddPlayerSnapshot(1007, IsInGroup: false, IsInAlliance: false)],
			defenderAlliance: VortexDefenderAllianceSnapshot.Open,
			requestSlotsByPlayerObjectId: new Dictionary<int, bool> { [1006] = false });

		Assert.Equal(VortexDefenderInvitationBatchPlanStatus.Planned, batch.Status);
		Assert.Same(updatePlan, batch.UpdatePlan);
		Assert.Equal(0, batch.LocationId);
		Assert.Equal([1004, 1006, 1007], batch.DefenderObjectIds);
		Assert.Equal([1007], batch.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, batch.DefenderAlliance);
		Assert.Equal(3, batch.InvitationPlanCount);
		Assert.Equal(1, batch.QuestionWindowIntentCount);
		Assert.Equal(1, batch.RequestNotStoredCount);
		Assert.Equal(1, batch.AlreadyDefenderCount);
		Assert.Equal(0, batch.AllianceFullCount);
		Assert.True(batch.WouldCallUpdateDefenders);
		Assert.True(batch.WouldInstallAnyRequest);
		Assert.True(batch.HasAnyQuestionWindowIntent);
		Assert.Equal(
			[
				VortexDefenderUpdateDefendersPlanStatus.InvitationPlanned,
				VortexDefenderUpdateDefendersPlanStatus.InvitationRequestNotStored,
				VortexDefenderUpdateDefendersPlanStatus.InvitationAlreadyDefender,
			],
			batch.DefenderInvitationPlans.Select(plan => plan.Status).ToArray());
		Assert.False(batch.ShouldMutateLiveRequest);
		Assert.False(batch.ShouldSendLivePacket);
		Assert.False(batch.ShouldMutateLiveAlliance);
		Assert.False(batch.ShouldMutateLiveGroup);
		Assert.False(batch.ShouldMutateLiveDefenders);
		Assert.Equal(
			"services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders",
			batch.JavaSource);
	}

	[Fact]
	public void DefenderInvitationBatchPlan_FullAllianceSkipsAllInvitationRequestsLikeJavaFirstGate()
	{
		var updatePlan = new VortexDefenderAllianceUpdatePlan(
			VortexDefenderAllianceUpdatePlanStatus.Planned,
			LocationId: 0,
			DefendersRace: "ELYOS",
			DefenderUpdatePlayers:
			[
				new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS"),
				new VortexZonePlayerSnapshot(PlayerObjectId: 1006, Race: "ELYOS"),
			],
			SkippedPlayers: [],
			JavaSource: "services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders");
		var planner = new VortexDefenderInvitationBatchPlanService();

		var batch = planner.CreatePlan(updatePlan, defenderAlliance: VortexDefenderAllianceSnapshot.Full);

		Assert.Equal(2, batch.InvitationPlanCount);
		Assert.Equal(0, batch.QuestionWindowIntentCount);
		Assert.Equal(0, batch.RequestNotStoredCount);
		Assert.Equal(0, batch.AlreadyDefenderCount);
		Assert.Equal(2, batch.AllianceFullCount);
		Assert.False(batch.WouldInstallAnyRequest);
		Assert.False(batch.HasAnyQuestionWindowIntent);
		Assert.All(batch.DefenderInvitationPlans, plan =>
			Assert.Equal(VortexDefenderUpdateDefendersPlanStatus.InvitationAllianceFull, plan.Status));
		Assert.False(batch.ShouldMutateLiveRequest);
		Assert.False(batch.ShouldSendLivePacket);
	}

	[Fact]
	public void DefenderUpdateDefendersPlan_ComposesAcceptanceRemovalAndAddPlayerTransition()
	{
		var planner = new VortexDefenderUpdateDefendersPlanService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: true,
			IsInAlliance: true);

		var plan = planner.CreateAcceptancePlan(
			responder,
			existingDefenders: [new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: true)],
			defenderAlliance: VortexDefenderAllianceSnapshot.Missing);

		Assert.Equal(VortexDefenderUpdateDefendersPlanStatus.AcceptancePlanned, plan.Status);
		Assert.Equal(VortexDefenderUpdateDefendersPlanStage.Acceptance, plan.Stage);
		Assert.True(plan.HasAcceptancePlan);
		Assert.Equal(VortexDefenderInvitationAcceptancePlanStatus.AcceptancePlanned, plan.AcceptancePlan?.Status);
		Assert.True(plan.WouldRemoveGroup);
		Assert.False(plan.WouldRemoveAlliance);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.True(plan.HasAddPlayerPlan);
		Assert.Equal(VortexDefenderAddPlayerTransitionPlanStatus.CreateDefenderAlliance, plan.AddPlayerPlan?.Status);
		Assert.True(plan.WouldPutParticipant);
		Assert.True(plan.AddPlayerPlan?.WouldCreateDefenderAlliance);
		Assert.Equal(PlayerAllianceTeamType.AllianceDefence, plan.AddPlayerPlan?.CreatedAllianceTeamType);
		Assert.False(plan.HasInvitationPlan);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveDefenders);
		Assert.Equal(
			"services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest -> services/vortex/Invasion.addPlayer(player, false)",
			plan.JavaSource);
	}

	[Fact]
	public void DefenderUpdateDefendersPlan_FullAllianceBlocksAcceptanceAddPlayerLikeJavaSecondGate()
	{
		var planner = new VortexDefenderUpdateDefendersPlanService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: false,
			IsInAlliance: true);

		var plan = planner.CreateAcceptancePlan(
			responder,
			existingDefenders: [new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false)],
			defenderAlliance: VortexDefenderAllianceSnapshot.Full);

		Assert.Equal(VortexDefenderUpdateDefendersPlanStatus.AcceptanceAllianceFull, plan.Status);
		Assert.True(plan.HasAcceptancePlan);
		Assert.Equal(VortexDefenderInvitationAcceptancePlanStatus.DefenderAllianceFull, plan.AcceptancePlan?.Status);
		Assert.False(plan.WouldRemoveGroup);
		Assert.True(plan.WouldRemoveAlliance);
		Assert.False(plan.WouldCallAddPlayer);
		Assert.False(plan.HasAddPlayerPlan);
		Assert.False(plan.WouldPutParticipant);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderUpdateDefendersPlan_ComposesAddPlayerWarningWhenAllianceMissingWithManyDefenders()
	{
		var planner = new VortexDefenderUpdateDefendersPlanService();
		var responder = new VortexDefenderInvitationResponderSnapshot(
			PlayerObjectId: 1004,
			IsInGroup: false,
			IsInAlliance: false);

		var plan = planner.CreateAcceptancePlan(
			responder,
			existingDefenders:
			[
				new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1001, IsInGroup: false, IsInAlliance: false),
				new VortexDefenderAddPlayerSnapshot(PlayerObjectId: 1002, IsInGroup: false, IsInAlliance: false),
			],
			defenderAlliance: VortexDefenderAllianceSnapshot.Disbanded);

		Assert.Equal(VortexDefenderUpdateDefendersPlanStatus.AcceptancePlanned, plan.Status);
		Assert.True(plan.WouldCallAddPlayer);
		Assert.True(plan.HasAddPlayerPlan);
		Assert.Equal(VortexDefenderAddPlayerTransitionPlanStatus.MissingAllianceTooManyParticipants, plan.AddPlayerPlan?.Status);
		Assert.False(plan.WouldPutParticipant);
		Assert.True(plan.WouldWarn);
		Assert.Equal([1001, 1002], plan.ExistingDefenderObjectIds);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveDefenders);
	}

	[Fact]
	public void DefenderInvitationPlan_NewDefenderWithOpenAllianceRecordsQuestionWindowIntent()
	{
		var planner = new VortexDefenderInvitationPlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var plan = planner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int> { 1001 },
			alliance: VortexDefenderAllianceSnapshot.Open);

		Assert.Equal(VortexDefenderInvitationPlanStatus.InvitationPlanned, plan.Status);
		Assert.Same(defender, plan.Defender);
		Assert.Equal([1001], plan.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, plan.Alliance);
		Assert.Equal(VortexDefenderInvitationPlanService.DefenderQuestionId, plan.RequestId);
		Assert.Equal(0, plan.QuestionWindowArg1);
		Assert.Equal(0, plan.QuestionWindowArg2);
		Assert.True(plan.RequestSlotAvailable);
		Assert.True(plan.WouldInstallRequest);
		Assert.True(plan.HasQuestionWindowIntent);
		Assert.False(plan.ShouldMutateLiveRequest);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.Equal("services/vortex/Invasion.updateDefenders", plan.JavaSource);
	}

	[Fact]
	public void DefenderInvitationPlan_GuardsExistingDefenderAndFullAllianceLikeJava()
	{
		var planner = new VortexDefenderInvitationPlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var alreadyDefender = planner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int> { 1004 },
			alliance: VortexDefenderAllianceSnapshot.Open);
		var fullAlliance = planner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int>(),
			alliance: VortexDefenderAllianceSnapshot.Full);

		Assert.Equal(VortexDefenderInvitationPlanStatus.AlreadyDefender, alreadyDefender.Status);
		Assert.Equal([1004], alreadyDefender.ExistingDefenderObjectIds);
		Assert.False(alreadyDefender.WouldInstallRequest);
		Assert.False(alreadyDefender.HasQuestionWindowIntent);
		Assert.False(alreadyDefender.ShouldMutateLiveRequest);
		Assert.False(alreadyDefender.ShouldSendLivePacket);
		Assert.Null(alreadyDefender.RequestId);
		Assert.Null(alreadyDefender.QuestionWindowArg1);
		Assert.Null(alreadyDefender.QuestionWindowArg2);

		Assert.Equal(VortexDefenderInvitationPlanStatus.AllianceFull, fullAlliance.Status);
		Assert.Equal(VortexDefenderAllianceSnapshot.Full, fullAlliance.Alliance);
		Assert.False(fullAlliance.WouldInstallRequest);
		Assert.False(fullAlliance.HasQuestionWindowIntent);
		Assert.False(fullAlliance.ShouldMutateLiveRequest);
		Assert.False(fullAlliance.ShouldSendLivePacket);
		Assert.Null(fullAlliance.RequestId);
		Assert.Null(fullAlliance.QuestionWindowArg1);
		Assert.Null(fullAlliance.QuestionWindowArg2);
	}

	[Fact]
	public void DefenderInvitationPlan_RequestSlotUnavailableOmitsQuestionWindowLikeJavaPutRequestFalse()
	{
		var planner = new VortexDefenderInvitationPlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var plan = planner.CreatePlan(
			defender,
			existingDefenderObjectIds: new HashSet<int>(),
			alliance: VortexDefenderAllianceSnapshot.Missing,
			requestSlotAvailable: false);

		Assert.Equal(VortexDefenderInvitationPlanStatus.RequestNotStored, plan.Status);
		Assert.Equal(VortexDefenderAllianceSnapshot.Missing, plan.Alliance);
		Assert.Equal(VortexDefenderInvitationPlanService.DefenderQuestionId, plan.RequestId);
		Assert.Equal(0, plan.QuestionWindowArg1);
		Assert.Equal(0, plan.QuestionWindowArg2);
		Assert.False(plan.RequestSlotAvailable);
		Assert.True(plan.WouldInstallRequest);
		Assert.False(plan.HasQuestionWindowIntent);
		Assert.False(plan.ShouldMutateLiveRequest);
		Assert.False(plan.ShouldSendLivePacket);
	}

	[Fact]
	public void DefenderZoneEntryUpdatePlan_BlocksBeforeUpdateDefendersLikeJavaZoneEntry()
	{
		var planner = new VortexDefenderZoneEntryUpdatePlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var existingZonePlayer = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: true,
			existingDefenderObjectIds: new HashSet<int> { 1001 },
			isNewZonePlayer: false);
		var inactive = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: false,
			existingDefenderObjectIds: new HashSet<int> { 1001 });
		var invaderRace = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: true,
			existingDefenderObjectIds: new HashSet<int> { 1001 },
			isInvaderRace: true);

		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.NotNewZonePlayer, existingZonePlayer.Status);
		Assert.False(existingZonePlayer.IsNewZonePlayer);
		Assert.True(existingZonePlayer.HasActiveInvasion);
		Assert.Equal([1001], existingZonePlayer.ExistingDefenderObjectIds);
		Assert.False(existingZonePlayer.WouldCallUpdateDefenders);
		Assert.False(existingZonePlayer.HasInvitationPlan);
		Assert.False(existingZonePlayer.ShouldRecordZonePlayer);
		Assert.Equal("model/vortex/VortexLocation.onEnterZone", existingZonePlayer.JavaSource);
		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.InactiveVortex, inactive.Status);
		Assert.False(inactive.HasActiveInvasion);
		Assert.False(inactive.WouldCallUpdateDefenders);
		Assert.False(inactive.HasInvitationPlan);
		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.InvaderRace, invaderRace.Status);
		Assert.True(invaderRace.IsInvaderRace);
		Assert.False(invaderRace.WouldCallUpdateDefenders);
		Assert.False(invaderRace.HasInvitationPlan);
	}

	[Fact]
	public void DefenderZoneEntryUpdatePlan_SelectsInvitationPlanForNewActiveDefender()
	{
		var planner = new VortexDefenderZoneEntryUpdatePlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var plan = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: true,
			existingDefenderObjectIds: new HashSet<int> { 1001 },
			defenderAlliance: VortexDefenderAllianceSnapshot.Open);

		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.InvitationPlanned, plan.Status);
		Assert.Equal(0, plan.LocationId);
		Assert.Equal(1004, plan.PlayerObjectId);
		Assert.Equal([1001], plan.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderAllianceSnapshot.Open, plan.DefenderAlliance);
		Assert.True(plan.IsNewZonePlayer);
		Assert.True(plan.HasActiveInvasion);
		Assert.False(plan.IsInvaderRace);
		Assert.True(plan.WouldCallUpdateDefenders);
		Assert.True(plan.HasInvitationPlan);
		Assert.Equal(VortexDefenderInvitationPlanStatus.InvitationPlanned, plan.InvitationPlan?.Status);
		Assert.True(plan.WouldInstallRequest);
		Assert.True(plan.HasQuestionWindowIntent);
		Assert.False(plan.ShouldMutateLiveRequest);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.False(plan.ShouldMutateLiveDefenders);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldMutateLiveGroup);
		Assert.Equal(
			"model/vortex/VortexLocation.onEnterZone -> services/vortex/Invasion.updateDefenders",
			plan.JavaSource);
	}

	[Fact]
	public void DefenderZoneEntryUpdatePlan_PropagatesExistingDefenderAndFullAllianceGuards()
	{
		var planner = new VortexDefenderZoneEntryUpdatePlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var alreadyDefender = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: true,
			existingDefenderObjectIds: new HashSet<int> { 1004 },
			defenderAlliance: VortexDefenderAllianceSnapshot.Open);
		var fullAlliance = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: true,
			existingDefenderObjectIds: new HashSet<int>(),
			defenderAlliance: VortexDefenderAllianceSnapshot.Full);

		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.AlreadyDefender, alreadyDefender.Status);
		Assert.True(alreadyDefender.WouldCallUpdateDefenders);
		Assert.True(alreadyDefender.HasInvitationPlan);
		Assert.Equal(VortexDefenderInvitationPlanStatus.AlreadyDefender, alreadyDefender.InvitationPlan?.Status);
		Assert.False(alreadyDefender.WouldInstallRequest);
		Assert.False(alreadyDefender.HasQuestionWindowIntent);
		Assert.Equal([1004], alreadyDefender.ExistingDefenderObjectIds);
		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.DefenderAllianceFull, fullAlliance.Status);
		Assert.True(fullAlliance.WouldCallUpdateDefenders);
		Assert.True(fullAlliance.HasInvitationPlan);
		Assert.Equal(VortexDefenderInvitationPlanStatus.AllianceFull, fullAlliance.InvitationPlan?.Status);
		Assert.False(fullAlliance.WouldInstallRequest);
		Assert.False(fullAlliance.HasQuestionWindowIntent);
		Assert.Equal(VortexDefenderAllianceSnapshot.Full, fullAlliance.DefenderAlliance);
	}

	[Fact]
	public void DefenderZoneEntryUpdatePlan_RequestSlotUnavailableOmitsQuestionWindow()
	{
		var planner = new VortexDefenderZoneEntryUpdatePlanService();
		var defender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS");

		var plan = planner.CreatePlan(
			locationId: 0,
			defender,
			hasActiveInvasion: true,
			existingDefenderObjectIds: new HashSet<int>(),
			defenderAlliance: VortexDefenderAllianceSnapshot.Missing,
			requestSlotAvailable: false);

		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.RequestNotStored, plan.Status);
		Assert.True(plan.WouldCallUpdateDefenders);
		Assert.True(plan.HasInvitationPlan);
		Assert.Equal(VortexDefenderInvitationPlanStatus.RequestNotStored, plan.InvitationPlan?.Status);
		Assert.False(plan.RequestSlotAvailable);
		Assert.True(plan.WouldInstallRequest);
		Assert.False(plan.HasQuestionWindowIntent);
		Assert.False(plan.ShouldMutateLiveRequest);
		Assert.False(plan.ShouldSendLivePacket);
	}

	[Fact]
	public void ZoneLeaveKickSchedulePlan_BlocksWhileStillInsideAndRemovesOnlyWhenInactive()
	{
		var planner = new VortexZoneLeaveKickSchedulePlanService();
		var invader = new VortexZonePlayerSnapshot(PlayerObjectId: 1002, Race: "ASMODIANS");

		var stillInside = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			isInvaderRace: true,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			isStillInsideLocation: true);
		var inactive = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: false,
			isInvaderRace: true,
			passedPlayerObjectIds: new HashSet<int> { 1002 });

		Assert.Equal(VortexZoneLeaveKickSchedulePlanStatus.StillInsideLocation, stillInside.Status);
		Assert.True(stillInside.IsStillInsideLocation);
		Assert.False(stillInside.WouldRemoveZonePlayer);
		Assert.False(stillInside.WouldScheduleKick);
		Assert.False(stillInside.WouldSendBattlefieldLeftMessage);
		Assert.False(stillInside.ShouldMutateLiveZonePlayers);
		Assert.Equal("model/vortex/VortexLocation.onLeaveZone", stillInside.JavaSource);
		Assert.Equal(VortexZoneLeaveKickSchedulePlanStatus.InactiveVortex, inactive.Status);
		Assert.False(inactive.HasActiveInvasion);
		Assert.True(inactive.WouldRemoveZonePlayer);
		Assert.False(inactive.WouldScheduleKick);
		Assert.False(inactive.WouldSendBattlefieldLeftMessage);
		Assert.False(inactive.ShouldMutateLiveZonePlayers);
	}

	[Fact]
	public void ZoneLeaveKickSchedulePlan_InvaderRequiresPassedPlayerBeforeMessageAndKickSchedule()
	{
		var planner = new VortexZoneLeaveKickSchedulePlanService();
		var invader = new VortexZonePlayerSnapshot(PlayerObjectId: 1002, Race: "ASMODIANS");

		var missingPass = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			isInvaderRace: true,
			passedPlayerObjectIds: new HashSet<int> { 1003 });
		var scheduled = planner.CreatePlan(
			locationId: 0,
			invader,
			hasActiveInvasion: true,
			isInvaderRace: true,
			passedPlayerObjectIds: new HashSet<int> { 1002, 1003 });

		Assert.Equal(VortexZoneLeaveKickSchedulePlanStatus.InvaderMissingPassedPlayer, missingPass.Status);
		Assert.True(missingPass.WouldRemoveZonePlayer);
		Assert.False(missingPass.HadPassedPortal);
		Assert.Equal([1003], missingPass.PassedPlayerObjectIds);
		Assert.False(missingPass.WouldSendBattlefieldLeftMessage);
		Assert.False(missingPass.WouldScheduleKick);
		Assert.Equal(VortexZoneLeaveKickSchedulePlanStatus.InvaderKickScheduled, scheduled.Status);
		Assert.Equal(0, scheduled.LocationId);
		Assert.Equal(1002, scheduled.PlayerObjectId);
		Assert.Equal([1002, 1003], scheduled.PassedPlayerObjectIds);
		Assert.True(scheduled.HadPassedPortal);
		Assert.True(scheduled.WouldRemoveZonePlayer);
		Assert.True(scheduled.WouldSendBattlefieldLeftMessage);
		Assert.Equal(VortexZoneLeaveKickSchedulePlanService.BattlefieldLeftMessageId, scheduled.BattlefieldLeftMessageId);
		Assert.True(scheduled.WouldScheduleKick);
		Assert.True(scheduled.ScheduledKickIsInvader);
		Assert.Equal(VortexZoneLeaveKickSchedulePlanService.KickDelaySeconds, scheduled.ScheduledKickDelaySeconds);
		Assert.True(scheduled.ScheduledKickRequiresOnline);
		Assert.True(scheduled.ScheduledKickRequiresOutsideActiveVortex);
		Assert.False(scheduled.ShouldSendLivePacket);
		Assert.False(scheduled.ShouldScheduleLiveKick);
		Assert.False(scheduled.ShouldMutateLiveParticipants);
		Assert.Equal(
			"model/vortex/VortexLocation.onLeaveZone -> ThreadPoolManager.schedule -> services/vortex/Invasion.kickPlayer",
			scheduled.JavaSource);
	}

	[Fact]
	public void ZoneLeaveKickSchedulePlan_DefenderSchedulesKickWithoutBattlefieldLeftMessage()
	{
		var planner = new VortexZoneLeaveKickSchedulePlanService();
		var offlineDefender = new VortexZonePlayerSnapshot(PlayerObjectId: 1004, Race: "ELYOS", IsOnline: false);

		var plan = planner.CreatePlan(
			locationId: 0,
			offlineDefender,
			hasActiveInvasion: true,
			isInvaderRace: false,
			passedPlayerObjectIds: new HashSet<int> { 1002 });

		Assert.Equal(VortexZoneLeaveKickSchedulePlanStatus.DefenderKickScheduled, plan.Status);
		Assert.False(plan.Player.IsOnline);
		Assert.False(plan.IsInvaderRace);
		Assert.False(plan.HadPassedPortal);
		Assert.True(plan.WouldRemoveZonePlayer);
		Assert.False(plan.WouldSendBattlefieldLeftMessage);
		Assert.Null(plan.BattlefieldLeftMessageId);
		Assert.True(plan.WouldScheduleKick);
		Assert.False(plan.ScheduledKickIsInvader);
		Assert.Equal(VortexZoneLeaveKickSchedulePlanService.KickDelaySeconds, plan.ScheduledKickDelaySeconds);
		Assert.True(plan.ScheduledKickRequiresOnline);
		Assert.True(plan.ScheduledKickRequiresOutsideActiveVortex);
		Assert.False(plan.ShouldScheduleLiveKick);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.False(plan.ShouldMutateLiveParticipants);
	}

	[Fact]
	public void KickPlayerRemovalPlan_OfflineInvaderRemovesParticipantPassedPlayerAndSyncsWithoutPackets()
	{
		var planner = new VortexKickPlayerRemovalPlanService();
		var player = new VortexKickPlayerSnapshot(PlayerObjectId: 1002, IsOnline: false, WorldId: 220050000);

		var plan = planner.CreatePlan(
			locationId: 0,
			player,
			isInvader: true,
			isParticipant: true,
			alliance: VortexKickPlayerAllianceSnapshot.MemberActive,
			passedPlayerObjectIds: new HashSet<int> { 1002, 1003 },
			passedPlayerCountAfterRemoval: 1,
			invasionWorldId: 220050000,
			homePoint: new WorldPosition(210060000, 951, 2433, 107, 0));

		Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemoved, plan.Status);
		Assert.True(plan.WouldRemoveParticipant);
		Assert.True(plan.WouldRemoveFromAlliance);
		Assert.Null(plan.AllianceKickMessageId);
		Assert.False(plan.WouldTeleportHome);
		Assert.Null(plan.DirectPortalOutMessageId);
		Assert.True(plan.WouldRemovePassedPlayer);
		Assert.True(plan.WouldSyncPassedPlayers);
		Assert.Equal(1, plan.PassedPlayerSyncPlan.PassedPlayerCount);
		Assert.False(plan.ShouldMutateLiveParticipants);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.False(plan.ShouldTeleportLivePlayer);
		Assert.False(plan.ShouldMutateLivePassedPlayers);
		Assert.False(plan.ShouldSyncLivePassedPlayers);
		Assert.Equal("services/vortex/Invasion.kickPlayer", plan.JavaSource);
	}

	[Fact]
	public void KickPlayerRemovalPlan_OnlineInvaderInInvasionWorldSendsPortalOutAndTeleportsHome()
	{
		var planner = new VortexKickPlayerRemovalPlanService();
		var player = new VortexKickPlayerSnapshot(PlayerObjectId: 1002, IsOnline: true, WorldId: 220050000);
		var homePoint = new WorldPosition(210060000, 951, 2433, 107, 0);

		var plan = planner.CreatePlan(
			locationId: 0,
			player,
			isInvader: true,
			isParticipant: true,
			alliance: VortexKickPlayerAllianceSnapshot.MemberDisbandedAfterRemoval,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			passedPlayerCountAfterRemoval: 0,
			invasionWorldId: 220050000,
			homePoint: homePoint);

		Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, plan.Status);
		Assert.True(plan.WasOnline);
		Assert.True(plan.WasInInvasionWorld);
		Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, plan.AllianceKickMessageId);
		Assert.True(plan.WouldClearAllianceReference);
		Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, plan.DirectPortalOutMessageId);
		Assert.True(plan.WouldTeleportHome);
		Assert.Equal(homePoint, plan.HomePoint);
		Assert.True(plan.WouldRemovePassedPlayer);
		Assert.True(plan.WouldSyncPassedPlayers);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.False(plan.ShouldTeleportLivePlayer);
	}

	[Fact]
	public void KickPlayerRemovalPlan_OnlineDefenderRemovesAllianceAndSendsDefenderKickOnly()
	{
		var planner = new VortexKickPlayerRemovalPlanService();
		var player = new VortexKickPlayerSnapshot(PlayerObjectId: 1004, IsOnline: true, WorldId: 220050000);

		var plan = planner.CreatePlan(
			locationId: 0,
			player,
			isInvader: false,
			isParticipant: true,
			alliance: VortexKickPlayerAllianceSnapshot.MemberDisbandedAfterRemoval,
			passedPlayerObjectIds: new HashSet<int>(),
			passedPlayerCountAfterRemoval: 0,
			invasionWorldId: 220050000);

		Assert.Equal(VortexKickPlayerRemovalPlanStatus.DefenderRemovedFromAlliance, plan.Status);
		Assert.True(plan.WouldRemoveParticipant);
		Assert.True(plan.WouldRemoveFromAlliance);
		Assert.Equal(VortexKickPlayerRemovalPlanService.DefenderAllianceKickMessageId, plan.AllianceKickMessageId);
		Assert.True(plan.WouldClearAllianceReference);
		Assert.Null(plan.DirectPortalOutMessageId);
		Assert.False(plan.WouldTeleportHome);
		Assert.False(plan.WouldRemovePassedPlayer);
		Assert.True(plan.WouldSyncPassedPlayers);
		Assert.False(plan.ShouldMutateLiveParticipants);
		Assert.False(plan.ShouldMutateLiveAlliance);
		Assert.False(plan.ShouldSendLivePacket);
		Assert.False(plan.ShouldTeleportLivePlayer);
	}

	[Fact]
	public void KickPlayerRemovalPlan_NonParticipantStillRecordsPassedSyncIntentLikeJavaTailCall()
	{
		var planner = new VortexKickPlayerRemovalPlanService();
		var player = new VortexKickPlayerSnapshot(PlayerObjectId: 1004, IsOnline: true, WorldId: 210060000);

		var plan = planner.CreatePlan(
			locationId: 0,
			player,
			isInvader: false,
			isParticipant: false,
			alliance: VortexKickPlayerAllianceSnapshot.NonMember,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			passedPlayerCountAfterRemoval: 1,
			invasionWorldId: 220050000);

		Assert.Equal(VortexKickPlayerRemovalPlanStatus.NotParticipant, plan.Status);
		Assert.False(plan.WouldRemoveParticipant);
		Assert.False(plan.WouldRemoveFromAlliance);
		Assert.Null(plan.AllianceKickMessageId);
		Assert.False(plan.WouldRemovePassedPlayer);
		Assert.True(plan.WouldSyncPassedPlayers);
		Assert.Equal(1, plan.PassedPlayerSyncPlan.PassedPlayerCount);
		Assert.False(plan.ShouldMutateLiveParticipants);
		Assert.False(plan.ShouldMutateLivePassedPlayers);
		Assert.False(plan.ShouldSyncLivePassedPlayers);
	}

	[Fact]
	public void InvaderKiskZoneMembershipPlan_EnterRecordsOnlyInvaderRaceKisksLikeJava()
	{
		var planner = new VortexInvaderKiskZoneMembershipPlanService();
		var invaderKisk = new VortexKiskZoneSnapshot(KiskObjectId: 7101, Race: "ASMODIANS");
		var defenderKisk = new VortexKiskZoneSnapshot(KiskObjectId: 7102, Race: "ELYOS");

		var recorded = planner.CreateEnterPlan(invaderKisk, isInvaderRace: true);
		var skipped = planner.CreateEnterPlan(defenderKisk, isInvaderRace: false);

		Assert.Equal(VortexInvaderKiskZoneMembershipPlanStatus.EnterRecordInvaderKisk, recorded.Status);
		Assert.Equal(7101, recorded.KiskObjectId);
		Assert.Equal("ASMODIANS", recorded.Race);
		Assert.True(recorded.IsInvaderRace);
		Assert.True(recorded.WouldRecordInvaderKisk);
		Assert.False(recorded.WouldRemoveInvaderKisk);
		Assert.False(recorded.ShouldMutateLiveKiskMap);
		Assert.False(recorded.ShouldKillOrDespawnKisk);
		Assert.Equal("model/vortex/VortexLocation.onEnterZone", recorded.JavaSource);
		Assert.Equal(VortexInvaderKiskZoneMembershipPlanStatus.EnterNonInvaderRace, skipped.Status);
		Assert.False(skipped.IsInvaderRace);
		Assert.False(skipped.WouldRecordInvaderKisk);
		Assert.False(skipped.WouldRemoveInvaderKisk);
		Assert.False(skipped.ShouldMutateLiveKiskMap);
	}

	[Fact]
	public void InvaderKiskZoneMembershipPlan_LeaveRemovesOnlyAfterFullyOutsideLocation()
	{
		var planner = new VortexInvaderKiskZoneMembershipPlanService();
		var kisk = new VortexKiskZoneSnapshot(KiskObjectId: 7101, Race: "ASMODIANS");

		var stillInside = planner.CreateLeavePlan(kisk, isStillInsideLocation: true);
		var removed = planner.CreateLeavePlan(kisk, isStillInsideLocation: false);

		Assert.Equal(VortexInvaderKiskZoneMembershipPlanStatus.LeaveStillInsideLocation, stillInside.Status);
		Assert.True(stillInside.IsStillInsideLocation);
		Assert.Null(stillInside.IsInvaderRace);
		Assert.False(stillInside.WouldRecordInvaderKisk);
		Assert.False(stillInside.WouldRemoveInvaderKisk);
		Assert.False(stillInside.ShouldMutateLiveKiskMap);
		Assert.Equal("model/vortex/VortexLocation.onLeaveZone", stillInside.JavaSource);
		Assert.Equal(VortexInvaderKiskZoneMembershipPlanStatus.LeaveRemoveInvaderKisk, removed.Status);
		Assert.False(removed.IsStillInsideLocation);
		Assert.Null(removed.IsInvaderRace);
		Assert.False(removed.WouldRecordInvaderKisk);
		Assert.True(removed.WouldRemoveInvaderKisk);
		Assert.False(removed.ShouldMutateLiveKiskMap);
		Assert.False(removed.ShouldKillOrDespawnKisk);
		Assert.Equal("model/vortex/VortexLocation.onLeaveZone", removed.JavaSource);
	}

	[Fact]
	public void LocationLifecyclePlan_RoutesKiskEnterAndLeaveToKiskMembershipPlanner()
	{
		var planner = new VortexLocationLifecyclePlanService();

		var enter = planner.CreateEnterPlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Kisk,
			objectId: 7101,
			race: "ASMODIANS",
			isInvaderRace: true,
			hasActiveInvasion: false);
		var leave = planner.CreateLeavePlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Kisk,
			objectId: 7101,
			race: "ASMODIANS",
			isInvaderRace: true,
			hasActiveInvasion: true,
			isStillInsideLocation: false);

		Assert.Equal(VortexLocationLifecyclePlanStatus.EnterKisk, enter.Status);
		Assert.Equal(VortexLocationLifecycleEventKind.Enter, enter.EventKind);
		Assert.True(enter.HasKiskPlan);
		Assert.Equal(VortexInvaderKiskZoneMembershipPlanStatus.EnterRecordInvaderKisk, enter.KiskPlan?.Status);
		Assert.False(enter.WouldRecordZonePlayer);
		Assert.False(enter.ShouldMutateLiveKiskMap);
		Assert.False(enter.ShouldMutateLiveZonePlayers);
		Assert.False(enter.HasInvaderEnterPlan);
		Assert.False(enter.HasDefenderEnterPlan);
		Assert.Equal(VortexLocationLifecyclePlanStatus.LeaveKisk, leave.Status);
		Assert.Equal(VortexLocationLifecycleEventKind.Leave, leave.EventKind);
		Assert.True(leave.HasKiskPlan);
		Assert.Equal(VortexInvaderKiskZoneMembershipPlanStatus.LeaveRemoveInvaderKisk, leave.KiskPlan?.Status);
		Assert.False(leave.WouldRemoveZonePlayer);
		Assert.False(leave.ShouldMutateLiveKiskMap);
		Assert.False(leave.HasLeavePlan);
	}

	[Fact]
	public void LocationLifecyclePlan_RoutesInvaderPlayerEnterToPassedPortalPlanner()
	{
		var planner = new VortexLocationLifecyclePlanService();

		var plan = planner.CreateEnterPlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Player,
			objectId: 1002,
			race: "ASMODIANS",
			isInvaderRace: true,
			hasActiveInvasion: true,
			isNewZonePlayer: true,
			passedPlayerObjectIds: new HashSet<int> { 1002 },
			existingInvaders: [],
			invaderAlliance: VortexInvaderAllianceSnapshot.Missing);

		Assert.Equal(VortexLocationLifecyclePlanStatus.EnterInvaderPlayer, plan.Status);
		Assert.Equal(VortexLocationLifecycleCreatureKind.Player, plan.CreatureKind);
		Assert.True(plan.WouldRecordZonePlayer);
		Assert.True(plan.HasInvaderEnterPlan);
		Assert.Equal(VortexInvaderPassedPortalUpdatePlanStatus.UpdatePlanned, plan.InvaderEnterPlan?.Status);
		Assert.True(plan.InvaderEnterPlan?.WouldCallAddPlayer);
		Assert.False(plan.HasKiskPlan);
		Assert.False(plan.HasDefenderEnterPlan);
		Assert.False(plan.ShouldMutateLiveZonePlayers);
		Assert.False(plan.ShouldMutateLiveParticipants);
		Assert.Equal("model/vortex/VortexLocation.onEnterZone", plan.JavaSource);
	}

	[Fact]
	public void LocationLifecyclePlan_RoutesDefenderPlayerEnterToDefenderUpdatePlanner()
	{
		var planner = new VortexLocationLifecyclePlanService();

		var plan = planner.CreateEnterPlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Player,
			objectId: 1004,
			race: "ELYOS",
			isInvaderRace: false,
			hasActiveInvasion: true,
			isNewZonePlayer: true,
			existingDefenderObjectIds: new HashSet<int>(),
			defenderAlliance: VortexDefenderAllianceSnapshot.Open);

		Assert.Equal(VortexLocationLifecyclePlanStatus.EnterDefenderPlayer, plan.Status);
		Assert.True(plan.WouldRecordZonePlayer);
		Assert.True(plan.HasDefenderEnterPlan);
		Assert.Equal(VortexDefenderZoneEntryUpdatePlanStatus.InvitationPlanned, plan.DefenderEnterPlan?.Status);
		Assert.True(plan.DefenderEnterPlan?.WouldCallUpdateDefenders);
		Assert.False(plan.HasKiskPlan);
		Assert.False(plan.HasInvaderEnterPlan);
		Assert.False(plan.ShouldMutateLiveZonePlayers);
		Assert.False(plan.ShouldMutateLiveRequests);
		Assert.False(plan.ShouldSendLivePacket);
	}

	[Fact]
	public void LocationLifecyclePlan_RoutesPlayerLeaveToKickSchedulePlanner()
	{
		var planner = new VortexLocationLifecyclePlanService();

		var plan = planner.CreateLeavePlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Player,
			objectId: 1002,
			race: "ASMODIANS",
			isInvaderRace: true,
			hasActiveInvasion: true,
			isStillInsideLocation: false,
			passedPlayerObjectIds: new HashSet<int> { 1002 });

		Assert.Equal(VortexLocationLifecyclePlanStatus.LeavePlayer, plan.Status);
		Assert.Equal(VortexLocationLifecycleEventKind.Leave, plan.EventKind);
		Assert.True(plan.WouldRemoveZonePlayer);
		Assert.True(plan.HasLeavePlan);
		Assert.Equal(VortexZoneLeaveKickSchedulePlanStatus.InvaderKickScheduled, plan.LeavePlan?.Status);
		Assert.True(plan.LeavePlan?.WouldScheduleKick);
		Assert.False(plan.HasKiskPlan);
		Assert.False(plan.HasInvaderEnterPlan);
		Assert.False(plan.HasDefenderEnterPlan);
		Assert.False(plan.ShouldMutateLiveZonePlayers);
		Assert.False(plan.ShouldScheduleLiveTask);
	}

	[Fact]
	public void LocationLifecyclePlan_IgnoresOtherCreatureKindsLikeJavaTypeChecks()
	{
		var planner = new VortexLocationLifecyclePlanService();

		var enter = planner.CreateEnterPlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Other,
			objectId: 9001,
			race: "NONE",
			isInvaderRace: false,
			hasActiveInvasion: true);
		var leave = planner.CreateLeavePlan(
			locationId: 0,
			VortexLocationLifecycleCreatureKind.Other,
			objectId: 9001,
			race: "NONE",
			isInvaderRace: false,
			hasActiveInvasion: true,
			isStillInsideLocation: false);

		Assert.Equal(VortexLocationLifecyclePlanStatus.EnterIgnoredCreature, enter.Status);
		Assert.False(enter.HasKiskPlan);
		Assert.False(enter.HasInvaderEnterPlan);
		Assert.False(enter.HasDefenderEnterPlan);
		Assert.False(enter.WouldRecordZonePlayer);
		Assert.Equal(VortexLocationLifecyclePlanStatus.LeaveIgnoredCreature, leave.Status);
		Assert.False(leave.HasKiskPlan);
		Assert.False(leave.HasLeavePlan);
		Assert.False(leave.WouldRemoveZonePlayer);
		Assert.False(leave.ShouldMutateLiveZonePlayers);
	}

	[Fact]
	public async Task DefenderAllianceUpdatePlan_SelectsOnlyJavaDefenderRaceZonePlayers()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-defender-update-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var planner = new VortexDefenderAllianceUpdatePlanService();
			var defender = CreatePlayer(1004, isOnline: true, location.InvasionWorldId, location.DefendersRace);
			var offlineDefender = CreatePlayer(1006, isOnline: false, location.InvasionWorldId, location.DefendersRace);
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId, location.InvadersRace);
			var lowerCaseDefender = CreatePlayer(1008, isOnline: true, location.InvasionWorldId, location.DefendersRace.ToLowerInvariant());

			var plan = planner.CreatePlan(
				location,
				[
					VortexZonePlayerSnapshot.FromPlayer(invader),
					VortexZonePlayerSnapshot.FromPlayer(defender),
					VortexZonePlayerSnapshot.FromPlayer(offlineDefender),
					VortexZonePlayerSnapshot.FromPlayer(lowerCaseDefender),
				]);

			Assert.Equal(VortexDefenderAllianceUpdatePlanStatus.Planned, plan.Status);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(location.DefendersRace, plan.DefendersRace);
			Assert.True(plan.WouldCallUpdateDefenders);
			Assert.False(plan.ShouldMutateLiveAlliance);
			Assert.Equal([1004, 1006], plan.DefenderObjectIds);
			Assert.Equal([1002, 1008], plan.SkippedObjectIds);
			Assert.Equal([location.DefendersRace, location.DefendersRace], plan.DefenderUpdatePlayers.Select(player => player.Race).ToArray());
			Assert.Equal(
				"services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders",
				plan.JavaSource);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task DefenderAllianceUpdatePlan_EmptyZoneHasNoLiveAllianceMutation()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-defender-update-empty-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(1));
			var planner = new VortexDefenderAllianceUpdatePlanService();

			var plan = planner.CreatePlan(location, zonePlayers: null);

			Assert.Equal(VortexDefenderAllianceUpdatePlanStatus.Planned, plan.Status);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(location.DefendersRace, plan.DefendersRace);
			Assert.False(plan.WouldCallUpdateDefenders);
			Assert.False(plan.ShouldMutateLiveAlliance);
			Assert.Empty(plan.DefenderObjectIds);
			Assert.Empty(plan.SkippedObjectIds);
			Assert.Empty(plan.DefenderUpdatePlayers);
			Assert.Empty(plan.SkippedPlayers);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task SetAndClearActivePortal_ModelsJavaSpawnAndDespawnControllerReference()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-active-portal-clear-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			runtime.StartInvasion(location);

			Assert.True(runtime.SetActivePortal(location.Id, portal));
			var withPortal = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.True(withPortal.HasActivePortal);
			Assert.Same(portal, withPortal.ActivePortal);

			Assert.True(runtime.ClearActivePortal(location.Id));
			var cleared = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.False(cleared.HasActivePortal);
			Assert.Null(cleared.ActivePortal);
			Assert.False(runtime.SetActivePortal(999, portal));
			Assert.False(runtime.ClearActivePortal(999));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopInvasion_ClearsActivePortalParticipantsAndRuntimeEntryLikeJavaStopMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-clears-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var defender = CreatePlayer(1004, isOnline: true, location.InvasionWorldId);
			var passer = CreatePlayer(1006, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location, portal);
			Assert.True(runtime.AddInvader(location.Id, invader));
			Assert.True(runtime.AddDefender(location.Id, defender));
			Assert.True(runtime.RecordPortalPass(location, passer));

			var stop = runtime.StopInvasion(location.Id);

			Assert.True(stop.Stopped);
			Assert.Equal(VortexStopInvasionStatus.Stopped, stop.Status);
			Assert.Equal(location.Id, stop.LocationId);
			Assert.True(stop.HadActivePortal);
			Assert.Equal(1, stop.RemovedInvaderCount);
			Assert.Equal(1, stop.RemovedDefenderCount);
			Assert.Equal(2, stop.RemovedPassedPlayerCount);
			Assert.Equal(
				"services/VortexService.stopInvasion -> services/vortex/Invasion.stopInvasion",
				stop.JavaSource);
			var previous = Assert.IsType<VortexInvasionSnapshot>(stop.PreviousSnapshot);
			Assert.True(previous.HasActivePortal);
			Assert.Equal([1002], previous.InvaderObjectIds);
			Assert.Equal([1004], previous.DefenderObjectIds);
			Assert.Equal([1002, 1006], previous.PassedPlayerObjectIds);
			var stopped = Assert.IsType<VortexInvasionSnapshot>(stop.StoppedSnapshot);
			Assert.False(stopped.HasActivePortal);
			Assert.Empty(stopped.InvaderObjectIds);
			Assert.Empty(stopped.DefenderObjectIds);
			Assert.Empty(stopped.PassedPlayerObjectIds);
			Assert.Null(runtime.GetSnapshot(location.Id));
			Assert.False(runtime.IsInvaderPlayer(invader));
			Assert.False(runtime.IsDefenderPlayer(defender));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopInvasion_MissingOrRepeatedStopReturnsGuardMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-missing-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();

			var missing = runtime.StopInvasion(location.Id);

			Assert.False(missing.Stopped);
			Assert.Equal(VortexStopInvasionStatus.MissingInvasion, missing.Status);
			Assert.Equal("services/VortexService.stopInvasion", missing.JavaSource);
			Assert.Null(missing.PreviousSnapshot);
			Assert.Null(missing.StoppedSnapshot);

			runtime.StartInvasion(location, CreateVortexPortal(location));
			var stopped = runtime.StopInvasion(location.Id);
			var repeated = runtime.StopInvasion(location.Id);

			Assert.True(stopped.Stopped);
			Assert.False(repeated.Stopped);
			Assert.Equal(VortexStopInvasionStatus.MissingInvasion, repeated.Status);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopInvasion_FinishedActiveInvasionRemovesEntryWithoutStopDispatchMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-finished-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);
			var defender = CreatePlayer(1004, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));
			Assert.True(runtime.AddDefender(location.Id, defender));
			Assert.True(runtime.MarkInvasionFinished(location.Id));
			var activeSnapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.True(activeSnapshot.IsFinished);

			var stop = runtime.StopInvasion(location.Id);

			Assert.False(stop.Stopped);
			Assert.Equal(VortexStopInvasionStatus.FinishedInvasion, stop.Status);
			Assert.Equal("services/VortexService.stopInvasion -> services/vortex/DimensionalVortex.isFinished", stop.JavaSource);
			Assert.True(stop.HadActivePortal);
			Assert.Equal(1, stop.RemovedInvaderCount);
			Assert.Equal(1, stop.RemovedDefenderCount);
			Assert.Equal(1, stop.RemovedPassedPlayerCount);
			var previous = Assert.IsType<VortexInvasionSnapshot>(stop.PreviousSnapshot);
			Assert.True(previous.IsFinished);
			Assert.True(previous.HasActivePortal);
			Assert.Equal([1002], previous.InvaderObjectIds);
			Assert.Equal([1004], previous.DefenderObjectIds);
			Assert.Null(stop.StoppedSnapshot);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StartInvasion_AfterStopCreatesFreshRuntimeStateLikeJavaServiceRestart()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-restart-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var firstInvader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);
			var nextInvader = CreatePlayer(1008, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, firstInvader));
			Assert.True(runtime.StopInvasion(location.Id).Stopped);

			var restarted = runtime.StartInvasion(location);
			Assert.True(runtime.AddInvader(location.Id, nextInvader));

			Assert.False(restarted.HasActivePortal);
			Assert.Empty(restarted.InvaderObjectIds);
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Equal([1008], snapshot.InvaderObjectIds);
			Assert.Equal([1008], snapshot.PassedPlayerObjectIds);
			Assert.False(runtime.IsInvaderPlayer(firstInvader));
			Assert.True(runtime.IsInvaderPlayer(nextInvader));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopSideEffectPlan_PreservesJavaStopOrderWithoutExecutingLiveEffects()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-plan-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var offlineInvader = CreatePlayer(1003, isOnline: false, location.InvasionWorldId);
			var outsideInvader = CreatePlayer(1005, isOnline: true, location.HomePoint.WorldId);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));
			Assert.True(runtime.AddInvader(location.Id, offlineInvader));
			Assert.True(runtime.AddInvader(location.Id, outsideInvader));
			var stop = runtime.StopInvasion(location.Id);
			var planner = new VortexStopInvasionSideEffectPlanService();
			var kisk = new PlayerKiskRuntimeState(7101, invader.ObjectId, 831200);
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831300,
				Template: new NpcTemplateSummary(831300, "Vortex spawned", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.StartPoint);
			var peaceSpawn = CreatePeaceSpawn(location);

			var plan = planner.CreatePlan(
				stop,
				[
					VortexStopInvaderSnapshot.FromPlayer(invader),
					VortexStopInvaderSnapshot.FromPlayer(offlineInvader),
					VortexStopInvaderSnapshot.FromPlayer(outsideInvader),
				],
				[VortexStopInvaderKiskSnapshot.FromRuntimeState(kisk)],
				[VortexStopSpawnedNpcSnapshot.FromWorldNpc(spawnedNpc)],
				[VortexStopPeaceSpawnSnapshot.FromSpawn(peaceSpawn)],
				invaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberDisbandedAfterRemoval,
					[outsideInvader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberActive,
				},
				passedPlayerObjectIds: new HashSet<int> { invader.ObjectId, outsideInvader.ObjectId });

			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.Planned, plan.Status);
			Assert.False(plan.ShouldExecuteLiveSideEffects);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(1, plan.KiskKillCount);
			Assert.Equal(2, plan.OnlineInvaderKickCount);
			Assert.Equal(1, plan.DespawnNpcCount);
			Assert.Equal(1, plan.PeaceSpawnCount);
			Assert.True(plan.HasKickRemovalPlans);
			Assert.Equal([invader.ObjectId, outsideInvader.ObjectId], plan.OrderedKickRemovalPlans.Select(kick => kick.PlayerObjectId).ToArray());
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				plan.OrderedSteps.Select(step => step.Kind).ToArray());
			var insideKick = plan.OrderedSteps.Single(step =>
				step.Kind == VortexStopInvasionSideEffectStepKind.KickOnlineInvader
				&& step.PlayerObjectId == invader.ObjectId);
			Assert.True(insideKick.WasInInvasionWorld);
			Assert.True(insideKick.ShouldTeleportHome);
			Assert.Equal(location.HomePoint, insideKick.TeleportDestination);
			var insideKickRemoval = Assert.IsType<VortexKickPlayerRemovalPlan>(insideKick.KickRemovalPlan);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, insideKickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, insideKickRemoval.AllianceKickMessageId);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, insideKickRemoval.DirectPortalOutMessageId);
			Assert.True(insideKickRemoval.WouldClearAllianceReference);
			Assert.True(insideKickRemoval.WouldRemovePassedPlayer);
			Assert.Equal(1, insideKickRemoval.PassedPlayerSyncPlan.PassedPlayerCount);
			Assert.False(insideKickRemoval.ShouldSendLivePacket);
			Assert.False(insideKickRemoval.ShouldTeleportLivePlayer);
			var outsideKick = plan.OrderedSteps.Single(step =>
				step.Kind == VortexStopInvasionSideEffectStepKind.KickOnlineInvader
				&& step.PlayerObjectId == outsideInvader.ObjectId);
			Assert.False(outsideKick.WasInInvasionWorld);
			Assert.False(outsideKick.ShouldTeleportHome);
			Assert.Null(outsideKick.TeleportDestination);
			var outsideKickRemoval = Assert.IsType<VortexKickPlayerRemovalPlan>(outsideKick.KickRemovalPlan);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemoved, outsideKickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, outsideKickRemoval.AllianceKickMessageId);
			Assert.Null(outsideKickRemoval.DirectPortalOutMessageId);
			Assert.True(outsideKickRemoval.WouldRemovePassedPlayer);
			Assert.Equal(0, outsideKickRemoval.PassedPlayerSyncPlan.PassedPlayerCount);
			Assert.False(outsideKickRemoval.ShouldMutateLiveParticipants);
			Assert.False(outsideKickRemoval.ShouldMutateLivePassedPlayers);
			Assert.False(outsideKickRemoval.ShouldSyncLivePassedPlayers);
			Assert.DoesNotContain(plan.OrderedSteps, step => step.PlayerObjectId == offlineInvader.ObjectId);
			Assert.DoesNotContain(plan.OrderedKickRemovalPlans, kick => kick.PlayerObjectId == offlineInvader.ObjectId);
			Assert.Equal(VortexStateType.Peace, plan.OrderedSteps.Last().VortexState);
			Assert.Same(peaceSpawn, plan.OrderedSteps.Last().Spawn);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopSideEffectPlan_MissingStopOrSnapshotsReturnsGuardMetadata()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-plan-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var planner = new VortexStopInvasionSideEffectPlanService();

			var missingStop = planner.CreatePlan(runtime.StopInvasion(location.Id));
			var missingSnapshots = planner.CreatePlan(new VortexStopInvasionResult(
				Stopped: true,
				LocationId: location.Id,
				Status: VortexStopInvasionStatus.Stopped,
				JavaSource: "test missing snapshots"));

			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.MissingInvasion, missingStop.Status);
			Assert.Empty(missingStop.OrderedSteps);
			Assert.False(missingStop.ShouldExecuteLiveSideEffects);
			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.MissingStopSnapshot, missingSnapshots.Status);
			Assert.Empty(missingSnapshots.OrderedSteps);
			Assert.False(missingSnapshots.ShouldExecuteLiveSideEffects);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public void VortexStateType_MetadataCarriesJavaStatesInOrder()
	{
		Assert.Equal([VortexStateType.Invasion, VortexStateType.Peace], Enum.GetValues<VortexStateType>());
		Assert.Equal(["Invasion", "Peace"], Enum.GetNames<VortexStateType>());
	}

	[Fact]
	public void PeaceSpawnSelection_SelectsJavaPeaceRowsForVortexLocation()
	{
		var selector = new VortexPeaceSpawnSnapshotSelectionService();
		var table = new NpcVortexSpawnTable(
			[
				CreateVortexSpawn(0, 0, 0, VortexStateType.Peace, 831500, "peace-a"),
				CreateVortexSpawn(0, 0, 1, VortexStateType.Invasion, 831600, "invasion-a"),
				CreateVortexSpawn(0, 1, 0, VortexStateType.Peace, 831501, "peace-b"),
				CreateVortexSpawn(1, 0, 0, VortexStateType.Peace, 831700, "other-location"),
			]);

		var selected = selector.SelectPeaceSpawns(0, table);

		Assert.Equal(2, selected.Count);
		Assert.All(selected, snapshot => Assert.Equal(VortexStateType.Peace, snapshot.State));
		Assert.Equal([831500, 831501], selected.Select(snapshot => snapshot.Spawn.NpcId).ToArray());
		Assert.Equal(["peace-a", "peace-b"], selected.Select(snapshot => snapshot.Spawn.Anchor).ToArray());
		Assert.Equal([210060000, 210060000], selected.Select(snapshot => snapshot.Spawn.MapId).ToArray());
		Assert.DoesNotContain(selected, snapshot => snapshot.Spawn.NpcId == 831600);
		Assert.DoesNotContain(selected, snapshot => snapshot.Spawn.NpcId == 831700);
	}

	[Fact]
	public async Task PeaceSpawnSelection_FeedsStopPlanWithoutLiveSpawnExecution()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-peace-spawn-selection-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var planner = new VortexStopInvasionSideEffectPlanService();
			var selector = new VortexPeaceSpawnSnapshotSelectionService();
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Peace, 831500, "peace-a"),
				]);
			runtime.StartInvasion(location, CreateVortexPortal(location));

			var stop = runtime.StopInvasion(location.Id);
			var peaceSpawns = selector.SelectPeaceSpawns(location.Id, table);
			var plan = planner.CreatePlan(stop, peaceSpawns: peaceSpawns);

			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.Planned, plan.Status);
			Assert.False(plan.ShouldExecuteLiveSideEffects);
			Assert.Equal(1, plan.PeaceSpawnCount);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				plan.OrderedSteps.Select(step => step.Kind).ToArray());
			var spawnStep = Assert.Single(plan.OrderedSteps, step => step.Kind == VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc);
			Assert.Equal(831500, spawnStep.NpcId);
			Assert.Equal(VortexStateType.Peace, spawnStep.VortexState);
			Assert.Equal("peace-a", spawnStep.Spawn?.Anchor);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public void PeaceSpawnSnapshot_RejectsInvasionVortexRows()
	{
		var invasionSpawn = CreateVortexSpawn(0, 0, 0, VortexStateType.Invasion, 831600, "invasion-a");

		Assert.Throws<ArgumentException>(() => VortexStopPeaceSpawnSnapshot.FromVortexSpawn(invasionSpawn));
	}

	[Fact]
	public void StopSnapshotRequest_NormalizesMissingGroupsWithoutLiveLookup()
	{
		var invader = CreatePlayer(1002, isOnline: true, worldId: 210060000);
		var request = new VortexStopInvasionSnapshotRequest(
			Invaders: [VortexStopInvaderSnapshot.FromPlayer(invader)]);

		Assert.True(request.HasAnySnapshot);
		Assert.Equal([1002], request.InvaderSnapshots.Select(snapshot => snapshot.PlayerObjectId).ToArray());
		Assert.Empty(request.InvaderKiskSnapshots);
		Assert.Empty(request.SpawnedNpcSnapshots);
		Assert.Empty(request.PeaceSpawnSnapshots);
		Assert.False(VortexStopInvasionSnapshotRequest.Empty.HasAnySnapshot);
	}

	[Fact]
	public void StopSnapshotRequest_AppendsSelectedPeaceSpawnsWithoutReplacingSuppliedSnapshots()
	{
		var selector = new VortexPeaceSpawnSnapshotSelectionService();
		var suppliedPeaceSpawn = VortexStopPeaceSpawnSnapshot.FromVortexSpawn(
			CreateVortexSpawn(0, 9, 0, VortexStateType.Peace, 831499, "supplied-peace"));
		var request = new VortexStopInvasionSnapshotRequest(
			PeaceSpawns: [suppliedPeaceSpawn],
			InvaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
			{
				[1002] = VortexKickPlayerAllianceSnapshot.MemberActive,
			},
			PassedPlayerObjectIds: new HashSet<int> { 1002 });
		var table = new NpcVortexSpawnTable(
			[
				CreateVortexSpawn(0, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"),
				CreateVortexSpawn(0, 1, 0, VortexStateType.Peace, 831500, "static-peace"),
				CreateVortexSpawn(1, 0, 0, VortexStateType.Peace, 831700, "other-location"),
			]);

		var enriched = request.WithPeaceSpawns(selector.SelectPeaceSpawns(0, table));

		Assert.Equal([831499], request.PeaceSpawnSnapshots.Select(snapshot => snapshot.Spawn.NpcId).ToArray());
		Assert.Equal([831499, 831500], enriched.PeaceSpawnSnapshots.Select(snapshot => snapshot.Spawn.NpcId).ToArray());
		Assert.Equal(["supplied-peace", "static-peace"], enriched.PeaceSpawnSnapshots.Select(snapshot => snapshot.Spawn.Anchor).ToArray());
		Assert.Equal([1002], enriched.InvaderAllianceSnapshots.Keys.ToArray());
		Assert.Equal([1002], enriched.PassedPlayerSnapshots.ToArray());
		Assert.DoesNotContain(enriched.PeaceSpawnSnapshots, snapshot => snapshot.Spawn.NpcId == 831600);
		Assert.DoesNotContain(enriched.PeaceSpawnSnapshots, snapshot => snapshot.Spawn.NpcId == 831700);
	}

	[Fact]
	public async Task StopRuntimeSnapshotCollector_CapturesJavaStopInputsAndFeedsCoordinatorWithoutLiveExecution()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-runtime-collector-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var collector = new VortexStopInvasionRuntimeSnapshotCollectorService();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());
			var onlineInvader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var offlineInvader = CreatePlayer(1003, isOnline: false, location.InvasionWorldId);
			var unrelatedPlayer = CreatePlayer(1999, isOnline: true, location.InvasionWorldId);
			var kisk = new PlayerKiskRuntimeState(7101, onlineInvader.ObjectId, 831200);
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831300,
				Template: new NpcTemplateSummary(831300, "Vortex spawned", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.StartPoint);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, onlineInvader));
			Assert.True(runtime.AddInvader(location.Id, offlineInvader));
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));

			var request = collector.Collect(
				snapshot,
				players: [onlineInvader, offlineInvader, unrelatedPlayer],
				invaderKisks: [kisk],
				spawnedNpcs: [spawnedNpc],
				invaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[onlineInvader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberDisbandedAfterRemoval,
					[unrelatedPlayer.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberActive,
				});
			var report = coordinator.StopInvasion(location.Id, request);

			Assert.True(request.HasAnySnapshot);
			Assert.Equal([onlineInvader.ObjectId, offlineInvader.ObjectId], request.InvaderSnapshots.Select(invader => invader.PlayerObjectId).ToArray());
			Assert.Equal([onlineInvader.ObjectId, offlineInvader.ObjectId], request.PassedPlayerSnapshots.Order().ToArray());
			Assert.Equal([onlineInvader.ObjectId], request.InvaderAllianceSnapshots.Keys.ToArray());
			Assert.Equal([kisk.ObjectId], request.InvaderKiskSnapshots.Select(invaderKisk => invaderKisk.KiskObjectId).ToArray());
			Assert.Equal([spawnedNpc.ObjectId], request.SpawnedNpcSnapshots.Select(npc => npc.ObjectId).ToArray());
			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			var kickRemoval = Assert.Single(report.OrderedKickRemovalPlans);
			Assert.Equal(onlineInvader.ObjectId, kickRemoval.PlayerObjectId);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, kickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, kickRemoval.AllianceKickMessageId);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, kickRemoval.DirectPortalOutMessageId);
			Assert.True(kickRemoval.WouldClearAllianceReference);
			Assert.True(kickRemoval.WouldRemovePassedPlayer);
			AssertPassedSyncPlan(kickRemoval.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 1);
			Assert.False(kickRemoval.ShouldSendLivePacket);
			Assert.False(kickRemoval.ShouldTeleportLivePlayer);
			Assert.False(kickRemoval.ShouldMutateLiveParticipants);
			Assert.False(kickRemoval.ShouldMutateLivePassedPlayers);
			Assert.False(kickRemoval.ShouldSyncLivePassedPlayers);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopRuntimeSnapshotCollector_PreparesRuntimeSnapshotsWithStaticPeaceSpawnsWithoutLiveExecution()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-runtime-static-peace-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var collector = new VortexStopInvasionRuntimeSnapshotCollectorService();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var kisk = new PlayerKiskRuntimeState(7101, invader.ObjectId, 831200);
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831300,
				Template: new NpcTemplateSummary(831300, "Vortex spawned", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.StartPoint);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Peace, 831500, "peace-a"),
					CreateVortexSpawn(location.Id + 1, 0, 0, VortexStateType.Peace, 831700, "other-location"),
				]);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));

			var request = collector.PrepareWithStaticPeaceSpawns(
				location.Id,
				snapshot,
				table,
				players: [invader],
				invaderKisks: [kisk],
				spawnedNpcs: [spawnedNpc],
				invaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberActive,
				});
			var report = coordinator.StopInvasion(location.Id, request);

			Assert.Equal([invader.ObjectId], request.InvaderSnapshots.Select(item => item.PlayerObjectId).ToArray());
			Assert.Equal([kisk.ObjectId], request.InvaderKiskSnapshots.Select(item => item.KiskObjectId).ToArray());
			Assert.Equal([spawnedNpc.ObjectId], request.SpawnedNpcSnapshots.Select(item => item.ObjectId).ToArray());
			Assert.Equal([invader.ObjectId], request.PassedPlayerSnapshots.ToArray());
			Assert.Equal([831500], request.PeaceSpawnSnapshots.Select(item => item.Spawn.NpcId).ToArray());
			Assert.Equal(["peace-a"], request.PeaceSpawnSnapshots.Select(item => item.Spawn.Anchor).ToArray());
			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			var spawnStep = Assert.Single(report.SideEffectPlan.OrderedSteps, step => step.Kind == VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc);
			Assert.Equal(831500, spawnStep.NpcId);
			Assert.Equal(VortexStateType.Peace, spawnStep.VortexState);
			var kickRemoval = Assert.Single(report.OrderedKickRemovalPlans);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, kickRemoval.Status);
			AssertPassedSyncPlan(kickRemoval.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.False(kickRemoval.ShouldSendLivePacket);
			Assert.False(kickRemoval.ShouldTeleportLivePlayer);
			Assert.False(kickRemoval.ShouldMutateLiveParticipants);
			Assert.False(kickRemoval.ShouldMutateLivePassedPlayers);
			Assert.False(kickRemoval.ShouldSyncLivePassedPlayers);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public void StopRuntimeSnapshotCollector_StaticPeacePreparationMissingSnapshotSkipsStaticSelection()
	{
		var peaceSpawnSelector = new CountingPeaceSpawnSelector();
		var collector = new VortexStopInvasionRuntimeSnapshotCollectorService(peaceSpawnSelector);
		var table = new NpcVortexSpawnTable(
			[
				CreateVortexSpawn(0, 0, 0, VortexStateType.Peace, 831500, "peace-a"),
			]);

		var request = collector.PrepareWithStaticPeaceSpawns(0, null, table);

		Assert.False(request.HasAnySnapshot);
		Assert.Empty(request.PeaceSpawnSnapshots);
		Assert.Equal(0, peaceSpawnSelector.CallCount);
		Assert.Empty(peaceSpawnSelector.LocationIds);
	}

	[Fact]
	public void StopRuntimeSnapshotCollector_MissingSnapshotReturnsEmptyRequest()
	{
		var collector = new VortexStopInvasionRuntimeSnapshotCollectorService();
		var invader = CreatePlayer(1002, isOnline: true, worldId: 210060000);

		var request = collector.Collect(
			null,
			players: [invader],
			invaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
			{
				[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberActive,
			});

		Assert.False(request.HasAnySnapshot);
		Assert.Empty(request.InvaderSnapshots);
		Assert.Empty(request.InvaderKiskSnapshots);
		Assert.Empty(request.SpawnedNpcSnapshots);
		Assert.Empty(request.InvaderAllianceSnapshots);
		Assert.Empty(request.PassedPlayerSnapshots);
	}

	[Fact]
	public async Task StopCoordinator_ComposesRuntimeStopAndSideEffectPlanWithoutLiveExecution()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var defender = CreatePlayer(1004, isOnline: true, location.InvasionWorldId);
			var kisk = new PlayerKiskRuntimeState(7101, invader.ObjectId, 831200);
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831300,
				Template: new NpcTemplateSummary(831300, "Vortex spawned", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.StartPoint);
			var peaceSpawn = CreatePeaceSpawn(location);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));
			Assert.True(runtime.AddDefender(location.Id, defender));

			var report = coordinator.StopInvasion(
				location.Id,
				[VortexStopInvaderSnapshot.FromPlayer(invader)],
				[VortexStopInvaderKiskSnapshot.FromRuntimeState(kisk)],
				[VortexStopSpawnedNpcSnapshot.FromWorldNpc(spawnedNpc)],
				[VortexStopPeaceSpawnSnapshot.FromSpawn(peaceSpawn)],
				invaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberDisbandedAfterRemoval,
				},
				passedPlayerObjectIds: new HashSet<int> { invader.ObjectId });

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.True(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			Assert.Equal(location.Id, report.LocationId);
			Assert.Equal(location.Id, report.StopResult.LocationId);
			Assert.Equal(VortexStopInvasionStatus.Stopped, report.StopResult.Status);
			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.Planned, report.SideEffectPlan.Status);
			Assert.True(report.HasKickRemovalPlans);
			var kickRemoval = Assert.Single(report.OrderedKickRemovalPlans);
			Assert.Equal(invader.ObjectId, kickRemoval.PlayerObjectId);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, kickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, kickRemoval.AllianceKickMessageId);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, kickRemoval.DirectPortalOutMessageId);
			Assert.True(kickRemoval.WouldClearAllianceReference);
			Assert.True(kickRemoval.WouldRemovePassedPlayer);
			AssertPassedSyncPlan(kickRemoval.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.False(kickRemoval.ShouldSendLivePacket);
			Assert.False(kickRemoval.ShouldTeleportLivePlayer);
			Assert.False(kickRemoval.ShouldMutateLiveParticipants);
			Assert.False(kickRemoval.ShouldMutateLivePassedPlayers);
			Assert.False(kickRemoval.ShouldSyncLivePassedPlayers);
			Assert.Equal([1002], Assert.IsType<VortexInvasionSnapshot>(report.StopResult.PreviousSnapshot).InvaderObjectIds);
			Assert.Equal([1004], Assert.IsType<VortexInvasionSnapshot>(report.StopResult.PreviousSnapshot).DefenderObjectIds);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Null(runtime.GetSnapshot(location.Id));
			Assert.False(runtime.IsInvaderPlayer(invader));
			Assert.False(runtime.IsDefenderPlayer(defender));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_SnapshotRequestDelegatesToExistingStopPlanPath()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-request-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var kisk = new PlayerKiskRuntimeState(7101, invader.ObjectId, 831200);
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831300,
				Template: new NpcTemplateSummary(831300, "Vortex spawned", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.StartPoint);
			var peaceSpawn = CreatePeaceSpawn(location);
			var request = new VortexStopInvasionSnapshotRequest(
				Invaders: [VortexStopInvaderSnapshot.FromPlayer(invader)],
				InvaderKisks: [VortexStopInvaderKiskSnapshot.FromRuntimeState(kisk)],
				SpawnedNpcs: [VortexStopSpawnedNpcSnapshot.FromWorldNpc(spawnedNpc)],
				PeaceSpawns: [VortexStopPeaceSpawnSnapshot.FromSpawn(peaceSpawn)],
				InvaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberActive,
				},
				PassedPlayerObjectIds: new HashSet<int> { invader.ObjectId });
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));

			var report = coordinator.StopInvasion(location.Id, request);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.True(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			var kickRemoval = Assert.Single(report.OrderedKickRemovalPlans);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, kickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, kickRemoval.AllianceKickMessageId);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, kickRemoval.DirectPortalOutMessageId);
			Assert.False(kickRemoval.WouldClearAllianceReference);
			AssertPassedSyncPlan(kickRemoval.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Equal(VortexStateType.Peace, report.SideEffectPlan.OrderedSteps.Last().VortexState);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_StaticPeaceSpawnRequestEnrichmentFeedsPlannerWithoutLiveExecution()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-static-peace-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());
			var suppliedPeaceSpawn = VortexStopPeaceSpawnSnapshot.FromVortexSpawn(
				CreateVortexSpawn(location.Id, 9, 0, VortexStateType.Peace, 831499, "supplied-peace"));
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var request = new VortexStopInvasionSnapshotRequest(
				Invaders: [VortexStopInvaderSnapshot.FromPlayer(invader)],
				PeaceSpawns: [suppliedPeaceSpawn],
				InvaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberActive,
				},
				PassedPlayerObjectIds: new HashSet<int> { invader.ObjectId });
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Peace, 831500, "static-peace"),
					CreateVortexSpawn(location.Id + 1, 0, 0, VortexStateType.Peace, 831700, "other-location"),
				]);
			runtime.StartInvasion(location, CreateVortexPortal(location));

			var report = coordinator.StopInvasion(location.Id, request, table);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			var kickRemoval = Assert.Single(report.OrderedKickRemovalPlans);
			Assert.Equal(invader.ObjectId, kickRemoval.PlayerObjectId);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, kickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, kickRemoval.DirectPortalOutMessageId);
			AssertPassedSyncPlan(kickRemoval.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.Equal(2, report.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			var spawnSteps = report.SideEffectPlan.OrderedSteps
				.Where(step => step.Kind == VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc)
				.ToArray();
			Assert.Equal([831499, 831500], spawnSteps.Select(step => step.NpcId).ToArray());
			Assert.Equal(["supplied-peace", "static-peace"], spawnSteps.Select(step => step.Spawn!.Anchor).ToArray());
			Assert.All(spawnSteps, step => Assert.Equal(VortexStateType.Peace, step.VortexState));
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_PreparedRuntimeStaticRequestConsumesCollectorMetadataWithoutExtraSelection()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-prepared-request-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinatorSelector = new CountingPeaceSpawnSelector();
			var collector = new VortexStopInvasionRuntimeSnapshotCollectorService();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService(),
				coordinatorSelector);
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var kisk = new PlayerKiskRuntimeState(7101, invader.ObjectId, 831200);
			var spawnedNpc = new WorldNpc(
				ObjectId: 7201,
				TemplateId: 831300,
				Template: new NpcTemplateSummary(831300, "Vortex spawned", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC"),
				Position: location.StartPoint);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Peace, 831500, "peace-a"),
				]);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			var preparedRequest = collector.PrepareWithStaticPeaceSpawns(
				location.Id,
				snapshot,
				table,
				players: [invader],
				invaderKisks: [kisk],
				spawnedNpcs: [spawnedNpc],
				invaderAlliances: new Dictionary<int, VortexKickPlayerAllianceSnapshot>
				{
					[invader.ObjectId] = VortexKickPlayerAllianceSnapshot.MemberDisbandedAfterRemoval,
				});

			var report = coordinator.StopInvasionWithPreparedRequest(location.Id, preparedRequest);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.Equal(0, coordinatorSelector.CallCount);
			Assert.Empty(coordinatorSelector.LocationIds);
			Assert.Equal(1, report.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.KillInvaderKisk,
					VortexStopInvasionSideEffectStepKind.KickOnlineInvader,
					VortexStopInvasionSideEffectStepKind.DespawnVortexNpc,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			var spawnStep = Assert.Single(report.SideEffectPlan.OrderedSteps, step => step.Kind == VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc);
			Assert.Equal(831500, spawnStep.NpcId);
			Assert.Equal("peace-a", spawnStep.Spawn!.Anchor);
			var kickRemoval = Assert.Single(report.OrderedKickRemovalPlans);
			Assert.Equal(VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport, kickRemoval.Status);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderAllianceKickMessageId, kickRemoval.AllianceKickMessageId);
			Assert.Equal(VortexKickPlayerRemovalPlanService.InvaderDirectPortalOutMessageId, kickRemoval.DirectPortalOutMessageId);
			Assert.True(kickRemoval.WouldClearAllianceReference);
			AssertPassedSyncPlan(kickRemoval.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.False(kickRemoval.ShouldSendLivePacket);
			Assert.False(kickRemoval.ShouldTeleportLivePlayer);
			Assert.False(kickRemoval.ShouldMutateLiveParticipants);
			Assert.False(kickRemoval.ShouldMutateLivePassedPlayers);
			Assert.False(kickRemoval.ShouldSyncLivePassedPlayers);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_PreparedRequestMissingOrRepeatedStopPreservesNoDispatchGuard()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-prepared-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());
			var preparedRequest = new VortexStopInvasionSnapshotRequest(
				PeaceSpawns:
				[
					VortexStopPeaceSpawnSnapshot.FromVortexSpawn(
						CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Peace, 831500, "peace-a")),
				]);

			var missing = coordinator.StopInvasionWithPreparedRequest(location.Id, preparedRequest);
			runtime.StartInvasion(location);
			var stopped = coordinator.StopInvasionWithPreparedRequest(location.Id, preparedRequest);
			var repeated = coordinator.StopInvasionWithPreparedRequest(location.Id, preparedRequest);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.MissingInvasion, missing.Status);
			Assert.False(missing.Stopped);
			Assert.False(missing.HasSideEffectPlan);
			Assert.False(missing.ShouldExecuteLiveSideEffects);
			Assert.Empty(missing.SideEffectPlan.OrderedSteps);
			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, stopped.Status);
			Assert.True(stopped.Stopped);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				stopped.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Equal(VortexStopInvasionCoordinatorStatus.MissingInvasion, repeated.Status);
			Assert.False(repeated.Stopped);
			Assert.False(repeated.HasSideEffectPlan);
			Assert.Empty(repeated.SideEffectPlan.OrderedSteps);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_StaticPeaceSpawnTableOnlyEnrichmentFeedsPlannerWithoutLiveExecution()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-static-only-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var peaceSpawnSelector = new CountingPeaceSpawnSelector();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService(),
				peaceSpawnSelector);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Invasion, 831600, "invasion-a"),
					CreateVortexSpawn(location.Id, 1, 0, VortexStateType.Peace, 831500, "static-peace"),
				]);
			runtime.StartInvasion(location, CreateVortexPortal(location));

			var report = coordinator.StopInvasion(location.Id, table);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			Assert.Equal(1, report.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				report.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			var spawnStep = Assert.Single(report.SideEffectPlan.OrderedSteps, step => step.Kind == VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc);
			Assert.Equal(831500, spawnStep.NpcId);
			Assert.Equal("static-peace", spawnStep.Spawn!.Anchor);
			Assert.Equal(VortexStateType.Peace, spawnStep.VortexState);
			Assert.Null(runtime.GetSnapshot(location.Id));
			Assert.Equal(1, peaceSpawnSelector.CallCount);
			Assert.Equal([location.Id], peaceSpawnSelector.LocationIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_StaticPeaceSpawnTableOnlyMissingOrRepeatedStopKeepsNoDispatchGuard()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-static-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var peaceSpawnSelector = new CountingPeaceSpawnSelector();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService(),
				peaceSpawnSelector);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Peace, 831500, "static-peace"),
				]);

			var missing = coordinator.StopInvasion(location.Id, table);
			runtime.StartInvasion(location);
			var stopped = coordinator.StopInvasion(location.Id, table);
			var repeated = coordinator.StopInvasion(location.Id, table);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.MissingInvasion, missing.Status);
			Assert.False(missing.Stopped);
			Assert.False(missing.HasSideEffectPlan);
			Assert.False(missing.ShouldExecuteLiveSideEffects);
			Assert.Empty(missing.SideEffectPlan.OrderedSteps);
			Assert.Equal(0, missing.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, stopped.Status);
			Assert.True(stopped.Stopped);
			Assert.Equal(1, stopped.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
					VortexStopInvasionSideEffectStepKind.SpawnPeaceNpc,
				],
				stopped.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Equal(VortexStopInvasionCoordinatorStatus.MissingInvasion, repeated.Status);
			Assert.False(repeated.Stopped);
			Assert.False(repeated.HasSideEffectPlan);
			Assert.Empty(repeated.SideEffectPlan.OrderedSteps);
			Assert.Equal(0, repeated.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(1, peaceSpawnSelector.CallCount);
			Assert.Equal([location.Id], peaceSpawnSelector.LocationIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_FinishedStaticPeaceSpawnStopSkipsSelectorAndSideEffectPlanning()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-finished-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var peaceSpawnSelector = new CountingPeaceSpawnSelector();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService(),
				peaceSpawnSelector);
			var table = new NpcVortexSpawnTable(
				[
					CreateVortexSpawn(location.Id, 0, 0, VortexStateType.Peace, 831500, "static-peace"),
				]);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.MarkInvasionFinished(location.Id));

			var report = coordinator.StopInvasion(location.Id, table);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.FinishedInvasion, report.Status);
			Assert.False(report.Stopped);
			Assert.False(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.Equal(VortexStopInvasionStatus.FinishedInvasion, report.StopResult.Status);
			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.FinishedInvasion, report.SideEffectPlan.Status);
			Assert.Empty(report.SideEffectPlan.OrderedSteps);
			Assert.Equal(0, report.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(0, peaceSpawnSelector.CallCount);
			Assert.Empty(peaceSpawnSelector.LocationIds);
			Assert.Null(runtime.GetSnapshot(location.Id));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task StopCoordinator_MissingOrRepeatedStopReturnsNoDispatchGuardReport()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-stop-coordinator-guard-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var coordinator = new VortexStopInvasionCoordinatorService(
				runtime,
				new VortexStopInvasionSideEffectPlanService());

			var missing = coordinator.StopInvasion(location.Id);
			runtime.StartInvasion(location);
			var stopped = coordinator.StopInvasion(location.Id);
			var repeated = coordinator.StopInvasion(location.Id);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.MissingInvasion, missing.Status);
			Assert.False(missing.Stopped);
			Assert.False(missing.HasSideEffectPlan);
			Assert.False(missing.ShouldExecuteLiveSideEffects);
			Assert.Empty(missing.SideEffectPlan.OrderedSteps);
			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, stopped.Status);
			Assert.True(stopped.Stopped);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
				],
				stopped.SideEffectPlan.OrderedSteps.Select(step => step.Kind).ToArray());
			Assert.Equal(VortexStopInvasionCoordinatorStatus.MissingInvasion, repeated.Status);
			Assert.False(repeated.Stopped);
			Assert.False(repeated.HasSideEffectPlan);
			Assert.Empty(repeated.SideEffectPlan.OrderedSteps);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveInvaderPlayer_IncludesActivePortalMetadataForRiftEntryUpdatePipeline()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-active-portal-removal-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location, portal);
			Assert.True(runtime.AddInvader(location.Id, invader));

			var removal = runtime.RemoveInvaderPlayer(invader);

			Assert.True(removal.Removed);
			Assert.True(removal.HasActivePortal);
			Assert.Same(portal, removal.ActivePortal);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveDefenderPlayer_IncludesActivePortalMetadataForRiftEntryUpdatePipeline()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-active-portal-defender-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var portal = CreateVortexPortal(location);
			var runtime = new VortexInvasionRuntime();
			var defender = CreatePlayer(1004, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location, portal);
			Assert.True(runtime.AddDefender(location.Id, defender));

			var removal = runtime.RemoveDefenderPlayer(defender);

			Assert.True(removal.Removed);
			Assert.True(removal.HasActivePortal);
			Assert.Same(portal, removal.ActivePortal);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveInvaderPlayer_RemovesActiveInvaderAndPassedPortalStateLikeJava()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-invasion-runtime-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location);

			Assert.True(runtime.AddInvader(location.Id, invader));
			Assert.True(runtime.IsInvaderPlayer(invader));

			var removal = runtime.RemoveInvaderPlayer(invader);

			Assert.True(removal.Removed);
			Assert.Equal(1002, removal.PlayerObjectId);
			Assert.Equal(location.Id, removal.LocationId);
			Assert.True(removal.RemovedPassedPlayer);
			Assert.False(removal.WasOnline);
			Assert.True(removal.WasInInvasionWorld);
			Assert.Empty(removal.SystemMessages ?? []);
			Assert.Null(removal.TeleportResult);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.False(runtime.IsInvaderPlayer(invader));
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Empty(snapshot.InvaderObjectIds);
			Assert.Empty(snapshot.DefenderObjectIds);
			Assert.Empty(snapshot.PassedPlayerObjectIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveInvaderPlayer_ForOnlineInvaderInInvasionWorld_SendsKickAndPortalOutThenTeleportsHomeLikeJava()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-online-kick-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			runtime.StartInvasion(location);
			Assert.True(runtime.AddInvader(location.Id, invader));

			var removal = runtime.RemoveInvaderPlayer(invader);

			Assert.True(removal.Removed);
			Assert.True(removal.WasOnline);
			Assert.True(removal.WasInInvasionWorld);
			Assert.Equal([1401452, 1401474], (removal.SystemMessages ?? []).Select(message => message.MessageId).ToArray());
			var teleport = Assert.IsType<PlayerTeleportResult>(removal.TeleportResult);
			Assert.Equal(new WorldPosition(location.InvasionWorldId, 1, 2, 3, 0), teleport.PreviousPosition);
			Assert.Equal(location.HomePoint, teleport.Destination);
			Assert.Equal(location.HomePoint, invader.Position);
			Assert.Equal(location.HomePoint.X, invader.Movement.TargetX);
			Assert.Equal(location.HomePoint.Y, invader.Movement.TargetY);
			Assert.Equal(location.HomePoint.Z, invader.Movement.TargetZ);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveInvaderPlayer_ForOnlineInvaderOutsideInvasionWorld_SendsKickWithoutPortalOutTeleportLikeJava()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-online-kick-outside-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: true, location.HomePoint.WorldId);
			var originalPosition = invader.Position;
			runtime.StartInvasion(location);
			Assert.True(runtime.AddInvader(location.Id, invader));

			var removal = runtime.RemoveInvaderPlayer(invader);

			Assert.True(removal.Removed);
			Assert.True(removal.WasOnline);
			Assert.False(removal.WasInInvasionWorld);
			Assert.Equal([1401452], (removal.SystemMessages ?? []).Select(message => message.MessageId).ToArray());
			Assert.Null(removal.TeleportResult);
			Assert.Equal(originalPosition, invader.Position);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveDefenderPlayer_ForOnlineDefender_SendsDefenderKickWithoutTeleportLikeJava()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-defender-kick-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var defender = CreatePlayer(1004, isOnline: true, location.InvasionWorldId);
			var originalPosition = defender.Position;
			runtime.StartInvasion(location);
			Assert.True(runtime.AddDefender(location.Id, defender));
			Assert.True(runtime.IsDefenderPlayer(defender));

			var removal = runtime.RemoveDefenderPlayer(defender);

			Assert.True(removal.Removed);
			Assert.Equal(1004, removal.PlayerObjectId);
			Assert.Equal(location.Id, removal.LocationId);
			Assert.False(removal.RemovedPassedPlayer);
			Assert.True(removal.WasOnline);
			Assert.Equal([1401476], (removal.SystemMessages ?? []).Select(message => message.MessageId).ToArray());
			Assert.Equal(originalPosition, defender.Position);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.False(runtime.IsDefenderPlayer(defender));
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Empty(snapshot.DefenderObjectIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveDefenderPlayer_ForOfflineDefender_RemovesDefenderWithoutMessagesLikeJavaOnlineGate()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-defender-offline-kick-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var defender = CreatePlayer(1004, isOnline: false, location.InvasionWorldId);
			runtime.StartInvasion(location);
			Assert.True(runtime.AddDefender(location.Id, defender));

			var removal = runtime.RemoveDefenderPlayer(defender);

			Assert.True(removal.Removed);
			Assert.False(removal.WasOnline);
			Assert.Empty(removal.SystemMessages ?? []);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 0);
			Assert.False(runtime.IsDefenderPlayer(defender));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RemoveInvaderPlayer_PassedSyncPlanUsesRemainingPassedPlayerCountLikeJavaSyncPassedTrue()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-passed-sync-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var removedInvader = CreatePlayer(1002, isOnline: false, location.InvasionWorldId);
			var remainingPasser = CreatePlayer(1003, isOnline: true, location.InvasionWorldId);
			runtime.StartInvasion(location);
			Assert.True(runtime.AddInvader(location.Id, removedInvader));
			Assert.True(runtime.RecordPortalPass(location, remainingPasser));

			var removal = runtime.RemoveInvaderPlayer(removedInvader);

			Assert.True(removal.Removed);
			Assert.True(removal.RemovedPassedPlayer);
			AssertPassedSyncPlan(removal.PassedPlayerSyncPlan, location.Id, passedPlayerCount: 1);
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Equal([1003], snapshot.PassedPlayerObjectIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task AddInvaderFromPassedPortal_PromotesOnlyRecordedPortalPassLikeJavaZoneEntry()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-vortex-invader-zone-entry-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(tempPath);
			var location = Assert.IsType<VortexLocationSummary>(context.DataManager?.StaticData.VortexLocations.GetLocation(0));
			var runtime = new VortexInvasionRuntime();
			var invader = CreatePlayer(1002, isOnline: true, location.InvasionWorldId);
			var unpassed = CreatePlayer(1003, isOnline: true, location.InvasionWorldId);
			runtime.StartInvasion(location);

			var blocked = runtime.AddInvaderFromPassedPortal(location, unpassed);
			var recorded = runtime.RecordPortalPass(location, invader);
			var joined = runtime.AddInvaderFromPassedPortal(location, invader);
			var duplicate = runtime.AddInvaderFromPassedPortal(location, invader);

			Assert.False(blocked.Added);
			Assert.False(blocked.HadPassedPortal);
			Assert.True(recorded);
			Assert.True(joined.Added);
			Assert.True(joined.HadPassedPortal);
			Assert.False(joined.WasAlreadyInvader);
			Assert.False(duplicate.Added);
			Assert.True(duplicate.WasAlreadyInvader);
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Equal([1002], snapshot.InvaderObjectIds);
			Assert.Empty(snapshot.DefenderObjectIds);
			Assert.Equal([1002], snapshot.PassedPlayerObjectIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	private static void AssertPassedSyncPlan(VortexPassedPlayerSyncPlan? plan, int locationId, int passedPlayerCount)
	{
		var syncPlan = Assert.IsType<VortexPassedPlayerSyncPlan>(plan);
		Assert.Equal(locationId, syncPlan.LocationId);
		Assert.Equal(passedPlayerCount, syncPlan.PassedPlayerCount);
		Assert.True(syncPlan.UsePassedPlayerCount);
		Assert.Equal(
			"services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)",
			syncPlan.JavaSource);
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextAsync(string tempPath)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<dimensional_vortex>
					<vortex_location id="0" defends_race="ELYOS" offence_race="ASMODIANS">
						<home_point map="120080000" x="559.4" y="207.8" z="93.5" h="0" />
						<resurrection_point map="210060000" x="951.0" y="2433.0" z="107.0" h="0" />
						<start_point map="210060000" x="951.0" y="2433.0" z="107.0" h="0" />
					</vortex_location>
					<vortex_location id="1" defends_race="ASMODIANS" offence_race="ELYOS">
						<home_point map="110070000" x="452.6" y="237.1" z="127.0" h="0" />
						<resurrection_point map="220050000" x="2237.3" y="2801.5" z="73.3" h="0" />
						<start_point map="220050000" x="2242.0" y="2797.0" z="75.4" h="0" />
					</vortex_location>
				</dimensional_vortex>
			</static_data>
			""");
		File.WriteAllText(schemaFile, """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
		var dataManager = await DataManager.LoadAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = staticDataFile,
				CacheXmlFilePath = cacheFile,
				SchemaFilePath = schemaFile,
				ValidateWhenCacheChanges = false,
			});
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		return context;
	}

	private static Player CreatePlayer(int objectId, bool isOnline, int worldId, string race = "ELYOS")
	{
		return new Player
		{
			ObjectId = objectId,
			Name = "Invader",
			IsOnline = isOnline,
			Race = race,
			Position = new WorldPosition(worldId, 1, 2, 3, 0),
		};
	}

	private static RiftPortalState CreateVortexPortal(VortexLocationSummary location)
	{
		var definition = new RiftDefinition(
			1170,
			"MARCHUTAN",
			"MARCHUTAN_AM",
			"MARCHUTAN_AS",
			2,
			45,
			65,
			location.InvadersRace,
			IsVortex: true);
		var template = new NpcTemplateSummary(831143, "Vortex", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 831143,
			Template: template,
			Position: location.StartPoint,
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 831144,
			Template: template,
			Position: location.HomePoint,
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 9200);
	}

	private static NpcSpawnSummary CreatePeaceSpawn(VortexLocationSummary location)
	{
		return new NpcSpawnSummary(
			location.HomePoint.WorldId,
			831500,
			location.HomePoint.X,
			location.HomePoint.Y,
			location.HomePoint.Z,
			0,
			0,
			0,
			0,
			"",
			0,
			0,
			"",
			0,
			"",
			0,
			"",
			Custom: false,
			GroupTemporarySchedule: null,
			SpotTemporarySchedule: null);
	}

	private static NpcVortexSpawnSummary CreateVortexSpawn(
		int vortexLocationId,
		int spawnGroupIndex,
		int spotIndex,
		VortexStateType stateType,
		int npcId,
		string anchor)
	{
		return new NpcVortexSpawnSummary(
			MapId: 210060000,
			VortexLocationId: vortexLocationId,
			SpawnGroupIndex: spawnGroupIndex,
			SpotIndex: spotIndex,
			StateType: stateType,
			NpcId: npcId,
			X: 129.5f + spotIndex,
			Y: 228.25f + spotIndex,
			Z: 337.75f + spotIndex,
			Heading: (byte)(45 + spotIndex),
			RespawnSeconds: 30,
			PoolSize: 0,
			DifficultId: 0,
			Handler: "VORTEX",
			StaticId: 0,
			RandomWalkRange: 0,
			WalkerId: string.Empty,
			WalkerIndex: 0,
			Anchor: anchor,
			State: 0,
			AiName: string.Empty,
			Custom: false,
			GroupTemporarySchedule: null,
			SpotTemporarySchedule: null);
	}

	private sealed class CountingPeaceSpawnSelector : IVortexPeaceSpawnSnapshotSelector
	{
		private readonly VortexPeaceSpawnSnapshotSelectionService _selector = new();
		private readonly List<int> _locationIds = [];

		public int CallCount { get; private set; }
		public IReadOnlyList<int> LocationIds => _locationIds;

		public IReadOnlyList<VortexStopPeaceSpawnSnapshot> SelectPeaceSpawns(
			int vortexLocationId,
			NpcVortexSpawnTable vortexSpawns)
		{
			CallCount++;
			_locationIds.Add(vortexLocationId);
			return _selector.SelectPeaceSpawns(vortexLocationId, vortexSpawns);
		}
	}

	private sealed class CountingInvasionSpawnSelector : IVortexInvasionSpawnSnapshotSelector
	{
		private readonly VortexInvasionSpawnSnapshotSelectionService _selector = new();
		private readonly List<int> _locationIds = [];

		public int CallCount { get; private set; }
		public IReadOnlyList<int> LocationIds => _locationIds;

		public IReadOnlyList<VortexStartInvasionSpawnSnapshot> SelectInvasionSpawns(
			int vortexLocationId,
			NpcVortexSpawnTable vortexSpawns)
		{
			CallCount++;
			_locationIds.Add(vortexLocationId);
			return _selector.SelectInvasionSpawns(vortexLocationId, vortexSpawns);
		}
	}

	private static void DeleteTempDirectory(string tempPath)
	{
		try
		{
			Directory.Delete(tempPath, recursive: true);
		}
		catch
		{
		}
	}
}
