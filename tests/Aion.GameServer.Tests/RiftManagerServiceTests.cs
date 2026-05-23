using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class RiftManagerServiceTests
{
	[Fact]
	public async Task SpawnRift_SpawnsSlaveThenMasterAndTracksByWorld()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-manager-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" h="4" anchor="KAISINEL_AM" state="2" ai="master_ai" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<rift_spawn id="1170" world="120080000">
						<spawn npc_id="730101" respawn_time="60">
							<spot x="5" y="6" z="7" h="8" anchor="KAISINEL_AS" state="3" ai="slave_ai" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var service = new RiftManagerService(context, world, new IDFactory());

			var result = service.SpawnRift(1170, isWithGuards: true);

			Assert.True(result.Spawned);
			Assert.Equal(RiftSpawnStatus.Spawned, result.Status);
			Assert.True(result.GuardsRequested);
			Assert.NotNull(result.Definition);
			Assert.Equal("KAISINEL_AM", result.Definition.MasterAnchor);
			Assert.Equal("KAISINEL_AS", result.Definition.SlaveAnchor);
			Assert.True(result.Definition.IsVortex);
			Assert.Equal(2, result.SpawnedNpcs.Count);
			var slaveNpc = result.SpawnedNpcs[0];
			var masterNpc = result.SpawnedNpcs[1];
			Assert.Equal(1, slaveNpc.ObjectId);
			Assert.Equal(2, masterNpc.ObjectId);
			Assert.Equal(730101, slaveNpc.TemplateId);
			Assert.Equal(730100, masterNpc.TemplateId);
			Assert.Equal(120080000, slaveNpc.Position.WorldId);
			Assert.Equal(110070000, masterNpc.Position.WorldId);
			Assert.Equal(5, slaveNpc.Position.X);
			Assert.Equal(1, masterNpc.Position.X);
			Assert.Equal(3, slaveNpc.State);
			Assert.Equal(2, masterNpc.State);
			Assert.Equal("slave_ai", slaveNpc.AiName);
			Assert.Equal("master_ai", masterNpc.AiName);
			Assert.Equal("KAISINEL_AS", slaveNpc.Anchor);
			Assert.Equal("KAISINEL_AM", masterNpc.Anchor);
			Assert.Equal(slaveNpc.Position, slaveNpc.SpawnLocation);
			Assert.Equal(masterNpc.Position, masterNpc.SpawnLocation);
			Assert.Equal(2, world.ObjectCount);
			Assert.Equal(2, service.SpawnedRiftCount);
			var slaveWorldRifts = Assert.Single(service.GetSpawnedRifts(120080000));
			var masterWorldRifts = Assert.Single(service.GetSpawnedRifts(110070000));
			Assert.Equal(slaveNpc, slaveWorldRifts);
			Assert.Equal(masterNpc, masterWorldRifts);
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
	public async Task SpawnRift_UsesJavaRiftHandlerAnchors()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-manager-handler-anchors-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<spawn_map map_id="110070000">
					<spawn npc_id="730100" handler="RIFT">
						<spot x="1" y="2" z="3" h="4" anchor="KAISINEL_AM" state="2" ai="master_ai" />
					</spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<spawn npc_id="730101" handler="RIFT">
						<spot x="5" y="6" z="7" h="8" anchor="KAISINEL_AS" state="3" ai="slave_ai" />
					</spawn>
				</spawn_map>
				""");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var service = new RiftManagerService(context, world, new IDFactory());

			var result = service.SpawnRift(1170);

			Assert.True(result.Spawned);
			Assert.Equal(RiftSpawnStatus.Spawned, result.Status);
			Assert.Equal(2, result.SpawnedNpcs.Count);
			Assert.Equal("KAISINEL_AS", result.SpawnedNpcs[0].Anchor);
			Assert.Equal("KAISINEL_AM", result.SpawnedNpcs[1].Anchor);
			Assert.Equal(2, service.SpawnedRiftCount);
			Assert.Equal(2, world.ObjectCount);
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
	public async Task SpawnRift_FansOutPortalPairsAcrossWorldMapInstances()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-manager-instance-fanout-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<spawn_map map_id="210020000">
					<spawn npc_id="730100" handler="RIFT">
						<spot x="1" y="2" z="3" anchor="ELTNEN_AM" />
					</spawn>
				</spawn_map>
				<spawn_map map_id="220020000">
					<spawn npc_id="730101" handler="RIFT">
						<spot x="5" y="6" z="7" anchor="MORHEIM_AS" />
					</spawn>
				</spawn_map>
				""",
				"""
				<map id="210020000" instance="false" twin_count="3" />
				<map id="220020000" instance="false" twin_count="3" />
				""");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var service = new RiftManagerService(context, world, new IDFactory());

			var result = service.SpawnRift(2120);

			Assert.True(result.Spawned);
			Assert.Equal(6, result.SpawnedNpcs.Count);
			Assert.Equal([1, 2, 3], result.SpawnedNpcs.Where(npc => npc.Anchor == "ELTNEN_AM").Select(npc => npc.Position.InstanceId).ToArray());
			Assert.Equal([1, 2, 3], result.SpawnedNpcs.Where(npc => npc.Anchor == "MORHEIM_AS").Select(npc => npc.Position.InstanceId).ToArray());
			Assert.Equal(3, service.GetSpawnedRifts(210020000).Count);
			Assert.Equal(3, service.GetSpawnedRifts(220020000).Count);
			Assert.Equal(6, service.SpawnedRiftCount);
			Assert.Equal(6, world.ObjectCount);
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
	public async Task RemoveSpawnedRift_RemovesManagerTrackingOnly()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-remove-tracking-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
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
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var service = new RiftManagerService(context, world, new IDFactory());
			var result = service.SpawnRift(1170);
			var slaveNpc = result.SpawnedNpcs[0];
			var masterNpc = result.SpawnedNpcs[1];

			var removed = service.RemoveSpawnedRift(slaveNpc);

			Assert.True(removed);
			Assert.Empty(service.GetSpawnedRifts(slaveNpc.Position.WorldId));
			Assert.Equal(masterNpc, Assert.Single(service.GetSpawnedRifts(masterNpc.Position.WorldId)));
			Assert.Equal(1, service.SpawnedRiftCount);
			Assert.Equal(2, world.ObjectCount);
			Assert.False(service.RemoveSpawnedRift(slaveNpc));
			Assert.False(service.RemoveSpawnedRift(slaveNpc.Position.WorldId, masterNpc.ObjectId));
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
	public async Task SpawnRift_UsesPooledSlaveSpotSelectedFromSlaveGroup()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-pooled-slave-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<spawn_map map_id="210020000">
					<rift_spawn id="2120" world="210020000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="ELTNEN_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="220020000">
					<rift_spawn id="2120" world="220020000">
						<spawn npc_id="730101" pool="2">
							<spot x="10" y="11" z="12" anchor="MORHEIM_AS" />
							<spot x="20" y="21" z="22" anchor="MORHEIM_AS_POOL_B" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var poolSelectionCount = 0;
			var service = new RiftManagerService(
				context,
				world,
				new IDFactory(),
				count =>
				{
					poolSelectionCount++;
					Assert.Equal(2, count);
					return 1;
				});

			var result = service.SpawnRift(2120);

			Assert.True(result.Spawned);
			Assert.Equal(1, poolSelectionCount);
			var slaveNpc = result.SpawnedNpcs[0];
			Assert.Equal(730101, slaveNpc.TemplateId);
			Assert.Equal(220020000, slaveNpc.Position.WorldId);
			Assert.Equal(20, slaveNpc.Position.X);
			Assert.Equal(21, slaveNpc.Position.Y);
			Assert.Equal(22, slaveNpc.Position.Z);
			Assert.Equal("MORHEIM_AS_POOL_B", slaveNpc.Anchor);
			Assert.False(context.DataManager!.StaticData.NpcRiftSpawns.TryGetSpawnByAnchor("MORHEIM_AS_POOL_B", out _));
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
	public async Task SpawnRift_RequiresBothMasterAndSlaveAnchors()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-missing-anchor-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var service = new RiftManagerService(context, world, new IDFactory());

			var result = service.SpawnRift(1170);

			Assert.False(result.Spawned);
			Assert.Equal(RiftSpawnStatus.MissingSlaveAnchor, result.Status);
			Assert.NotNull(result.Definition);
			Assert.Empty(result.SpawnedNpcs);
			Assert.Equal(0, service.SpawnedRiftCount);
			Assert.Equal(0, world.ObjectCount);
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
	public async Task SpawnRift_ClearsCreaturePvpZoneCountersWhenRollingBackPartialSpawn()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-rollback-pvp-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextAsync(
				tempPath,
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
				""",
				zones:
				"""
				<zone name="PVP_RIFT_ROLLBACK_120080000" zone_type="PVP" area_type="POLYGON" mapid="120080000">
					<points bottom="0" top="10">
						<point x="4" y="5" />
						<point x="6" y="5" />
						<point x="6" y="7" />
						<point x="4" y="7" />
					</points>
				</zone>
				""");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			Assert.True(world.TryAddObject(2, new object()));
			var zoneCounterService = new CreaturePvpZoneCounterService();
			var service = new RiftManagerService(
				context,
				world,
				new IDFactory(),
				creaturePvpZoneCounterService: zoneCounterService);

			var result = service.SpawnRift(1170);

			Assert.False(result.Spawned);
			Assert.Equal(RiftSpawnStatus.WorldAddFailed, result.Status);
			Assert.False(world.TryGetObject(1, out _));
			Assert.True(world.TryGetObject(2, out _));
			Assert.Equal(1, world.ObjectCount);
			Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(1));
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
		string spawnMaps,
		string worldMaps = "",
		string zones = "")
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		var worldMapsSection = string.IsNullOrWhiteSpace(worldMaps)
			? string.Empty
			: $$"""
				<world_maps>
			{{worldMaps}}
				</world_maps>
			""";
		var zonesSection = string.IsNullOrWhiteSpace(zones)
			? string.Empty
			: $$"""
				<zones>
			{{zones}}
				</zones>
			""";
		File.WriteAllText(
			staticDataFile,
			$$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
			{{worldMapsSection}}
			{{zonesSection}}
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
