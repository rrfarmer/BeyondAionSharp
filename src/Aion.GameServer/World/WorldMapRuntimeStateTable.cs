using Aion.GameServer.Dataholders;

namespace Aion.GameServer.World;

public sealed class WorldMapRuntimeStateTable
{
	public static readonly WorldMapRuntimeStateTable Empty = new(Array.Empty<WorldMapSummary>());

	private readonly IReadOnlyDictionary<int, WorldMapRuntimeState> _statesByMapId;

	public WorldMapRuntimeStateTable(IReadOnlyList<WorldMapSummary> worldMaps)
	{
		// Java parity: world/World constructor creates one WorldMap per WorldMapTemplate and stores it by map id.
		var states = worldMaps
			.GroupBy(map => map.MapId)
			.Select(group => new WorldMapRuntimeState(group.Last()))
			.ToArray();
		States = states;
		_statesByMapId = states.ToDictionary(state => state.Summary.MapId);
	}

	public IReadOnlyList<WorldMapRuntimeState> States { get; }

	public int Count => States.Count;

	public bool TryGetMap(int mapId, out WorldMapRuntimeState? state)
	{
		// Java parity: world/World.getWorldMap(int) lookup by map id.
		return _statesByMapId.TryGetValue(mapId, out state);
	}

	public WorldMapRuntimeState? GetMap(int mapId)
	{
		// Java parity: world/World.getWorldMap(int) returns null for unknown map ids.
		return _statesByMapId.GetValueOrDefault(mapId);
	}

	public bool SetWorldOption(int mapId, WorldZoneAttributes option)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return false;

		state.SetWorldOption(option);
		return true;
	}

	public bool RemoveWorldOption(int mapId, WorldZoneAttributes option)
	{
		if (!_statesByMapId.TryGetValue(mapId, out var state))
			return false;

		state.RemoveWorldOption(option);
		return true;
	}
}
