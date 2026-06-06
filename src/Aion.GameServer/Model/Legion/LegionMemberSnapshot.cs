namespace Aion.GameServer.Model.Legion;

public sealed record LegionMemberSnapshot(
	int PlayerObjectId,
	int LegionId,
	string Name,
	string Rank,
	string Nickname,
	string SelfIntro,
	bool IsOnline,
	string PlayerClass = "",
	long Exp = 0,
	int WorldId = 0,
	DateTime? LastOnline = null,
	int HouseAddressId = 0,
	int HouseDoorStateId = 0);
