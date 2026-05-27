namespace Aion.GameServer.Model.GameObjects;

public enum PetSpecialFunction
{
	// Java parity: model/gameobjects/PetSpecialFunction.
	Doping = 2,
	AutoLoot = 3,
	AutoSell = 4,
}

public static class PetSpecialFunctionResolver
{
	public static PetSpecialFunction? GetById(int id) => id switch
	{
		2 => PetSpecialFunction.Doping,
		3 => PetSpecialFunction.AutoLoot,
		4 => PetSpecialFunction.AutoSell,
		_ => null,
	};
}
