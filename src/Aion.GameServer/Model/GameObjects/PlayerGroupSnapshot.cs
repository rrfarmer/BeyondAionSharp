namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerGroupSnapshot(
	int TeamId,
	IReadOnlyList<int> MemberObjectIds)
{
	public static PlayerGroupSnapshot FromMembers(int teamId, IReadOnlyList<Player> members)
	{
		// Java parity: model/team/GeneralTeam.getTeamId plus GeneralTeam.getMembers returning live Player objects.
		return new PlayerGroupSnapshot(teamId, members.Select(member => member.ObjectId).ToArray());
	}
}
