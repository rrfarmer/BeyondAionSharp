using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public interface IHouseDoorStateService
{
	void SetHouseDoorState(int worldId, int addressId, byte doorState);

	byte? GetHouseDoorState(int worldId, int addressId);
}

public sealed class HouseDoorStateService : IHouseDoorStateService
{
	private readonly ConcurrentDictionary<HouseDoorStateKey, byte> _doorStates = new();

	public void SetHouseDoorState(int worldId, int addressId, byte doorState)
	{
		// Java parity: world/geo/GeoService.setHouseDoorState stores the active collision door state by map/address.
		_doorStates[new HouseDoorStateKey(worldId, addressId)] = doorState;
	}

	public byte? GetHouseDoorState(int worldId, int addressId)
	{
		return _doorStates.TryGetValue(new HouseDoorStateKey(worldId, addressId), out var doorState)
			? doorState
			: null;
	}

	private readonly record struct HouseDoorStateKey(int WorldId, int AddressId);
}
