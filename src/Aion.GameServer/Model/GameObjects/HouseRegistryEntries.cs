using System.Buffers.Binary;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.GameObjects;

// Java parity breadcrumbs: model/house/HouseRegistry inventory rows used by SM_HOUSE_REGISTRY.
public sealed record RegisteredHouseObjectSummary(
	int ObjectId,
	int TemplateId,
	int CooldownSeconds = 0,
	int ExpirationSeconds = 0,
	int? Color = null,
	byte TypeId = 0,
	byte[]? UsageData = null,
	float X = 0,
	float Y = 0,
	float Z = 0,
	int Heading = 0,
	int OwnerUseCount = 0,
	int VisitorUseCount = 0,
	int ColorExpires = 0,
	int NpcObjectId = 0,
	string Area = "NONE")
{
	public bool IsSpawnedByPlayer => X != 0 || Y != 0 || Z != 0;

	public int Rotation => (Heading & 0xFF) * 3;
}

public sealed record RegisteredHouseDecorationSummary(
	int ObjectId,
	int TemplateId,
	int Room = -1,
	bool IsDeleted = false)
{
	public bool IsUnused => !IsDeleted && Room == -1;
}

public sealed record HouseRegisteredItemRow(
	int ItemObjectId,
	int ItemId,
	int? ExpireTimeSeconds,
	int? Color,
	int ColorExpires,
	int OwnerUseCount,
	int VisitorUseCount,
	float X,
	float Y,
	float Z,
	int Heading,
	string Area,
	int Room);

