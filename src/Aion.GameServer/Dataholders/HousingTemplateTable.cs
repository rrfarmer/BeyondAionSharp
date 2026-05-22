using System.Collections.ObjectModel;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Dataholders;

public sealed class HousingTemplateTable
{
	private readonly IReadOnlyDictionary<int, HousingAddressSummary> _addressesById;
	private readonly IReadOnlyDictionary<int, HousingBuildingSummary> _buildingsById;
	private readonly IReadOnlyDictionary<int, HousingPartSummary> _partsById;
	private static readonly HousingDecorLine[] DecorLines =
	[
		new("ROOF", 1, 1, 0),
		new("OUTWALL", 2, 2, 1),
		new("FRAME", 3, 3, 2),
		new("DOOR", 4, 4, 3),
		new("GARDEN", 5, 5, 4),
		new("FENCE", 6, 6, 5),
		new("INWALL_ANY", 8, 13, 6),
		new("INFLOOR_ANY", 14, 19, 12),
		new("ADDON", 27, 27, 18),
	];

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

	public int GetDefaultDecorId(int buildingId, string partType)
	{
		var decorIds = GetDefaultDecorIds(buildingId);
		var line = DecorLines.FirstOrDefault(line => string.Equals(line.PartType, partType, StringComparison.OrdinalIgnoreCase));
		return line == null || line.PacketIndex >= decorIds.Count ? 0 : decorIds[line.PacketIndex];
	}

	public IReadOnlyList<int> GetDecorIds(int buildingId, HouseRegistrySummary? registry)
	{
		// Java parity: network/aion/serverpackets/AbstractHouseInfoPacket asks HouseRegistry.getUsedDecorId for each PartType room.
		var decorIds = GetDefaultDecorIds(buildingId).Take(19).ToArray();
		if (decorIds.Length < 19)
			decorIds = decorIds.Concat(Enumerable.Repeat(0, 19 - decorIds.Length)).ToArray();

		if (registry == null)
			return decorIds;

		foreach (var decoration in registry.Decorations)
		{
			if (decoration.IsDeleted || decoration.Room < 0)
				continue;

			var part = GetPart(decoration.TemplateId);
			if (part == null || !TryGetDecorPacketIndex(part.Type, decoration.Room, out var packetIndex))
				continue;

			decorIds[packetIndex] = decoration.TemplateId;
		}

		return decorIds;
	}

	public static bool TryGetDecorLine(int lineNumber, out string partType, out int room)
	{
		// Java parity: model/templates/housing/PartType.getForLineNr plus room offset math in CM_HOUSE_DECORATE.
		foreach (var line in DecorLines)
		{
			if (line.StartLineNumber > lineNumber || line.EndLineNumber < lineNumber)
				continue;

			partType = line.PartType;
			room = lineNumber - line.StartLineNumber;
			return true;
		}

		partType = string.Empty;
		room = 0;
		return false;
	}

	public static bool TryGetDecorPacketIndex(string partType, int room, out int packetIndex)
	{
		foreach (var line in DecorLines)
		{
			if (!string.Equals(line.PartType, partType, StringComparison.OrdinalIgnoreCase))
				continue;
			if (room < 0 || room >= line.RoomCount)
				break;

			packetIndex = line.PacketIndex + room;
			return true;
		}

		packetIndex = -1;
		return false;
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

internal sealed record HousingDecorLine(
	string PartType,
	int StartLineNumber,
	int EndLineNumber,
	int PacketIndex)
{
	public int RoomCount => EndLineNumber - StartLineNumber + 1;
}
