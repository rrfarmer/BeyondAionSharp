namespace Aion.GameServer.Network.LoginServer;

public sealed record AccountAuthResult(
	int AccountId,
	bool Ok,
	string AccountName = "",
	long CreationDate = 0,
	long AccumulatedOnlineTime = 0,
	long AccumulatedRestTime = 0,
	byte AccessLevel = 0,
	byte Membership = 0,
	string AllowedHddSerial = "");
