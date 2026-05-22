using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class RiftManagerService
{
	private static readonly IReadOnlyDictionary<int, RiftDefinition> RiftDefinitions = BuildRiftDefinitions()
		.ToDictionary(definition => definition.Id);
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IDFactory _idFactory;
	private readonly Func<int, int> _nextPoolIndex;
	private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, WorldNpc>> _riftsPerWorld = new();

	public RiftManagerService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		Func<int, int>? nextPoolIndex = null)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_idFactory = idFactory;
		_nextPoolIndex = nextPoolIndex ?? (count => Random.Shared.Next(count));
	}

	public int SpawnedRiftCount => _riftsPerWorld.Values.Sum(worldRifts => worldRifts.Count);

	public IReadOnlyList<WorldNpc> GetSpawnedRifts(int worldId)
	{
		// Java parity: services/rift/RiftManager.getSpawnedRifts returns the rifts tracked for one world id.
		return _riftsPerWorld.TryGetValue(worldId, out var rifts)
			? rifts.Values.OrderBy(npc => npc.ObjectId).ToArray()
			: Array.Empty<WorldNpc>();
	}

	public bool RemoveSpawnedRift(WorldNpc rift)
	{
		// Java parity: services/rift/RiftManager.removeSpawnedRift removes the despawned rift from tracking only.
		return RemoveSpawnedRift(rift.Position.WorldId, rift.ObjectId);
	}

	public bool RemoveSpawnedRift(int worldId, int objectId)
	{
		return _riftsPerWorld.TryGetValue(worldId, out var rifts) && rifts.TryRemove(objectId, out _);
	}

	public RiftSpawnResult SpawnRift(int riftId, bool isWithGuards = false)
	{
		// Java parity: services/rift/RiftManager.spawnRift(RiftLocation, boolean) resolves RiftEnum anchors and spawns slave before master.
		if (!TryGetRiftDefinition(riftId, out var definition) || definition == null)
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.UnsupportedRift, definition: null, isWithGuards);

		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.MissingStaticData, definition, isWithGuards);
		if (!TryGetPortalSpawn(staticData, definition.MasterAnchor, out var masterSpawn))
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.MissingMasterAnchor, definition, isWithGuards);
		if (!TryGetPortalSpawn(staticData, definition.SlaveAnchor, out var slaveAnchorSpawn))
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.MissingSlaveAnchor, definition, isWithGuards);

		var slaveSpawn = SelectSlaveSpawn(slaveAnchorSpawn, staticData.NpcRiftSpawns);
		var masterTemplate = staticData.NpcTemplates.GetNpcTemplate(masterSpawn.NpcId);
		var slaveTemplate = staticData.NpcTemplates.GetNpcTemplate(slaveSpawn.NpcId);
		if (masterTemplate == null || slaveTemplate == null)
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.MissingNpcTemplate, definition, isWithGuards);

		if (!TrySpawnRiftNpc(slaveSpawn, slaveTemplate, out var slaveNpc))
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.WorldAddFailed, definition, isWithGuards);
		if (!TrySpawnRiftNpc(masterSpawn, masterTemplate, out var masterNpc))
		{
			RollBackSpawnedNpc(slaveNpc);
			return RiftSpawnResult.NotSpawned(RiftSpawnStatus.WorldAddFailed, definition, isWithGuards);
		}

		AddSpawnedRift(slaveNpc);
		AddSpawnedRift(masterNpc);
		return RiftSpawnResult.SpawnedPair(definition, isWithGuards, slaveNpc, masterNpc);
	}

	public static bool TryGetRiftDefinition(int riftId, out RiftDefinition? definition)
	{
		return RiftDefinitions.TryGetValue(riftId, out definition);
	}

	private static bool TryGetPortalSpawn(
		StaticData staticData,
		string anchor,
		out RiftSpawnPoint spawn)
	{
		if (staticData.NpcSpawns.TryGetRiftSpawnByAnchor(anchor, out var handlerSpawn) && handlerSpawn != null)
		{
			spawn = RiftSpawnPoint.FromNpcSpawn(handlerSpawn);
			return true;
		}

		if (staticData.NpcRiftSpawns.TryGetSpawnByAnchor(anchor, out var riftSpawn) && riftSpawn != null)
		{
			spawn = RiftSpawnPoint.FromNpcRiftSpawn(riftSpawn);
			return true;
		}

		spawn = default;
		return false;
	}

	private RiftSpawnPoint SelectSlaveSpawn(
		RiftSpawnPoint anchorSpawn,
		NpcRiftSpawnTable riftSpawns)
	{
		// Java parity: services/rift/RiftManager.spawnRift calls SpawnTemplate.changeTemplate(instance) for pooled slave templates.
		if (anchorSpawn.PoolSize <= 0 || !anchorSpawn.RiftId.HasValue || !anchorSpawn.SpawnGroupIndex.HasValue)
			return anchorSpawn;

		var pool = riftSpawns.Spawns
			.Where(
				spawn => spawn.MapId == anchorSpawn.MapId
					&& spawn.RiftId == anchorSpawn.RiftId.Value
					&& spawn.SpawnGroupIndex == anchorSpawn.SpawnGroupIndex.Value)
			.OrderBy(spawn => spawn.SpotIndex)
			.ToArray();
		if (pool.Length == 0)
			return anchorSpawn;

		var index = _nextPoolIndex(pool.Length);
		if (index < 0 || index >= pool.Length)
			index = 0;
		return RiftSpawnPoint.FromNpcRiftSpawn(pool[index]);
	}

	private bool TrySpawnRiftNpc(
		RiftSpawnPoint spawn,
		NpcTemplateSummary template,
		out WorldNpc npc)
	{
		var objectId = _idFactory.NextId();
		var position = new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading);
		npc = new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			position,
			WorldNpcState.FromTemplateAndSpawn(template, spawn.State),
			WorldNpcAiName.FromTemplateAndSpawn(template, spawn.AiName),
			spawn.RespawnSeconds,
			spawn.StaticId,
			spawn.RandomWalkRange,
			spawn.WalkerId,
			spawn.WalkerIndex,
			spawn.Anchor,
			position);
		if (_world.TryAddObject(objectId, npc))
			return true;

		_idFactory.ReleaseId(objectId);
		return false;
	}

	private readonly record struct RiftSpawnPoint(
		int MapId,
		int? RiftId,
		int? SpawnGroupIndex,
		int NpcId,
		float X,
		float Y,
		float Z,
		byte Heading,
		int RespawnSeconds,
		int PoolSize,
		int StaticId,
		int RandomWalkRange,
		string WalkerId,
		int WalkerIndex,
		string Anchor,
		int State,
		string AiName)
	{
		public static RiftSpawnPoint FromNpcSpawn(NpcSpawnSummary spawn)
		{
			return new RiftSpawnPoint(
				spawn.MapId,
				RiftId: null,
				SpawnGroupIndex: null,
				spawn.NpcId,
				spawn.X,
				spawn.Y,
				spawn.Z,
				spawn.Heading,
				spawn.RespawnSeconds,
				spawn.PoolSize,
				spawn.StaticId,
				spawn.RandomWalkRange,
				spawn.WalkerId,
				spawn.WalkerIndex,
				spawn.Anchor,
				spawn.State,
				spawn.AiName);
		}

		public static RiftSpawnPoint FromNpcRiftSpawn(NpcRiftSpawnSummary spawn)
		{
			return new RiftSpawnPoint(
				spawn.MapId,
				spawn.RiftId,
				spawn.SpawnGroupIndex,
				spawn.NpcId,
				spawn.X,
				spawn.Y,
				spawn.Z,
				spawn.Heading,
				spawn.RespawnSeconds,
				spawn.PoolSize,
				spawn.StaticId,
				spawn.RandomWalkRange,
				spawn.WalkerId,
				spawn.WalkerIndex,
				spawn.Anchor,
				spawn.State,
				spawn.AiName);
		}
	}

	private void AddSpawnedRift(WorldNpc npc)
	{
		// Java parity: services/rift/RiftManager.addSpawnedRift stores every spawned rift NPC by its world id.
		_riftsPerWorld
			.GetOrAdd(npc.Position.WorldId, _ => new ConcurrentDictionary<int, WorldNpc>())
			.TryAdd(npc.ObjectId, npc);
	}

	private void RollBackSpawnedNpc(WorldNpc npc)
	{
		_world.TryRemoveObject(npc.ObjectId, out _);
		_idFactory.ReleaseId(npc.ObjectId);
	}

	private static IReadOnlyList<RiftDefinition> BuildRiftDefinitions()
	{
		// Java parity: services/rift/RiftEnum id, master/slave anchors, entry limits, destination race, and vortex flags.
		return
		[
			new(1170, "KAISINEL_AM", "KAISINEL_AM", "KAISINEL_AS", 24, 45, 65, "ASMODIANS", IsVortex: true),
			new(2120, "ELTNEN_AM", "ELTNEN_AM", "MORHEIM_AS", 12, 20, 65, "ASMODIANS"),
			new(2121, "ELTNEN_BM", "ELTNEN_BM", "MORHEIM_BS", 20, 20, 65, "ASMODIANS"),
			new(2122, "ELTNEN_CM", "ELTNEN_CM", "MORHEIM_CS", 35, 20, 65, "ASMODIANS"),
			new(2123, "ELTNEN_DM", "ELTNEN_DM", "MORHEIM_DS", 35, 20, 65, "ASMODIANS"),
			new(2124, "ELTNEN_EM", "ELTNEN_EM", "MORHEIM_ES", 45, 20, 65, "ASMODIANS"),
			new(2125, "ELTNEN_FM", "ELTNEN_FM", "MORHEIM_FS", 50, 20, 65, "ASMODIANS"),
			new(2126, "ELTNEN_GM", "ELTNEN_GM", "MORHEIM_GS", 50, 20, 65, "ASMODIANS"),
			new(2140, "HEIRON_AM", "HEIRON_AM", "BELUSLAN_AS", 24, 20, 65, "ASMODIANS"),
			new(2141, "HEIRON_BM", "HEIRON_BM", "BELUSLAN_BS", 36, 20, 65, "ASMODIANS"),
			new(2142, "HEIRON_CM", "HEIRON_CM", "BELUSLAN_CS", 48, 20, 65, "ASMODIANS"),
			new(2143, "HEIRON_DM", "HEIRON_DM", "BELUSLAN_DS", 48, 20, 65, "ASMODIANS"),
			new(2144, "HEIRON_EM", "HEIRON_EM", "BELUSLAN_ES", 60, 20, 65, "ASMODIANS"),
			new(2145, "HEIRON_FM", "HEIRON_FM", "BELUSLAN_FS", 72, 20, 65, "ASMODIANS"),
			new(2146, "HEIRON_GM", "HEIRON_GM", "BELUSLAN_GS", 72, 20, 65, "ASMODIANS"),
			new(2150, "INGGISON_AM", "INGGISON_AM", "GELKMAROS_AS", 150, 20, 65, "ASMODIANS"),
			new(2151, "INGGISON_BM", "INGGISON_BM", "GELKMAROS_BS", 150, 20, 65, "ASMODIANS"),
			new(2152, "INGGISON_CM", "INGGISON_CM", "GELKMAROS_CS", 150, 20, 65, "ASMODIANS"),
			new(2153, "INGGISON_DM", "INGGISON_DM", "GELKMAROS_DS", 150, 20, 65, "ASMODIANS"),
			new(2170, "CYGNEA_AM", "CYGNEA_AM", "ENSHAR_AS", 12, 50, 65, "ASMODIANS"),
			new(2171, "CYGNEA_BM", "CYGNEA_BM", "ENSHAR_BS", 36, 50, 65, "ASMODIANS"),
			new(2172, "CYGNEA_CM", "CYGNEA_CM", "ENSHAR_CS", 48, 55, 65, "ASMODIANS"),
			new(2173, "CYGNEA_DM", "CYGNEA_DM", "ENSHAR_DS", 48, 55, 65, "ASMODIANS"),
			new(2174, "CYGNEA_EM", "CYGNEA_EM", "ENSHAR_ES", 48, 55, 65, "ASMODIANS"),
			new(2175, "CYGNEA_FM", "CYGNEA_FM", "ENSHAR_FS", 48, 55, 65, "ASMODIANS"),
			new(2176, "CYGNEA_GM", "CYGNEA_GM", "ENSHAR_GS", 144, 60, 65, "ASMODIANS", CanBeVolatile: true),
			new(2177, "CYGNEA_HM", "CYGNEA_HM", "ENSHAR_HS", 144, 60, 65, "ASMODIANS", CanBeVolatile: true),
			new(2178, "CYGNEA_IM", "CYGNEA_IM", "ENSHAR_IS", 144, 60, 65, "ASMODIANS", CanBeVolatile: true),
			new(2189, "CYGNEA_VIL1M", "CYGNEA_VIL1M", "ENSHAR_VIL1S", 72, 55, 65, "ASMODIANS", IsInvasionRift: true),
			new(2190, "CYGNEA_VIL2M", "CYGNEA_VIL2M", "ENSHAR_VIL2S", 72, 55, 65, "ASMODIANS", IsInvasionRift: true),
			new(2191, "CYGNEA_VIL3M", "CYGNEA_VIL3M", "ENSHAR_VIL3S", 72, 55, 65, "ASMODIANS", IsInvasionRift: true),
			new(1280, "MARCHUTAN_AM", "MARCHUTAN_AM", "MARCHUTAN_AS", 24, 45, 65, "ELYOS", IsVortex: true),
			new(2220, "MORHEIM_AM", "MORHEIM_AM", "ELTNEN_AS", 12, 20, 65, "ELYOS"),
			new(2221, "MORHEIM_BM", "MORHEIM_BM", "ELTNEN_BS", 20, 20, 65, "ELYOS"),
			new(2222, "MORHEIM_CM", "MORHEIM_CM", "ELTNEN_CS", 35, 20, 65, "ELYOS"),
			new(2223, "MORHEIM_DM", "MORHEIM_DM", "ELTNEN_DS", 35, 20, 65, "ELYOS"),
			new(2224, "MORHEIM_EM", "MORHEIM_EM", "ELTNEN_ES", 45, 20, 65, "ELYOS"),
			new(2225, "MORHEIM_FM", "MORHEIM_FM", "ELTNEN_FS", 50, 20, 65, "ELYOS"),
			new(2226, "MORHEIM_GM", "MORHEIM_GM", "ELTNEN_GS", 50, 20, 65, "ELYOS"),
			new(2240, "BELUSLAN_AM", "BELUSLAN_AM", "HEIRON_AS", 24, 20, 65, "ELYOS"),
			new(2241, "BELUSLAN_BM", "BELUSLAN_BM", "HEIRON_BS", 36, 20, 65, "ELYOS"),
			new(2242, "BELUSLAN_CM", "BELUSLAN_CM", "HEIRON_CS", 48, 20, 65, "ELYOS"),
			new(2243, "BELUSLAN_DM", "BELUSLAN_DM", "HEIRON_DS", 48, 20, 65, "ELYOS"),
			new(2244, "BELUSLAN_EM", "BELUSLAN_EM", "HEIRON_ES", 60, 20, 65, "ELYOS"),
			new(2245, "BELUSLAN_FM", "BELUSLAN_FM", "HEIRON_FS", 72, 20, 65, "ELYOS"),
			new(2246, "BELUSLAN_GM", "BELUSLAN_GM", "HEIRON_GS", 72, 20, 65, "ELYOS"),
			new(2270, "GELKMAROS_AM", "GELKMAROS_AM", "INGGISON_AS", 150, 20, 65, "ELYOS"),
			new(2271, "GELKMAROS_BM", "GELKMAROS_BM", "INGGISON_BS", 150, 20, 65, "ELYOS"),
			new(2272, "GELKMAROS_CM", "GELKMAROS_CM", "INGGISON_CS", 150, 20, 65, "ELYOS"),
			new(2273, "GELKMAROS_DM", "GELKMAROS_DM", "INGGISON_DS", 150, 20, 65, "ELYOS"),
			new(2280, "ENSHAR_AM", "ENSHAR_AM", "CYGNEA_AS", 12, 50, 65, "ELYOS"),
			new(2281, "ENSHAR_BM", "ENSHAR_BM", "CYGNEA_BS", 36, 50, 65, "ELYOS"),
			new(2282, "ENSHAR_CM", "ENSHAR_CM", "CYGNEA_CS", 48, 55, 65, "ELYOS"),
			new(2283, "ENSHAR_DM", "ENSHAR_DM", "CYGNEA_DS", 48, 55, 65, "ELYOS"),
			new(2284, "ENSHAR_EM", "ENSHAR_EM", "CYGNEA_ES", 48, 55, 65, "ELYOS"),
			new(2285, "ENSHAR_FM", "ENSHAR_FM", "CYGNEA_FS", 48, 55, 65, "ELYOS"),
			new(2286, "ENSHAR_GM", "ENSHAR_GM", "CYGNEA_GS", 144, 60, 65, "ELYOS", CanBeVolatile: true),
			new(2287, "ENSHAR_HM", "ENSHAR_HM", "CYGNEA_HS", 144, 60, 65, "ELYOS", CanBeVolatile: true),
			new(2288, "ENSHAR_IM", "ENSHAR_IM", "CYGNEA_IS", 144, 60, 65, "ELYOS", CanBeVolatile: true),
			new(2289, "ENSHAR_VIL1M", "ENSHAR_VIL1M", "CYGNEA_VIL1S", 72, 55, 65, "ELYOS", IsInvasionRift: true),
			new(2290, "ENSHAR_VIL2M", "ENSHAR_VIL2M", "CYGNEA_VIL2S", 72, 55, 65, "ELYOS", IsInvasionRift: true),
			new(2291, "ENSHAR_VIL3M", "ENSHAR_VIL3M", "CYGNEA_VIL3S", 72, 55, 65, "ELYOS", IsInvasionRift: true),
		];
	}
}

