using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestDelayedRefreshExecutionReportServiceTests
{
	[Fact]
	public async Task CreateReport_ComposesPerPlayerNearbyRefreshResultsWithoutSending()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		var schedulePlan = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001, 3002]);
		var elyos = CreatePlayer(1001, "ELYOS");
		var asmodian = CreatePlayer(2002, "ASMODIANS");

		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReport(
			schedulePlan,
			instance,
			[elyos, asmodian],
			staticData);

		Assert.Equal(NearbyQuestDelayedRefreshExecutionStatus.Completed, report.Status);
		Assert.True(report.ClearedPendingRefresh);
		Assert.False(instance.HasPendingNearbyQuestRefresh);
		Assert.True(report.WouldInvokePlayerRefresh);
		Assert.Equal(2, report.PlayerReports.Count);
		var elyosReport = Assert.Single(report.PlayerReports, player => player.PlayerObjectId == 1001);
		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.Created, elyosReport.RefreshResult.Status);
		Assert.Equal(NearbyQuestRefreshPlanStatus.Ready, elyosReport.RefreshResult.Plan.Status);
		Assert.Equal([3001], elyosReport.RefreshResult.Plan.Markers.Select(marker => marker.QuestId));
		Assert.Equal(NearbyQuestStartConditionFailure.Race, elyosReport.RefreshResult.Plan.RejectedQuestIds[3002]);
		var asmodianReport = Assert.Single(report.PlayerReports, player => player.PlayerObjectId == 2002);
		Assert.Equal(NearbyQuestRefreshPlanStatus.Ready, asmodianReport.RefreshResult.Plan.Status);
		Assert.Equal([3002], asmodianReport.RefreshResult.Plan.Markers.Select(marker => marker.QuestId));
		Assert.Equal(NearbyQuestStartConditionFailure.Race, asmodianReport.RefreshResult.Plan.RejectedQuestIds[3001]);
	}

	[Fact]
	public async Task CreateReportFromMapRegions_ComposesPerPlayerMapRegionResultsWithoutSending()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var schedulePlan = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001, 3002]);
		var elyos = CreatePlayer(1001, "ELYOS");
		elyos.Position = new WorldPosition(210010000, 100, 200, 300, 45, InstanceId: 7);
		var asmodian = CreatePlayer(2002, "ASMODIANS");
		asmodian.Position = new WorldPosition(220010000, 400, 500, 600, 90, InstanceId: 7);

		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReportFromMapRegions(
			schedulePlan,
			instance,
			[
				new NearbyQuestDelayedRefreshPlayerInput(
					elyos,
					new NearbyQuestMapRegionSnapshot(elyos.Position, instance)),
				new NearbyQuestDelayedRefreshPlayerInput(
					asmodian,
					new NearbyQuestMapRegionSnapshot(asmodian.Position, instance)),
			],
			staticData);

		Assert.Equal(NearbyQuestDelayedRefreshExecutionStatus.Completed, report.Status);
		Assert.True(report.ClearedPendingRefresh);
		Assert.False(instance.HasPendingNearbyQuestRefresh);
		var elyosReport = Assert.Single(report.PlayerReports, player => player.PlayerObjectId == 1001);
		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.Created, elyosReport.RefreshResult.Status);
		Assert.Equal(elyos.Position, elyosReport.RefreshResult.PlayerPosition);
		Assert.Equal(elyos.Position, elyosReport.RefreshResult.MapRegionPosition);
		Assert.Equal(7, elyosReport.RefreshResult.MapRegionParentInstanceId);
		Assert.Equal([3001], elyosReport.RefreshResult.Plan.Markers.Select(marker => marker.QuestId));
		var asmodianReport = Assert.Single(report.PlayerReports, player => player.PlayerObjectId == 2002);
		Assert.Equal(asmodian.Position, asmodianReport.RefreshResult.PlayerPosition);
		Assert.Equal([3002], asmodianReport.RefreshResult.Plan.Markers.Select(marker => marker.QuestId));
	}

	[Fact]
	public async Task CreateReportFromMapRegions_RecordsMissingMapRegionPerPlayerWithoutPacketIntent()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var schedulePlan = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001]);
		var player = CreatePlayer(1001, "ELYOS");
		player.Position = new WorldPosition(210010000, 1, 2, 3, 4, InstanceId: 7);

		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReportFromMapRegions(
			schedulePlan,
			instance,
			[new NearbyQuestDelayedRefreshPlayerInput(player, MapRegion: null)],
			staticData);

		Assert.Equal(NearbyQuestDelayedRefreshExecutionStatus.Completed, report.Status);
		Assert.True(report.ClearedPendingRefresh);
		var playerReport = Assert.Single(report.PlayerReports);
		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.MissingMapRegion, playerReport.RefreshResult.Status);
		Assert.Equal("mapRegion", playerReport.RefreshResult.MissingDependency);
		Assert.Equal(player.Position, playerReport.RefreshResult.PlayerPosition);
		Assert.False(playerReport.RefreshResult.Plan.WouldSendPacket);
		var summary = NearbyQuestDelayedRefreshExecutionReportService.CreatePacketIntentSummary(report);
		Assert.False(summary.HasPacketIntent);
		Assert.Equal(0, summary.PacketIntentCount);
	}

	[Fact]
	public async Task CreatePacketIntentSummary_AggregatesMapRegionReadyEmptyAndMissingResults()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var scheduledInstance = new WorldMapInstanceRuntimeState(instanceId: 7);
		var schedulePlan = scheduledInstance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001, 3002]);
		var readyRegion = new WorldMapInstanceRuntimeState(instanceId: 7);
		readyRegion.RegisterQuestStartIds([3001]);
		var emptyRegion = new WorldMapInstanceRuntimeState(instanceId: 7);
		var readyPlayer = CreatePlayer(1001, "ELYOS");
		readyPlayer.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 7);
		var emptyPlayer = CreatePlayer(2002, "ELYOS");
		emptyPlayer.Position = new WorldPosition(210010000, 11, 21, 31, 41, InstanceId: 7);
		var missingRegionPlayer = CreatePlayer(3003, "ELYOS");
		missingRegionPlayer.Position = new WorldPosition(210010000, 12, 22, 32, 42, InstanceId: 7);

		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReportFromMapRegions(
			schedulePlan,
			scheduledInstance,
			[
				new NearbyQuestDelayedRefreshPlayerInput(
					readyPlayer,
					new NearbyQuestMapRegionSnapshot(readyPlayer.Position, readyRegion)),
				new NearbyQuestDelayedRefreshPlayerInput(
					emptyPlayer,
					new NearbyQuestMapRegionSnapshot(emptyPlayer.Position, emptyRegion)),
				new NearbyQuestDelayedRefreshPlayerInput(missingRegionPlayer, MapRegion: null),
			],
			staticData);

		var summary = NearbyQuestDelayedRefreshExecutionReportService.CreatePacketIntentSummary(report);

		Assert.Equal(3, summary.PlayerCount);
		Assert.True(summary.HasPacketIntent);
		Assert.Equal(2, summary.PacketIntentCount);
		Assert.Equal(1, summary.ReadyPacketCount);
		Assert.Equal(1, summary.EmptyPacketIntentCount);
		Assert.Empty(summary.RejectionCounts);
		Assert.Equal(0, summary.UnsupportedDependencyCount);
		var missingReport = Assert.Single(
			report.PlayerReports,
			playerReport => playerReport.PlayerObjectId == 3003);
		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.MissingMapRegion, missingReport.RefreshResult.Status);
		Assert.False(missingReport.RefreshResult.Plan.WouldSendPacket);
	}

	[Fact]
	public async Task CreatePacketIntentSummary_AggregatesReadyEmptyRejectedAndUnsupportedCounts()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		var schedulePlan = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001, 3002, 3003, 3004]);
		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReport(
			schedulePlan,
			instance,
			[
				CreatePlayer(1001, "ELYOS"),
				CreatePlayer(2002, "ASMODIANS"),
				CreatePlayer(3003, "BALAUR"),
			],
			staticData);

		var summary = NearbyQuestDelayedRefreshExecutionReportService.CreatePacketIntentSummary(report);

		Assert.Equal(3, summary.PlayerCount);
		Assert.True(summary.HasPacketIntent);
		Assert.Equal(3, summary.PacketIntentCount);
		Assert.Equal(2, summary.ReadyPacketCount);
		Assert.Equal(1, summary.EmptyPacketIntentCount);
		Assert.Equal(4, summary.RejectionCounts[NearbyQuestStartConditionFailure.Race]);
		Assert.Equal(3, summary.RejectionCounts[NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions]);
		Assert.Equal(3, summary.UnsupportedDependencyCount);
	}

	[Fact]
	public async Task CreatePacketIntentSummary_CapturesJavaEmptyPacketIntentWhenNoWorldQuestIds()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		var schedulePlan = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001]);
		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReport(
			schedulePlan,
			instance,
			Array.Empty<Player>(),
			staticData);
		var emptyInstanceReport = NearbyQuestDelayedRefreshExecutionReport.Completed(
			schedulePlan,
			clearedPendingRefresh: true,
			[
				new NearbyQuestDelayedRefreshPlayerReport(
					1001,
					NearbyQuestRefreshInputAdapterService.CreatePlan(
						CreatePlayer(1001, "ELYOS"),
						new WorldMapInstanceRuntimeState(instanceId: 2),
						staticData))
			]);

		var noPlayersSummary = NearbyQuestDelayedRefreshExecutionReportService.CreatePacketIntentSummary(report);
		var emptyPacketSummary = NearbyQuestDelayedRefreshExecutionReportService.CreatePacketIntentSummary(emptyInstanceReport);

		Assert.Equal(0, noPlayersSummary.PlayerCount);
		Assert.False(noPlayersSummary.HasPacketIntent);
		Assert.Equal(1, emptyPacketSummary.PlayerCount);
		Assert.True(emptyPacketSummary.HasPacketIntent);
		Assert.Equal(1, emptyPacketSummary.PacketIntentCount);
		Assert.Equal(0, emptyPacketSummary.ReadyPacketCount);
		Assert.Equal(1, emptyPacketSummary.EmptyPacketIntentCount);
	}

	[Fact]
	public async Task CreateReport_ClearsPendingRefreshEvenWhenNoPlayersArePresent()
	{
		using var temp = TempDirectory.Create();
		var staticData = await LoadStaticDataAsync(temp.Path);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		var schedulePlan = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001]);

		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReport(
			schedulePlan,
			instance,
			Array.Empty<Player>(),
			staticData);

		Assert.Equal(NearbyQuestDelayedRefreshExecutionStatus.NoPlayers, report.Status);
		Assert.True(report.ClearedPendingRefresh);
		Assert.False(instance.HasPendingNearbyQuestRefresh);
		Assert.False(report.WouldInvokePlayerRefresh);
		Assert.Empty(report.PlayerReports);
	}

	[Fact]
	public void CreateReport_DoesNotRunWhenSchedulePlanDidNotSchedule()
	{
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001]);
		var notScheduled = instance.RegisterQuestStartIdsAndPlanNearbyRefresh([3001]);

		var report = NearbyQuestDelayedRefreshExecutionReportService.CreateReport(
			notScheduled,
			instance,
			[CreatePlayer(1001, "ELYOS")],
			staticData: null);

		Assert.Equal(NearbyQuestDelayedRefreshExecutionStatus.NotScheduled, report.Status);
		Assert.False(report.ClearedPendingRefresh);
		Assert.True(instance.HasPendingNearbyQuestRefresh);
		Assert.Empty(report.PlayerReports);
	}

	private static async Task<StaticData> LoadStaticDataAsync(string directory)
	{
		var cacheFile = Path.Combine(directory, "static_data.xml");
		await File.WriteAllTextAsync(
			cacheFile,
			"""
			<static_data>
				<quests>
					<quest id="3001" minlevel_permitted="10" race_permitted="ELYOS" />
					<quest id="3002" minlevel_permitted="10" race_permitted="ASMODIANS" />
					<quest id="3003">
						<start_conditions>
							<future_condition>1</future_condition>
						</start_conditions>
					</quest>
				</quests>
			</static_data>
			""");
		return await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());
	}

	private static Player CreatePlayer(int objectId, string race)
	{
		return new Player
		{
			ObjectId = objectId,
			Level = 20,
			Race = race,
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
		};
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				"aion-nearby-delayed-refresh-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			if (Directory.Exists(Path))
				Directory.Delete(Path, recursive: true);
		}
	}
}
