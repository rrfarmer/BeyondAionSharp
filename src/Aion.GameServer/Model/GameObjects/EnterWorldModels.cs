using Aion.GameServer.Model.Account;

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

public sealed record AtreianPassportLoginResult(
	bool ShouldSendSnapshot,
	bool ShouldSendAttendRewardMessage,
	IReadOnlyList<PlayerPassport> NewPassports);

public sealed record PlayerEnterWorldResult(
	EnterWorldCheckMessage Message,
	Player? Player = null,
	AtreianPassportLoginResult? AtreianPassportLogin = null);
