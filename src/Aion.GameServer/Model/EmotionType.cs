namespace Aion.GameServer.Model;

public enum EmotionType
{
	None = -1,
	SelectTarget = 0,
	Jump = 1,
	Sit = 2,
	Stand = 3,
	ChairSit = 4,
	ChairUp = 5,
	StartFlyTeleport = 6,
	LandFlyTeleport = 7,
	Windstream = 8,
	WindstreamEnd = 9,
	WindstreamExit = 10,
	WindstreamStartBoost = 11,
	WindstreamEndBoost = 12,
	Fly = 13,
	Land = 14,
	Ride = 15,
	RideEnd = 16,
	Attack = 17,
	Die = 18,
	Resurrect = 19,
	Emote = 21,
	EmoteEnd = 22,
	AttackModeInMove = 24,
	NeutralModeInMove = 25,
	Walk = 26,
	Run = 27,
	OpenDoor = 31,
	CloseDoor = 32,
	OpenPrivateShop = 33,
	ClosePrivateShop = 34,
	ChangeSpeed = 35,
	PowershardOn = 36,
	PowershardOff = 37,
	AttackModeInStanding = 38,
	NeutralModeInStanding = 39,
	StartLoot = 40,
	EndLoot = 41,
	StartQuestLoot = 42,
	EndQuestLoot = 43,
	TurnRight = 44,
	TurnLeft = 45,
	StartGlide = 46,
	StopGlide = 47,
	StopFly = 48,
	SummonStopJump = 49,
	StartFeeding = 50,
	EndFeeding = 51,
	WindstreamStrafe = 52,
	StartSprint = 53,
	EndSprint = 54,
}

public static class EmotionTypes
{
	public static EmotionType FromId(int id)
	{
		// Java parity: model/EmotionType.getEmotionTypeById.
		return Enum.IsDefined(typeof(EmotionType), id) ? (EmotionType)id : EmotionType.None;
	}
}
