using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/house/House visible-object state consumed by PlayerController.see/notSee.
public sealed record WorldHouse(
	int ObjectId,
	int AddressId,
	int BuildingId,
	int OwnerObjectId,
	string OwnerName,
	int LegionId,
	string LegionName,
	byte LegionEmblemId,
	byte LegionEmblemType,
	byte LegionEmblemColorA,
	byte LegionEmblemColorR,
	byte LegionEmblemColorG,
	byte LegionEmblemColorB,
	bool IsInactive,
	byte DoorState,
	bool ShowOwnerName,
	string? SignNotice,
	WorldPosition Position)
{
	public static bool TryCreate(Player owner, PlayerHouse house, HousingTemplateTable housingTemplates, out WorldHouse? worldHouse)
	{
		// Java parity: services/HousingService.spawnHouses uses HouseAddress map/x/y/z when bringing houses into World.
		var address = housingTemplates.GetAddress(house.AddressId);
		if (address == null || address.MapId == 0)
		{
			worldHouse = null;
			return false;
		}

		worldHouse = new WorldHouse(
			house.ObjectId,
			house.AddressId,
			house.BuildingId,
			owner.ObjectId,
			owner.Name,
			owner.LegionId,
			owner.LegionName,
			owner.LegionEmblemId,
			owner.LegionEmblemType,
			owner.LegionEmblemColorA,
			owner.LegionEmblemColorR,
			owner.LegionEmblemColorG,
			owner.LegionEmblemColorB,
			house.IsInactive,
			house.DoorState,
			house.ShowOwnerName,
			house.SignNotice,
			new WorldPosition(address.MapId, address.X, address.Y, address.Z, 0));
		return true;
	}

	public static bool TryCreateUnowned(HousingAddressSummary address, Func<int> nextObjectId, out WorldHouse? worldHouse)
	{
		// Java parity: services/HousingService.spawnHouses creates new House(address, instanceId) when no DB row exists.
		if (address.DefaultBuildingId == 0 || address.MapId == 0)
		{
			worldHouse = null;
			return false;
		}

		worldHouse = new WorldHouse(
			nextObjectId(),
			address.AddressId,
			address.DefaultBuildingId,
			0,
			string.Empty,
			0,
			string.Empty,
			0,
			0,
			0,
			0,
			0,
			0,
			false,
			PlayerHouse.DoorClosed,
			true,
			null,
			new WorldPosition(address.MapId, address.X, address.Y, address.Z, 0));
		return true;
	}
}
