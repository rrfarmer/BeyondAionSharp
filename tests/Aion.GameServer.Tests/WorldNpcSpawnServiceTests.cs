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
			CreateSpawn(210010000, 203004, groupTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "9.*.*", "10.*.*")),
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
	public void SpawnWorldNpcs_SpawnsOnlyTemporarySchedulesInCurrentWindow()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203020, x: 1, groupTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
			CreateSpawn(210010000, 203020, x: 2),
			CreateSpawn(210010000, 203021, x: 3, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
			CreateSpawn(210010000, 203021, x: 4, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "4.*.*", "5.*.*")),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203020), CreateTemplate(203021)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 2 * 60, serverDayOfWeek: DayOfWeek.Friday);

		Assert.Equal(new WorldNpcSpawnResult(3, 1), result);
		Assert.Equal([1, 2, 3], world.GetNpcs().Select(npc => npc.Position.X).OrderBy(x => x).ToArray());
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

	[Fact]
	public async Task ProcessTemporarySpawnHourChange_DespawnsThenSpawnsEligibleTemporaryGroups()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var groupSchedule = TemporarySpawnSchedule.FromAttributes(null, null, null);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203030, x: 1, groupTemporarySchedule: groupSchedule, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
			CreateSpawn(210010000, 203030, x: 2, groupTemporarySchedule: groupSchedule, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "4.*.*", "5.*.*")),
			CreateSpawn(210010000, 203031, x: 3),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203030), CreateTemplate(203031)]);

		var startup = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 2 * 60, serverDayOfWeek: DayOfWeek.Friday);
		var despawn = await service.ProcessTemporarySpawnHourChangeAsync(spawns, templates, [210010000], gameMinutes: 3 * 60, serverDayOfWeek: DayOfWeek.Friday);
		var respawn = await service.ProcessTemporarySpawnHourChangeAsync(spawns, templates, [210010000], gameMinutes: 4 * 60, serverDayOfWeek: DayOfWeek.Friday);

		Assert.Equal(new WorldNpcSpawnResult(2, 1), startup);
		Assert.Equal(new TemporarySpawnHourChangeResult(0, 1, 3), despawn);
		Assert.Equal(new TemporarySpawnHourChangeResult(1, 0, 2), respawn);
		Assert.Equal([2, 3], world.GetNpcs().Select(npc => npc.Position.X).OrderBy(x => x).ToArray());
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
		TemporarySpawnSchedule? groupTemporarySchedule = null,
		TemporarySpawnSchedule? spotTemporarySchedule = null)
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
			groupTemporarySchedule,
			spotTemporarySchedule);
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
