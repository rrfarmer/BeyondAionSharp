using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
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
				[VortexStopPeaceSpawnSnapshot.FromSpawn(peaceSpawn)]);

			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.Planned, plan.Status);
			Assert.False(plan.ShouldExecuteLiveSideEffects);
			Assert.Equal(location.Id, plan.LocationId);
			Assert.Equal(1, plan.KiskKillCount);
			Assert.Equal(2, plan.OnlineInvaderKickCount);
			Assert.Equal(1, plan.DespawnNpcCount);
			Assert.Equal(1, plan.PeaceSpawnCount);
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
			var outsideKick = plan.OrderedSteps.Single(step =>
				step.Kind == VortexStopInvasionSideEffectStepKind.KickOnlineInvader
				&& step.PlayerObjectId == outsideInvader.ObjectId);
			Assert.False(outsideKick.WasInInvasionWorld);
			Assert.False(outsideKick.ShouldTeleportHome);
			Assert.Null(outsideKick.TeleportDestination);
			Assert.DoesNotContain(plan.OrderedSteps, step => step.PlayerObjectId == offlineInvader.ObjectId);
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
			PeaceSpawns: [suppliedPeaceSpawn]);
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
		Assert.DoesNotContain(enriched.PeaceSpawnSnapshots, snapshot => snapshot.Spawn.NpcId == 831600);
		Assert.DoesNotContain(enriched.PeaceSpawnSnapshots, snapshot => snapshot.Spawn.NpcId == 831700);
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
				[VortexStopPeaceSpawnSnapshot.FromSpawn(peaceSpawn)]);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.True(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
			Assert.Equal(location.Id, report.LocationId);
			Assert.Equal(location.Id, report.StopResult.LocationId);
			Assert.Equal(VortexStopInvasionStatus.Stopped, report.StopResult.Status);
			Assert.Equal(VortexStopInvasionSideEffectPlanStatus.Planned, report.SideEffectPlan.Status);
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
				PeaceSpawns: [VortexStopPeaceSpawnSnapshot.FromSpawn(peaceSpawn)]);
			runtime.StartInvasion(location, CreateVortexPortal(location));
			Assert.True(runtime.AddInvader(location.Id, invader));

			var report = coordinator.StopInvasion(location.Id, request);

			Assert.Equal(VortexStopInvasionCoordinatorStatus.Planned, report.Status);
			Assert.True(report.Stopped);
			Assert.True(report.HasSideEffectPlan);
			Assert.False(report.ShouldExecuteLiveSideEffects);
			Assert.False(report.SideEffectPlan.ShouldExecuteLiveSideEffects);
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
			var request = new VortexStopInvasionSnapshotRequest(
				PeaceSpawns: [suppliedPeaceSpawn]);
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
			Assert.Equal(2, report.SideEffectPlan.PeaceSpawnCount);
			Assert.Equal(
				[
					VortexStopInvasionSideEffectStepKind.ClearActiveVortex,
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

	private static Player CreatePlayer(int objectId, bool isOnline, int worldId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = "Invader",
			IsOnline = isOnline,
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
