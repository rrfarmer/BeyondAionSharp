using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceViceCaptainAssignmentPlanner
{
	public PlayerAllianceViceCaptainAssignmentPlan CreateAssignmentPlan(
		int allianceId,
		int leaderObjectId,
		IReadOnlyList<Player> members,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		int eventPlayerObjectId,
		PlayerAllianceAssignType assignType,
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		bool isInLeague = false,
		int leagueId = 0)
	{
		// Java parity: model/team/alliance/events/AssignViceCaptainEvent mutates viceCaptainIds, then sends SM_ALLIANCE_INFO to every member.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var eventPlayer = members.FirstOrDefault(member => member.ObjectId == eventPlayerObjectId);
		if (eventPlayer == null)
		{
			return CreateNoopPlan(
				allianceId,
				leaderObjectId,
				eventPlayerObjectId,
				assignType,
				PlayerAllianceRolePlanStatus.EventPlayerMissing,
				currentViceCaptainObjectIds,
				isInLeague: false);
		}

		if (!eventPlayer.IsOnline)
		{
			return CreateNoopPlan(
				allianceId,
				leaderObjectId,
				eventPlayerObjectId,
				assignType,
				PlayerAllianceRolePlanStatus.EventPlayerOffline,
				currentViceCaptainObjectIds,
				isInLeague: false);
		}

		var updatedViceCaptainIds = currentViceCaptainObjectIds.Distinct().ToList();
		var messageId = 0;
		switch (assignType)
		{
			case PlayerAllianceAssignType.Promote:
				if (updatedViceCaptainIds.Count == 4)
				{
					return new PlayerAllianceViceCaptainAssignmentPlan(
						allianceId,
						leaderObjectId,
						eventPlayerObjectId,
						assignType,
						PlayerAllianceRolePlanStatus.PromoteLimitReached,
						updatedViceCaptainIds,
						AllianceInfoIntents: [],
						new PlayerAllianceSystemMessageIntent(
							leaderObjectId,
							SmSystemMessage.ForceCannotPromoteManager()),
						WouldBroadcastLeague: false);
				}

				if (!updatedViceCaptainIds.Contains(eventPlayerObjectId))
					updatedViceCaptainIds.Add(eventPlayerObjectId);
				messageId = PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId;
				break;
			case PlayerAllianceAssignType.Demote:
				updatedViceCaptainIds.Remove(eventPlayerObjectId);
				messageId = PlayerAllianceInfoPacketPlan.ViceCaptainDemoteMessageId;
				break;
			case PlayerAllianceAssignType.DemoteCaptainToViceCaptain:
				if (updatedViceCaptainIds.Count < 3 && !updatedViceCaptainIds.Contains(eventPlayerObjectId))
					updatedViceCaptainIds.Add(eventPlayerObjectId);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(assignType), assignType, "Unsupported alliance vice-captain assignment type.");
		}

		var actualLootRules = lootRules ?? PlayerGroupLootRules.Default();
		var intents = members
			.Select(member => new PlayerAllianceInfoIntent(
				member.ObjectId,
				PlayerAllianceInfoPacketPlan.FromSnapshot(
					allianceId,
					leaderObjectId,
					allianceGroupSize: members.Count,
					activePlayerMapId: member.Position.WorldId,
					updatedViceCaptainIds,
					actualLootRules,
					teamType,
					messageId,
					eventPlayer.Name,
					leagueId)))
			.ToArray();

		return new PlayerAllianceViceCaptainAssignmentPlan(
			allianceId,
			leaderObjectId,
			eventPlayerObjectId,
			assignType,
			PlayerAllianceRolePlanStatus.Planned,
			updatedViceCaptainIds,
			intents,
			WouldBroadcastLeague: isInLeague);
	}

	private static PlayerAllianceViceCaptainAssignmentPlan CreateNoopPlan(
		int allianceId,
		int leaderObjectId,
		int eventPlayerObjectId,
		PlayerAllianceAssignType assignType,
		PlayerAllianceRolePlanStatus status,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		bool isInLeague)
	{
		return new PlayerAllianceViceCaptainAssignmentPlan(
			allianceId,
			leaderObjectId,
			eventPlayerObjectId,
			assignType,
			status,
			currentViceCaptainObjectIds.Distinct().ToArray(),
			AllianceInfoIntents: [],
			WouldBroadcastLeague: isInLeague);
	}
}
