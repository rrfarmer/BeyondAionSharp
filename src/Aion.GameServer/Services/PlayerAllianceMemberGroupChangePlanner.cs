using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceMemberGroupChangePlanner
{
	public PlayerAllianceMemberGroupChangePlan? CreateMemberGroupChangePlan(
		int allianceId,
		IReadOnlyList<Player> members,
		int firstMemberObjectId,
		int secondMemberObjectId,
		int targetAllianceGroupId)
	{
		// Java parity: model/team/alliance/events/ChangeMemberGroupEvent sends MEMBER_GROUP_CHANGE packets for moved/swapped members.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var firstMember = members.FirstOrDefault(member => member.ObjectId == firstMemberObjectId);
		if (firstMember == null)
			return null;

		if (secondMemberObjectId != 0)
		{
			var secondMember = members.FirstOrDefault(member => member.ObjectId == secondMemberObjectId);
			if (secondMember == null)
				return null;

			return new PlayerAllianceMemberGroupChangePlan(
				allianceId,
				firstMemberObjectId,
				secondMemberObjectId,
				targetAllianceGroupId,
				[
					CreateIntent(allianceId, firstMember),
					CreateIntent(allianceId, secondMember),
				]);
		}

		return new PlayerAllianceMemberGroupChangePlan(
			allianceId,
			firstMemberObjectId,
			SecondMemberObjectId: 0,
			targetAllianceGroupId,
			[CreateIntent(allianceId, firstMember)]);
	}

	private static PlayerAllianceMemberInfoIntent CreateIntent(int allianceId, Player member)
	{
		return new PlayerAllianceMemberInfoIntent(
			RecipientObjectId: 0,
			member.ObjectId,
			PlayerAllianceEvent.MemberGroupChange,
			PlayerAllianceMemberInfoPacketPlan.FromPlayer(
				allianceId,
				member,
				PlayerAllianceMemberInfoEvent.MemberGroupChange));
	}
}