public sealed record HouseRegistrySummary(
	IReadOnlyList<RegisteredHouseObjectSummary> Objects,
	IReadOnlyList<RegisteredHouseDecorationSummary> Decorations,
	bool HasInvalidDecorations = false)
{
	public static HouseRegistrySummary Empty { get; } =
		new(Array.Empty<RegisteredHouseObjectSummary>(), Array.Empty<RegisteredHouseDecorationSummary>());

	public IReadOnlyList<RegisteredHouseObjectSummary> NotSpawnedObjects =>
		Objects.Where(obj => !obj.IsSpawnedByPlayer).ToArray();

	public IReadOnlyList<RegisteredHouseDecorationSummary> UnusedDecorations =>
		Decorations.Where(decor => decor.IsUnused).ToArray();

	public RegisteredHouseObjectSummary? GetObject(int objectId)
	{
		return Objects.FirstOrDefault(obj => obj.ObjectId == objectId);
	}

	public HouseRegistrySummary WithObject(RegisteredHouseObjectSummary updatedObject)
	{
		// Java parity: model/house/HouseRegistry keeps objects keyed by item object ID.
		return this with
		{
			Objects = Objects
				.Select(obj => obj.ObjectId == updatedObject.ObjectId ? updatedObject : obj)
				.ToArray(),
		};
	}

	public HouseRegistrySummary WithoutObject(int objectId)
	{
		// Java parity: model/house/HouseRegistry.discardObject removes deleted objects after PlayerRegisteredItemsDAO.store.
		return this with
		{
			Objects = Objects
				.Where(obj => obj.ObjectId != objectId)
				.ToArray(),
		};
	}

	public IReadOnlyList<RegisteredHouseObjectSummary> GetNotSpawnedObjects(
		IReadOnlyDictionary<int, long> cooldowns,
		Func<long>? currentUnixTimeMilliseconds = null)
	{
		// Java parity: SM_HOUSE_REGISTRY action 1 writes Player.getHouseObjectCooldowns().remainingSeconds.
		return NotSpawnedObjects
			.Select(obj => obj.WithCooldown(cooldowns, currentUnixTimeMilliseconds))
			.ToArray();
	}

	public IReadOnlyList<PlacedHouseObjectSummary> GetSpawnedObjects(PlayerHouse house, int ownerPlayerId)
	{
		// Java parity: model/house/HouseRegistry.getSpawnedObjects feeds CM_LEVEL_READY SM_HOUSE_OBJECTS.
		return Objects
			.Where(obj => obj.IsSpawnedByPlayer)
			.Select(obj => ToPlacedObject(obj, house.AddressId, ownerPlayerId))
			.ToArray();
	}

	public IReadOnlyList<PlacedHouseObjectSummary> GetSpawnedObjects(WorldHouse house)
	{
		// Java parity: controllers/HouseController.spawnObjects exposes spawned registry objects from visible House state.
		return Objects
			.Where(obj => obj.IsSpawnedByPlayer)
			.Select(obj => ToPlacedObject(obj, house.AddressId, house.OwnerObjectId))
			.ToArray();
	}

	public IReadOnlyList<PlacedHouseObjectSummary> GetSpawnedObjects(
		WorldHouse house,
		IReadOnlyDictionary<int, long> cooldowns,
		Func<long>? currentUnixTimeMilliseconds = null)
	{
		// Java parity: SM_HOUSE_OBJECT writes cooldowns for the receiving player, not for the house owner.
		return Objects
			.Where(obj => obj.IsSpawnedByPlayer)
			.Select(obj => ToPlacedObject(obj.WithCooldown(cooldowns, currentUnixTimeMilliseconds), house.AddressId, house.OwnerObjectId))
			.ToArray();
	}

	public static HouseRegistrySummary FromRows(
		int buildingId,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		IEnumerable<HouseRegisteredItemRow> rows,
		Func<long>? currentUnixTimeSeconds = null)
	{
		// Java parity: dao/PlayerRegisteredItemsDAO.loadRegistry constructs DECOR rows separately from HouseObject rows.
		var now = currentUnixTimeSeconds?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var objects = new List<RegisteredHouseObjectSummary>();
		var decorations = new List<RegisteredHouseDecorationSummary>();
		foreach (var row in rows)
		{
			if (string.Equals(row.Area, "DECOR", StringComparison.OrdinalIgnoreCase))
			{
				var isDeleted = !housingTemplates.IsPartValidForBuilding(row.ItemId, buildingId)
					|| row.Room > 0 && !housingTemplates.IsPalaceBuilding(buildingId);
				decorations.Add(new RegisteredHouseDecorationSummary(row.ItemObjectId, row.ItemId, row.Room, isDeleted));
				continue;
			}

			var template = housingObjectTemplates.GetTemplate(row.ItemId);
			var expirationSeconds = template?.UseDays > 0 && row.ExpireTimeSeconds.HasValue
				? row.ExpireTimeSeconds.Value - (int)now
				: 0;
			objects.Add(
				new RegisteredHouseObjectSummary(
					row.ItemObjectId,
					row.ItemId,
					ExpirationSeconds: expirationSeconds,
					Color: row.Color,
					TypeId: template?.TypeId ?? 0,
					UsageData: CreateUsageData(template, row),
					X: row.X,
					Y: row.Y,
					Z: row.Z,
					Heading: row.Heading,
					OwnerUseCount: row.OwnerUseCount,
					VisitorUseCount: row.VisitorUseCount,
					ColorExpires: row.ColorExpires,
					NpcObjectId: template?.NpcId ?? 0,
					Area: template?.Area ?? row.Area));
		}

		return new HouseRegistrySummary(objects, decorations, decorations.Any(decor => decor.IsDeleted));
	}

	private static PlacedHouseObjectSummary ToPlacedObject(RegisteredHouseObjectSummary obj, int addressId, int ownerPlayerId)
	{
		return new PlacedHouseObjectSummary(
			addressId,
			ownerPlayerId,
			obj.ObjectId,
			obj.TemplateId,
			obj.X,
			obj.Y,
			obj.Z,
			obj.Rotation,
			obj.CooldownSeconds,
			obj.ExpirationSeconds,
			obj.Color,
			obj.TypeId,
			obj.NpcObjectId,
			obj.UsageData);
	}

	private static byte[]? CreateUsageData(HousingObjectTemplateSummary? template, HouseRegisteredItemRow row)
	{
		// Java parity: model/gameobjects/UseableItemObject.writeUsageData writes total use count plus action check type.
		if (template?.TypeId != 1)
			return null;

		var data = new byte[5];
		BinaryPrimitives.WriteInt32LittleEndian(data, template.UseCount == 0 ? 0 : row.OwnerUseCount + row.VisitorUseCount);
		data[4] = (byte)template.UseActionCheckType;
		return data;
	}

}

public static class RegisteredHouseObjectSummaryExtensions
{
	public static RegisteredHouseObjectSummary WithCooldown(
		this RegisteredHouseObjectSummary obj,
		IReadOnlyDictionary<int, long> cooldowns,
		Func<long>? currentUnixTimeMilliseconds = null)
	{
		return obj with
		{
			CooldownSeconds = RemainingSeconds(cooldowns, obj.ObjectId, currentUnixTimeMilliseconds),
		};
	}

	private static int RemainingSeconds(
		IReadOnlyDictionary<int, long> cooldowns,
		int objectId,
		Func<long>? currentUnixTimeMilliseconds)
	{
		if (!cooldowns.TryGetValue(objectId, out var reuseTimeMillis))
			return 0;

		var nowMillis = currentUnixTimeMilliseconds?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		return reuseTimeMillis <= nowMillis ? 0 : (int)((reuseTimeMillis - nowMillis) / 1000);
	}
}

public sealed record PlacedHouseObjectSummary(
	int AddressId,
	int OwnerPlayerId,
	int ObjectId,
	int TemplateId,
	float X,
	float Y,
	float Z,
	int Rotation,
	int CooldownSeconds = 0,
	int ExpirationSeconds = 0,
	int? Color = null,
	byte TypeId = 0,
	int NpcObjectId = 0,
	byte[]? UsageData = null);
