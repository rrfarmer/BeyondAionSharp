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
			CreateSpawn(210010000, 203000, x: 10, y: 20, z: 30, heading: 40, staticId: 107, randomWalkRange: 7, walkerId: "path-a", walkerIndex: 3, anchor: "anchor-a"),
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
		Assert.Equal(WorldNpcState.DefaultSpawnState, npc.State);
		Assert.Equal(295, npc.RespawnSeconds);
		Assert.Equal(107, npc.StaticId);
		Assert.Equal(7, npc.RandomWalkRange);
		Assert.Equal("path-a", npc.WalkerId);
		Assert.Equal(3, npc.WalkerIndex);
		Assert.Equal("anchor-a", npc.Anchor);
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
	public void SpawnWorldNpcs_FiltersByDifficultId()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203040, x: 1, difficultId: 1),
			CreateSpawn(210010000, 203040, x: 2, difficultId: 2),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203040)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 0, serverDayOfWeek: DayOfWeek.Friday, difficultId: 1);

		Assert.Equal(new WorldNpcSpawnResult(1, 1), result);
		var npc = Assert.Single(world.GetNpcs());
		Assert.Equal(1, npc.Position.X);
	}

	[Fact]
	public void SpawnWorldNpcs_AppliesTemplateAndSpawnStatesLikeJava()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203050, x: 1),
			CreateSpawn(210010000, 203051, x: 2),
			CreateSpawn(210010000, 203052, x: 3, state: 6),
		]);
		var templates = new NpcTemplateTable(
		[
			CreateTemplate(203050),
			CreateTemplate(203051, state: 32),
			CreateTemplate(203052, state: 32),
		]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(3, 0), result);
		Assert.Equal(
			[WorldNpcState.DefaultSpawnState, 32, 6],
			world.GetNpcs().OrderBy(npc => npc.Position.X).Select(npc => npc.State).ToArray());
	}

	[Fact]
	public void SpawnWorldNpcs_AppliesTemplateAndSpawnAiNamesLikeJava()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203060, x: 1),
			CreateSpawn(210010000, 203061, x: 2, aiName: "spot_ai"),
			CreateSpawn(210010000, 203062, x: 3, aiName: "__NO_AI__"),
		]);
		var templates = new NpcTemplateTable(
		[
			CreateTemplate(203060, aiName: "template_ai"),
			CreateTemplate(203061, aiName: "template_ai"),
			CreateTemplate(203062, aiName: "template_ai"),
		]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(3, 0), result);
		Assert.Equal(
			["template_ai", "spot_ai", string.Empty],
			world.GetNpcs().OrderBy(npc => npc.Position.X).Select(npc => npc.AiName).ToArray());
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
		byte difficultId = 0,
		string handler = "",
		int state = 0,
		string aiName = "",
		int staticId = 0,
		int randomWalkRange = 0,
		string walkerId = "",
		int walkerIndex = 0,
		string anchor = "",
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
			difficultId,
			handler,
			staticId,
			randomWalkRange,
			walkerId,
			walkerIndex,
			anchor,
			state,
			aiName,
			false,
			groupTemporarySchedule,
			spotTemporarySchedule);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, int state = 0, string aiName = "")
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
			Type: "GENERAL",
			State: state,
			AiName: aiName);
	}
}
