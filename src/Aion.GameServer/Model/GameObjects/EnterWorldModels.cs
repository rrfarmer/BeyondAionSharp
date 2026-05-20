namespace Aion.GameServer.Model.GameObjects;

public enum EnterWorldCheckMessage : byte
{
	Ok = 0,
	CharAlreadyOnline = 1,
	ConnectionError = 2,
	BothFactions = 3,
	ReservationTime = 4,
	TooManyCharacters = 5,
	ReentryTime = 6,
}

public sealed record PlayerEnterWorldResult(EnterWorldCheckMessage Message, Player? Player = null);
