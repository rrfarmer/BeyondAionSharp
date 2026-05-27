namespace Aion.GameServer.Services.ToyPet;

public enum PetHungryLevel : byte
{
	// Java parity: services/toypet/PetHungryLevel enum ids.
	Hungry = 0,
	Content = 1,
	Semifull = 2,
	Full = 3,
}

public static class PetHungryLevelExtensions
{
	public static PetHungryLevel GetNextValue(this PetHungryLevel level) => level switch
	{
		PetHungryLevel.Hungry => PetHungryLevel.Content,
		PetHungryLevel.Content => PetHungryLevel.Semifull,
		PetHungryLevel.Semifull => PetHungryLevel.Full,
		PetHungryLevel.Full => PetHungryLevel.Hungry,
		_ => PetHungryLevel.Hungry,
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
