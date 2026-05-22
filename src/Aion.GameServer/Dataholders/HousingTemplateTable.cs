using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class HousingTemplateTable
{
	private readonly IReadOnlyDictionary<int, HousingAddressSummary> _addressesById;
	private readonly IReadOnlyDictionary<int, HousingBuildingSummary> _buildingsById;
	private readonly IReadOnlyDictionary<int, HousingPartSummary> _partsById;

	public HousingTemplateTable(
		IReadOnlyList<HousingAddressSummary> addresses,
		IReadOnlyList<HousingBuildingSummary> buildings,
		IReadOnlyList<HousingPartSummary>? parts = null)
	{
		Addresses = addresses;
		Buildings = buildings;
		Parts = parts ?? Array.Empty<HousingPartSummary>();
		_addressesById = new ReadOnlyDictionary<int, HousingAddressSummary>(
			addresses.ToDictionary(address => address.AddressId));
		_buildingsById = new ReadOnlyDictionary<int, HousingBuildingSummary>(
			buildings.ToDictionary(building => building.BuildingId));
		_partsById = new ReadOnlyDictionary<int, HousingPartSummary>(
			Parts.ToDictionary(part => part.PartId));
	}

	public IReadOnlyList<HousingAddressSummary> Addresses { get; }

	public IReadOnlyList<HousingBuildingSummary> Buildings { get; }

	public IReadOnlyList<HousingPartSummary> Parts { get; }

	public int AddressCount => Addresses.Count;

	public int BuildingCount => Buildings.Count;

	public int PartCount => Parts.Count;

	public HousingAddressSummary? GetAddress(int addressId)
	{
		return _addressesById.GetValueOrDefault(addressId);
	}

	public int GetHouseTypeId(int buildingId)
	{
		// Java parity: model/templates/housing/HouseType ids written by SM_HOUSE_BIDS.
		return _buildingsById.GetValueOrDefault(buildingId)?.HouseTypeId ?? 0;
	}

	public HousingBuildingSummary? GetBuilding(int buildingId)
	{
		return _buildingsById.GetValueOrDefault(buildingId);
	}

	public HousingPartSummary? GetPart(int partId)
	{
		return _partsById.GetValueOrDefault(partId);
	}

	public bool IsPartValidForBuilding(int partId, int buildingId)
	{
		// Java parity: model/templates/housing/HousePart.isForBuilding.
		var part = GetPart(partId);
		var building = GetBuilding(buildingId);
		return part != null
			&& building != null
			&& part.BuildingTags.Any(tag => string.Equals(tag, building.PartsMatch, StringComparison.OrdinalIgnoreCase));
	}

	public bool IsPalaceBuilding(int buildingId)
	{
		// Java parity: dao/PlayerRegisteredItemsDAO.createDecoration room guard uses HouseType.PALACE.
		return GetHouseTypeId(buildingId) == 4;
	}

	public IReadOnlyList<int> GetDefaultDecorIds(int buildingId)
	{
		// Java parity: model/house/HouseRegistry.getUsedDecorId falls back to Building.getDefaultDecorId.
		return _buildingsById.GetValueOrDefault(buildingId)?.DefaultDecorIds ?? Array.Empty<int>();
	}

	public IReadOnlyList<int> GetDefaultPartIds(int buildingId)
	{
		// Java parity: model/templates/housing/Building.getDefaultPartIds for SM_HOUSE_REGISTRY action 2.
		return _buildingsById.GetValueOrDefault(buildingId)?.DefaultPartIds ?? Array.Empty<int>();
	}

	public IReadOnlyList<HousingAddressSummary> GetCustomFieldAddresses()
	{
		// Java parity: services/HousingService.spawnHouses skips PERSONAL_INS studio addresses.
		return Addresses
			.Where(address => address.MapId != 0 && !string.Equals(address.DefaultBuildingType, "PERSONAL_INS", StringComparison.OrdinalIgnoreCase))
			.ToArray();
	}
}

public sealed record HousingAddressSummary(
	int AddressId,
	int LandId,
	int ManagerNpcId,
	int TownId = 0,
	int MinLevel = 0,
	long MaintenanceFee = 0,
	int MapId = 0,
	float X = 0,
	float Y = 0,
	float Z = 0,
	int? ExitMapId = null,
	float? ExitX = null,
	float? ExitY = null,
	float? ExitZ = null,
	int DefaultBuildingId = 0,
	string DefaultBuildingType = "");

public sealed record HousingBuildingSummary(
	int BuildingId,
	string Size,
	int HouseTypeId,
	string BuildingType = "",
	IReadOnlyList<int>? DefaultDecorIds = null,
	IReadOnlyList<int>? DefaultPartIds = null,
	string PartsMatch = "");

public sealed record HousingPartSummary(
	int PartId,
	string Type,
	IReadOnlySet<string> BuildingTags);
