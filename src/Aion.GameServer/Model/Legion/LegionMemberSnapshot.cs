namespace Aion.GameServer.Model.Legion;

public sealed record LegionMemberSnapshot(
	int PlayerObjectId,
	int LegionId,
	string Name,
	string Rank,
	string Nickname,
	string SelfIntro,
	bool IsOnline);
