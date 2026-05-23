using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceLeaderChangePlanner
{
	public PlayerAllianceLeaderChangePlan CreateLeaderChangePlan(
		int allianceId,
		int oldLeaderObjectId,
		IReadOnlyList<Player> members,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		int newLeaderObjectId,
		bool eventPlayerWasSpecified,
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		bool isInLeague = false)
	{
		// Java parity: model/team/alliance/events/ChangeAllianceLeaderEvent.changeLeaderTo non-league packet/message fanout.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var newLeader = members.First(member => member.ObjectId == newLeaderObjectId);
		var updatedViceCaptainIds = currentViceCaptainObjectIds
			.Where(objectId => objectId != newLeaderObjectId)
			.Distinct()
			.ToArray();

		var actualLootRules = lootRules ?? PlayerGroupLootRules.Default();
		var allianceInfoIntents = isInLeague
			? Array.Empty<PlayerAllianceInfoIntent>()
			: members
				.Select(member => new PlayerAllianceInfoIntent(
					member.ObjectId,
					PlayerAllianceInfoPacketPlan.FromSnapshot(
						allianceId,
						leaderObjectId: newLeaderObjectId,
						allianceGroupSize: members.Count,
						activePlayerMapId: member.Position.WorldId,
						updatedViceCaptainIds,
						actualLootRules,
						teamType,
						messageId: 0,
						message: string.Empty)))
				.ToArray();

		var systemMessageIntents = members
			.SelectMany(member => CreateSystemMessageIntents(member, newLeader, eventPlayerWasSpecified))
			.ToArray();

		return new PlayerAllianceLeaderChangePlan(
			allianceId,
			oldLeaderObjectId,
			newLeaderObjectId,
			eventPlayerWasSpecified,
			updatedViceCaptainIds,
			allianceInfoIntents,
			systemMessageIntents,
			WouldBroadcastLeague: isInLeague);
	}

	private static IEnumerable<PlayerAllianceSystemMessageIntent> CreateSystemMessageIntents(
		Player member,
		Player newLeader,
		bool eventPlayerWasSpecified)
	{
		if (member.ObjectId == newLeader.ObjectId)
		{
			yield return new PlayerAllianceSystemMessageIntent(
				member.ObjectId,
				SmSystemMessage.ForceYouBecomeNewLeader());
			yield break;
		}

		if (eventPlayerWasSpecified)
		{
			yield return new PlayerAllianceSystemMessageIntent(
				member.ObjectId,
				SmSystemMessage.ForceHeIsNewLeader(newLeader.Name));
		}
	}
}
