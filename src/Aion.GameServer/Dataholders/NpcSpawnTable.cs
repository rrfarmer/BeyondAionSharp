using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class NpcSpawnTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<NpcSpawnSummary>> _spawnsByMapId;

	public NpcSpawnTable(IReadOnlyList<NpcSpawnSummary> spawns)
	{
		Spawns = spawns;
		_spawnsByMapId = new ReadOnlyDictionary<int, IReadOnlyList<NpcSpawnSummary>>(
			spawns
				.GroupBy(spawn => spawn.MapId)
				.ToDictionary(
					group => group.Key,
					group => (IReadOnlyList<NpcSpawnSummary>)group.ToArray()));
	}

	public IReadOnlyList<NpcSpawnSummary> Spawns { get; }

	public int Count => Spawns.Count;

	public IReadOnlyList<NpcSpawnSummary> GetSpawnsForMap(int mapId)
	{
		return _spawnsByMapId.GetValueOrDefault(mapId) ?? Array.Empty<NpcSpawnSummary>();
	}
}

public sealed record NpcSpawnSummary(
	int MapId,
	int NpcId,
	float X,
	float Y,
	float Z,
	byte Heading,
	int RespawnSeconds,
	int PoolSize,
	string Handler,
	int StaticId,
	string WalkerId,
	int WalkerIndex,
	bool Custom);
