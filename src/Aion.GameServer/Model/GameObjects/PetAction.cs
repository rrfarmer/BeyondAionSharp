namespace Aion.GameServer.Model.GameObjects;

public enum PetAction
{
	// Java parity: model/gameobjects/PetAction.
	LoadPets = 0,
	Adopt = 1,
	Surrender = 2,
	Spawn = 3,
	Dismiss = 4,
	TalkWithMerchant = 6,
	TalkWithMinder = 7,
	Food = 9,
	Rename = 10,
	Mood = 12,
	SpecialFunction = 13,
	ExtendExpiration = 15,
	HAdopt = 16,
	HAbandon = 17,
	Unknown = 255,
}

public static class PetActionResolver
{
	public static PetAction GetActionById(int actionId) => actionId switch
	{
		0 => PetAction.LoadPets,
		1 => PetAction.Adopt,
		2 => PetAction.Surrender,
		3 => PetAction.Spawn,
		4 => PetAction.Dismiss,
		6 => PetAction.TalkWithMerchant,
		7 => PetAction.TalkWithMinder,
		9 => PetAction.Food,
		10 => PetAction.Rename,
		12 => PetAction.Mood,
		13 => PetAction.SpecialFunction,
		15 => PetAction.ExtendExpiration,
		16 => PetAction.HAdopt,
		17 => PetAction.HAbandon,
		_ => PetAction.Unknown,
	};
}
