namespace Aion.GameServer.Model.GameObjects;

public enum PetEmote
{
	// Java parity: model/gameobjects/PetEmote.
	MoveStop = 0,
	MovePositionUpdate = 8,
	MoveTo = 12,
	NoInteraction = 128,
	FlyStart = 129,
	FlyStop = 130,
	Fly = 131,
	Emotion = 133,
	EatStart = 134,
	EatStop = 135,
	EatStopHeart = 136,
	NotHungry = 137,
	AttackModeFearless = 140,
	AttackModeFearful = 141,
	Alarm = 142,
	AlarmStopShouting = 144,
	InitInteraction = 145,
	PerformInteraction = 146,
	GetMoodGift = 147,
	Buff = 148,
	LootStart = 149,
	LootStop = 150,
	Unknown = int.MaxValue,
}

public static class PetEmoteResolver
{
	public static PetEmote GetEmoteById(int emoteId) => emoteId switch
	{
		0 => PetEmote.MoveStop,
		8 => PetEmote.MovePositionUpdate,
		12 => PetEmote.MoveTo,
		128 => PetEmote.NoInteraction,
		129 => PetEmote.FlyStart,
		130 => PetEmote.FlyStop,
		131 => PetEmote.Fly,
		133 => PetEmote.Emotion,
		134 => PetEmote.EatStart,
		135 => PetEmote.EatStop,
		136 => PetEmote.EatStopHeart,
		137 => PetEmote.NotHungry,
		140 => PetEmote.AttackModeFearless,
		141 => PetEmote.AttackModeFearful,
		142 => PetEmote.Alarm,
		144 => PetEmote.AlarmStopShouting,
		145 => PetEmote.InitInteraction,
		146 => PetEmote.PerformInteraction,
		147 => PetEmote.GetMoodGift,
		148 => PetEmote.Buff,
		149 => PetEmote.LootStart,
		150 => PetEmote.LootStop,
		_ => PetEmote.Unknown,
	};
}
