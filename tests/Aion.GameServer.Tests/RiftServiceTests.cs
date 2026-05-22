using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class RiftServiceTests
{
	[Fact]
	public async Task OpenAndCloseRifts_WithRiftId_TracksSpawnedLocation()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-service-id-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""<rift_location id="1170" world="110070000" />""",
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<rift_spawn id="1170" world="120080000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="KAISINEL_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var idFactory = new IDFactory();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var manager = new RiftManagerService(context, world, idFactory);
			var service = new RiftService(context, manager, world, idFactory);

			Assert.True(service.IsValidId(1170));
			Assert.True(service.IsValidId(110070000));
			Assert.False(service.IsValidId(9999));
			Assert.False(service.IsValidId(999999999));
			var open = service.OpenRifts(1170, guards: true);

			Assert.True(open.Succeeded);
			Assert.Equal(RiftServiceStatus.Opened, open.Status);
			var state = Assert.Single(open.Locations);
			Assert.True(state.Opened);
			Assert.Equal(1170, state.Location.Id);
			Assert.Equal(2, state.SpawnedCount);
			Assert.Equal(2, world.ObjectCount);
			Assert.Equal(2, manager.SpawnedRiftCount);
			Assert.True(service.IsRiftOpened(1170));
			Assert.Equal(state, service.GetActiveRift(1170));
			var spawnResult = Assert.Single(open.SpawnResults);
			Assert.True(spawnResult.Spawned);
			Assert.True(spawnResult.GuardsRequested);

			var duplicate = service.OpenRifts(1170, guards: false);
			Assert.False(duplicate.Succeeded);
			Assert.Equal(RiftServiceStatus.AlreadyOpen, duplicate.Status);
			Assert.Equal(2, world.ObjectCount);

			var close = service.CloseRifts(1170);

			Assert.True(close.Succeeded);
			Assert.Equal(RiftServiceStatus.Closed, close.Status);
			var closedState = Assert.Single(close.Locations);
			Assert.False(closedState.Opened);
			Assert.Equal(0, closedState.SpawnedCount);
			Assert.False(service.IsRiftOpened(1170));
			Assert.Null(service.GetActiveRift(1170));
			Assert.Equal(0, world.ObjectCount);
			Assert.Equal(0, manager.SpawnedRiftCount);
			Assert.Equal(1, idFactory.NextId());

			var closeAgain = service.CloseRifts(1170);
			Assert.False(closeAgain.Succeeded);
			Assert.Equal(RiftServiceStatus.NotOpen, closeAgain.Status);
		}
		finally
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

	[Fact]
	public async Task OpenRifts_CreatesJavaShapedPortalState()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-service-portal-state-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<rift_location id="1170" world="110070000" />
				<rift_location id="2176" world="210070000" has_spawns="true" />
				""",
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<rift_spawn id="1170" world="120080000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="KAISINEL_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="210070000">
					<rift_spawn id="2176" world="210070000">
						<spawn npc_id="730100">
							<spot x="11" y="12" z="13" anchor="CYGNEA_GM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="220080000">
					<rift_spawn id="2176" world="220080000">
						<spawn npc_id="730101">
							<spot x="15" y="16" z="17" anchor="ENSHAR_GS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var idFactory = new IDFactory();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var manager = new RiftManagerService(context, world, idFactory);
			var options = new GameServerOptions
			{
				Custom = new GameServerCustomOptions
				{
					RiftDuration = 2,
					VortexDuration = 3,
				},
			};
			var now = DateTimeOffset.FromUnixTimeSeconds(1000);
			var service = new RiftService(context, manager, world, idFactory, options, () => now);

			var vortexOpen = service.OpenRifts(1170, guards: false);
			var volatileOpen = service.OpenRifts(2176, guards: true);

			var vortexPortal = Assert.Single(vortexOpen.Locations).Portal;
			Assert.NotNull(vortexPortal);
			Assert.Equal("KAISINEL_AM", vortexPortal.MasterNpc.Anchor);
			Assert.Equal("KAISINEL_AS", vortexPortal.SlaveNpc.Anchor);
			Assert.Equal(24, vortexPortal.MaxEntries);
			Assert.Equal(45, vortexPortal.MinLevel);
			Assert.Equal(65, vortexPortal.MaxLevel);
			Assert.Equal("ASMODIANS", vortexPortal.DestinationRace);
			Assert.True(vortexPortal.IsVortex);
			Assert.False(vortexPortal.IsVolatile);
			Assert.False(vortexPortal.IsInvasion);
			Assert.Equal(1000 + 3 * 3600, vortexPortal.DespawnTimeUnixSeconds);
			Assert.Equal(3 * 3600, vortexPortal.GetRemainTime(now));

			var volatilePortal = Assert.Single(volatileOpen.Locations).Portal;
			Assert.NotNull(volatilePortal);
			Assert.Equal("CYGNEA_GM", volatilePortal.MasterNpc.Anchor);
			Assert.Equal("ENSHAR_GS", volatilePortal.SlaveNpc.Anchor);
			Assert.Equal(144, volatilePortal.MaxEntries);
			Assert.Equal(60, volatilePortal.MinLevel);
			Assert.Equal(65, volatilePortal.MaxLevel);
			Assert.False(volatilePortal.IsVortex);
			Assert.True(volatilePortal.IsVolatile);
			Assert.False(volatilePortal.IsInvasion);
			Assert.Equal(1000 + 2 * 3600, volatilePortal.DespawnTimeUnixSeconds);
			volatilePortal.SyncPassed(usePassedPlayerCount: false);
			Assert.Equal(1, volatilePortal.UsedEntries);
			volatilePortal.SyncPassed(usePassedPlayerCount: false);
			Assert.Equal(2, volatilePortal.UsedEntries);
			volatilePortal.SyncPassed(usePassedPlayerCount: true, passedPlayerCount: 7);
			Assert.Equal(7, volatilePortal.UsedEntries);
		}
		finally
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

	[Fact]
	public async Task OpenAndCloseRifts_WithWorldId_UsesLocationWorldLookup()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-service-world-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""<rift_location id="1170" world="110070000" />""",
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<rift_spawn id="1170" world="120080000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="KAISINEL_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var idFactory = new IDFactory();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var manager = new RiftManagerService(context, world, idFactory);
			var service = new RiftService(context, manager, world, idFactory);

			var open = service.OpenRifts(110070000, guards: false);

			Assert.True(open.Succeeded);
			Assert.Equal(1170, Assert.Single(open.Locations).Location.Id);
			Assert.True(service.IsRiftOpened(1170));
			Assert.Equal(2, world.ObjectCount);

			var close = service.CloseRifts(110070000);

			Assert.True(close.Succeeded);
			Assert.Equal(1170, Assert.Single(close.Locations).Location.Id);
			Assert.Equal(0, world.ObjectCount);
			Assert.Equal(0, manager.SpawnedRiftCount);
		}
		finally
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

	[Fact]
	public async Task CloseAutoCloseableRifts_LeavesNonAutoCloseableLocationsOpen()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-service-auto-close-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<rift_location id="1170" world="110070000" />
				<rift_location id="1280" world="110070000" auto_closeable="false" />
				""",
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
						</spawn>
					</rift_spawn>
					<rift_spawn id="1280" world="110070000">
						<spawn npc_id="730100">
							<spot x="9" y="10" z="11" anchor="MARCHUTAN_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<rift_spawn id="1170" world="120080000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="KAISINEL_AS" />
						</spawn>
					</rift_spawn>
					<rift_spawn id="1280" world="120080000">
						<spawn npc_id="730101">
							<spot x="12" y="13" z="14" anchor="MARCHUTAN_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var idFactory = new IDFactory();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var manager = new RiftManagerService(context, world, idFactory);
			var service = new RiftService(context, manager, world, idFactory);

			var open = service.OpenRifts(110070000, guards: false);

			Assert.True(open.Succeeded);
			Assert.Equal(2, open.Locations.Count);
			Assert.Equal(4, world.ObjectCount);
			Assert.Equal(4, manager.SpawnedRiftCount);

			service.CloseAutoCloseableRifts(110070000);

			Assert.False(service.IsRiftOpened(1170));
			Assert.True(service.IsRiftOpened(1280));
			var remaining = service.GetActiveRift(1280);
			Assert.NotNull(remaining);
			Assert.Equal(2, remaining.SpawnedCount);
			Assert.Equal(2, world.ObjectCount);
			Assert.Equal(2, manager.SpawnedRiftCount);
		}
		finally
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

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextAsync(
		string tempPath,
		string riftLocations,
		string spawnMaps)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			$$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<rift_locations>
			{{riftLocations}}
				</rift_locations>
				<npc_templates>
					<npc_template npc_id="730100" name="master rift" name_id="730100" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="5" ai="portal" />
					<npc_template npc_id="730101" name="slave rift" name_id="730101" level="1" rank="NORMAL" rating="NORMAL" race="ASMODIANS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="6" ai="portal" />
				</npc_templates>
				<spawns>
			{{spawnMaps}}
				</spawns>
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
}
