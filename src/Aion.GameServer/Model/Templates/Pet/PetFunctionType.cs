namespace Aion.GameServer.Model.Templates.Pet;

public enum PetFunctionType
{
	// Java parity: model/templates/pet/PetFunctionType. Food and Appearance intentionally share id 1.
	Warehouse = 0,
	Food = 1,
	Doping = 2,
	Loot = 3,
	Buff = 4,
	Merchant = 5,
	None = 6,
	Appearance = 1,
	Bag = -1,
	Wing = -2,
}
