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
			Assert.False(runtime.IsInvaderPlayer(invader));
			var snapshot = Assert.IsType<VortexInvasionSnapshot>(runtime.GetSnapshot(location.Id));
			Assert.Empty(snapshot.InvaderObjectIds);
			Assert.Empty(snapshot.PassedPlayerObjectIds);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
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
