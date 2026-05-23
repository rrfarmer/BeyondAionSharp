namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerGroupDescriptor(
	int TeamId,
	int LeaderObjectId,
	PlayerGroupType TeamType,
	int MaxMemberCount)
{
	public const int JavaMaxMemberCount = 6;

	public bool IsFull(int memberCount)
	{
		// Java parity: model/team/GeneralTeam.isFull compares size() with PlayerGroup.getMaxMemberCount().
		return memberCount >= MaxMemberCount;
	}

	public static PlayerGroupDescriptor FromLeader(
		int teamId,
		Player leader,
		PlayerGroupType teamType = PlayerGroupType.Group,
		int maxMemberCount = JavaMaxMemberCount)
	{
		// Java parity: model/team/group/PlayerGroup constructor stores TeamType and calls GeneralTeam.setLeader.
		return new PlayerGroupDescriptor(teamId, leader.ObjectId, teamType, maxMemberCount);
	}
}
