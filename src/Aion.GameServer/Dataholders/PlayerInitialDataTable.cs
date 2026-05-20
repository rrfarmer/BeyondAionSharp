using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class PlayerInitialDataTable
{
	private readonly IReadOnlyDictionary<string, PlayerCreationData> _creationDataByClass;
	private readonly IReadOnlyDictionary<string, PlayerSpawnLocation> _spawnLocationsByRace;

	public PlayerInitialDataTable(
		IReadOnlyDictionary<string, PlayerCreationData> creationDataByClass,
		IReadOnlyDictionary<string, PlayerSpawnLocation> spawnLocationsByRace)
	{
		_creationDataByClass = new ReadOnlyDictionary<string, PlayerCreationData>(
			new Dictionary<string, PlayerCreationData>(creationDataByClass, StringComparer.OrdinalIgnoreCase));
		_spawnLocationsByRace = new ReadOnlyDictionary<string, PlayerSpawnLocation>(
			new Dictionary<string, PlayerSpawnLocation>(spawnLocationsByRace, StringComparer.OrdinalIgnoreCase));
	}

	public int Count => _creationDataByClass.Count;

	public PlayerCreationData? GetPlayerCreationData(string playerClass)
	{
		return _creationDataByClass.GetValueOrDefault(playerClass);
	}

	public PlayerSpawnLocation? GetSpawnLocation(string race)
	{
		return _spawnLocationsByRace.GetValueOrDefault(race);
	}
}

public sealed record PlayerCreationData(string PlayerClass, IReadOnlyList<StartingItem> Items);

public sealed record StartingItem(int ItemId, long Count);

public sealed record PlayerSpawnLocation(int MapId, float X, float Y, float Z, int Heading);
