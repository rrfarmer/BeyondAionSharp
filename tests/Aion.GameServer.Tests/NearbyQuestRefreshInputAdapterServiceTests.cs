using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestRefreshInputAdapterServiceTests
{
	[Fact]
	public async Task CreatePlan_UsesStaticDataQuestTemplatesWithoutLiveDispatch()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		await File.WriteAllTextAsync(
			cacheFile,
			"""
			<static_data>
				<events>
					<event id="1">
						<quest id="9999" />
					</event>
				</events>
				<quests>
					<quest id="3001" minlevel_permitted="10" race_permitted="ELYOS" />
				</quests>
			</static_data>
			""");
		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());
		var worldInstance = new WorldMapInstanceRuntimeState(instanceId: 1);
		worldInstance.RegisterQuestStartIds([3001, 9999]);

		var result = NearbyQuestRefreshInputAdapterService.CreatePlan(CreatePlayer(), worldInstance, staticData);

		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.Created, result.Status);
		Assert.True(result.Applied);
		Assert.Equal(NearbyQuestRefreshPlanStatus.Ready, result.Plan.Status);
		Assert.True(result.Plan.WouldSendPacket);
		var marker = Assert.Single(result.Plan.Markers);
		Assert.Equal(3001, marker.QuestId);
		Assert.Equal(-10, marker.LevelRequirementDiff);
		Assert.True(result.Plan.RejectedQuestIds.TryGetValue(9999, out var eventQuestFailure));
		Assert.Equal(NearbyQuestStartConditionFailure.MissingTemplate, eventQuestFailure);
	}

	[Fact]
	public async Task CreatePlan_ReturnsEmptyPacketIntentWhenStaticDataHasNoWorldQuestIds()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		await File.WriteAllTextAsync(cacheFile, "<static_data><quests /></static_data>");
		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

		var result = NearbyQuestRefreshInputAdapterService.CreatePlan(
			CreatePlayer(),
			new WorldMapInstanceRuntimeState(instanceId: 2),
			staticData);

		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.Created, result.Status);
		Assert.Equal(NearbyQuestRefreshPlanStatus.NoWorldQuestIds, result.Plan.Status);
		Assert.True(result.Plan.WouldSendPacket);
		Assert.Empty(result.Plan.Markers);
	}

	[Fact]
	public void CreatePlan_GuardsMissingPlayerAndStaticData()
	{
		var noPlayer = NearbyQuestRefreshInputAdapterService.CreatePlan(
			player: null,
			new WorldMapInstanceRuntimeState(instanceId: 1),
			staticData: null);
		var noStaticData = NearbyQuestRefreshInputAdapterService.CreatePlan(
			CreatePlayer(),
			new WorldMapInstanceRuntimeState(instanceId: 1),
			staticData: null);

		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.MissingPlayer, noPlayer.Status);
		Assert.False(noPlayer.Applied);
		Assert.Equal("player", noPlayer.MissingDependency);
		Assert.False(noPlayer.Plan.WouldSendPacket);
		Assert.Equal(NearbyQuestRefreshInputAdapterStatus.MissingStaticData, noStaticData.Status);
		Assert.False(noStaticData.Applied);
		Assert.Equal("staticData", noStaticData.MissingDependency);
		Assert.False(noStaticData.Plan.WouldSendPacket);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			Level = 20,
			Race = "ELYOS",
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
				"aion-nearby-refresh-adapter-" + Guid.NewGuid().ToString("N"));
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