public sealed record RiftDefinition(
	int Id,
	string Name,
	string MasterAnchor,
	string SlaveAnchor,
	int Entries,
	int MinLevel,
	int MaxLevel,
	string DestinationRace,
	bool IsVortex = false,
	bool CanBeVolatile = false,
	bool IsInvasionRift = false);

public sealed record RiftSpawnResult(
	bool Spawned,
	RiftSpawnStatus Status,
	RiftDefinition? Definition,
	bool GuardsRequested,
	IReadOnlyList<WorldNpc> SpawnedNpcs)
{
	public static RiftSpawnResult SpawnedPair(
		RiftDefinition definition,
		bool guardsRequested,
		WorldNpc slaveNpc,
		WorldNpc masterNpc)
	{
		return new RiftSpawnResult(
			Spawned: true,
			RiftSpawnStatus.Spawned,
			definition,
			guardsRequested,
			[slaveNpc, masterNpc]);
	}

	public static RiftSpawnResult NotSpawned(
		RiftSpawnStatus status,
		RiftDefinition? definition,
		bool guardsRequested)
	{
		return new RiftSpawnResult(
			Spawned: false,
			status,
			definition,
			guardsRequested,
			Array.Empty<WorldNpc>());
	}
}

public enum RiftSpawnStatus
{
	Spawned,
	MissingStaticData,
	UnsupportedRift,
	MissingMasterAnchor,
	MissingSlaveAnchor,
	MissingNpcTemplate,
	WorldAddFailed,
}
