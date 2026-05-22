using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcSpawnServiceTests
{
	[Fact]
	public void SpawnWorldNpcs_MaterializesOnlySupportedRegularSpawns()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203000, x: 10, y: 20, z: 30, heading: 40),
			CreateSpawn(210010000, 400001),
			CreateSpawn(210010000, 203001, handler: "STATIC"),
			CreateSpawn(210010000, 203002, poolSize: 2),
			CreateSpawn(300030000, 203003),
			CreateSpawn(210010000, 299999),
			CreateSpawn(210010000, 203004, hasTemporarySchedule: true),
		]);
		var templates = new NpcTemplateTable(
		[
			CreateTemplate(203000),
			CreateTemplate(203001),
			CreateTemplate(203002),
			CreateTemplate(203003),
			CreateTemplate(203004),
		]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(2, 5), result);
		Assert.True(world.TryGetObject(1, out var gameObject));
		var npc = Assert.IsType<WorldNpc>(gameObject);
		Assert.Equal(203000, npc.TemplateId);
		Assert.Equal(new global::Aion.GameServer.World.WorldPosition(210010000, 10, 20, 30, 40), npc.Position);
		Assert.Equal(2, world.GetNpcs().Count);
		Assert.Contains(world.GetNpcs(), worldNpc => worldNpc.ObjectId == npc.ObjectId);
		Assert.Equal(2, world.GetNpcs(210010000).Count);
		Assert.Empty(world.GetNpcs(220010000));
	}

	[Fact]
	public void SpawnWorldNpcs_ActivatesOnlyPoolSizeSpotsForValidPool()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203010, x: 1, poolSize: 2),
			CreateSpawn(210010000, 203010, x: 2, poolSize: 2),
			CreateSpawn(210010000, 203010, x: 3, poolSize: 2),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203010)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(2, 0), result);
		Assert.Equal(2, world.GetNpcs().Count);
		Assert.Equal(2, world.GetNpcs().Select(npc => npc.Position.X).Distinct().Count());
	}

	[Fact]
	public void SpawnWorldNpcs_SkipsTemporaryScheduledSpawnsUntilSchedulerExists()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203020, x: 1, hasTemporarySchedule: true),
			CreateSpawn(210010000, 203020, x: 2),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203020)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(1, 1), result);
		var npc = Assert.Single(world.GetNpcs());
		Assert.Equal(2, npc.Position.X);
	}

	[Fact]
	public void SpawnWorldNpcsForMap_UsesOnlyRequestedMap()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203000),
			CreateSpawn(220010000, 203001),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203000), CreateTemplate(203001)]);

		var result = service.SpawnWorldNpcsForMap(220010000, spawns, templates);

		Assert.Equal(new WorldNpcSpawnResult(1, 0), result);
		Assert.True(world.TryGetObject(1, out var gameObject));
		var npc = Assert.IsType<WorldNpc>(gameObject);
		Assert.Equal(203001, npc.TemplateId);
		Assert.Equal(220010000, npc.Position.WorldId);
	}

	private static WorldNpcSpawnService CreateService(GameWorld world)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			NullLogger<WorldNpcSpawnService>.Instance);
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		float x = 1,
		float y = 2,
		float z = 3,
		byte heading = 0,
		int poolSize = 0,
		string handler = "",
		bool hasTemporarySchedule = false)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			x,
			y,
			z,
			heading,
			295,
			poolSize,
			handler,
			0,
			string.Empty,
			0,
			false,
			hasTemporarySchedule);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: templateId,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL");
	}
}
