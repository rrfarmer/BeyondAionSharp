namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerFriend(
	int ObjectId,
	string Name,
	long Exp,
	string PlayerClass,
	string Gender,
	int MapId,
	DateTime? LastOnline,
	string Note,
	string Memo,
	bool IsOnline,
	int HouseAddressId = 0,
	byte HouseDoorState = 0);
