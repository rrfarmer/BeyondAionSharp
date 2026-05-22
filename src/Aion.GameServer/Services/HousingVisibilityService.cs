using System.Collections.Concurrent;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class HousingVisibilityService
{
	private readonly ConcurrentDictionary<int, HashSet<int>> _knownHouseAddressesByPlayer = new();
	private readonly float _visibilityDistance;

	public HousingVisibilityService(GameServerOptions options)
	{
		_visibilityDistance = options.Housing.VisibilityDistance;
	}

	public HousingVisibilityService(float visibilityDistance)
	{
		_visibilityDistance = visibilityDistance;
	}

	public HousingVisibilityDelta UpdateKnownHouses(Player player, IEnumerable<WorldHouse> houses)
	{
		// Java parity: world/knownlist/KnownList.updateKnownList for House objects with HousingConfig.VISIBILITY_DISTANCE.
		var visibleHouses = houses
			.Where(house => IsVisibleTo(player, house))
			.GroupBy(house => house.AddressId)
			.Select(group => group.First())
			.ToArray();
		var visibleAddresses = visibleHouses.Select(house => house.AddressId).ToHashSet();
		var knownAddresses = _knownHouseAddressesByPlayer.GetOrAdd(player.ObjectId, _ => []);

		lock (knownAddresses)
		{
			var appeared = visibleHouses
				.Where(house => !knownAddresses.Contains(house.AddressId))
				.ToArray();
			var disappeared = knownAddresses
				.Where(addressId => !visibleAddresses.Contains(addressId))
				.ToArray();

			foreach (var house in appeared)
				knownAddresses.Add(house.AddressId);
			foreach (var addressId in disappeared)
				knownAddresses.Remove(addressId);

			return new HousingVisibilityDelta(appeared, disappeared);
		}
	}

	public void ClearKnownHouses(int playerObjectId)
	{
		// Java parity: KnownList.clear on player removal/despawn.
		_knownHouseAddressesByPlayer.TryRemove(playerObjectId, out _);
	}

	public bool IsVisibleTo(Player player, WorldHouse house)
	{
		var playerPosition = player.Position;
		var housePosition = house.Position;
		if (playerPosition.WorldId != housePosition.WorldId)
			return false;

		var deltaX = playerPosition.X - housePosition.X;
		var deltaY = playerPosition.Y - housePosition.Y;
		var deltaZ = playerPosition.Z - housePosition.Z;
		return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= _visibilityDistance * _visibilityDistance;
	}
}

public sealed record HousingVisibilityDelta(IReadOnlyList<WorldHouse> Appeared, IReadOnlyList<int> DisappearedAddressIds);
