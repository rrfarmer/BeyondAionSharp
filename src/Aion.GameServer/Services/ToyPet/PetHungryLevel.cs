namespace Aion.GameServer.Services.ToyPet;

public enum PetHungryLevel : byte
{
	// Java parity: services/toypet/PetHungryLevel enum ids.
	HUNGRY = 0,
	CONTENT = 1,
	SEMIFULL = 2,
	FULL = 3,
}

public static class PetHungryLevelExtensions
{
	public static PetHungryLevel GetNextValue(this PetHungryLevel level) => level switch
	{
		PetHungryLevel.HUNGRY => PetHungryLevel.CONTENT,
		PetHungryLevel.CONTENT => PetHungryLevel.SEMIFULL,
		PetHungryLevel.SEMIFULL => PetHungryLevel.FULL,
		PetHungryLevel.FULL => PetHungryLevel.HUNGRY,
		_ => PetHungryLevel.HUNGRY,
	};

	public static PetHungryLevel FromId(int value)
	{
		if (!Enum.IsDefined(typeof(PetHungryLevel), (byte)value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), value, "Java PetHungryLevel.fromId indexes enum values and fails for unknown ids.");
		}

		return (PetHungryLevel)(byte)value;
	}
}
