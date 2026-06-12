namespace Aion.GameServer.Model.GameObjects;

public enum PetSpecialFunction
{
	// Java parity: model/gameobjects/PetSpecialFunction.
	DOPING = 2,
	AUTOLOOT = 3,
	AUTOSELL = 4,
}

public static class PetSpecialFunctionResolver
{
	public static PetSpecialFunction? GetById(int id) => id switch
	{
		2 => PetSpecialFunction.DOPING,
		3 => PetSpecialFunction.AUTOLOOT,
		4 => PetSpecialFunction.AUTOSELL,
		_ => null,
	};
}
