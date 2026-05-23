using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceLeavedPlanner
{
	public PlayerAllianceLeavedPlan CreateLeavedPlan(
		int allianceId,
		int leaderObjectId,
		IReadOnlyList<Player> membersAfterLeave,
		Player leavedPlayer,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		PlayerAllianceLeaveReason reason = PlayerAllianceLeaveReason.Leave,
		string banPersonName = "",
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		bool leavedPlayerWasLeader = false,
		bool shouldDisband = false,
		bool isInLeague = false)
	{
		// Java parity: model/team/alliance/events/PlayerAllianceLeavedEvent removes the member, then sends leave reason/member/alliance packets to remaining members.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var actualLootRules = lootRules ?? PlayerGroupLootRules.Default();
		var viceCaptainIdsAfterLeave = currentViceCaptainObjectIds
			.Where(objectId => objectId != leavedPlayer.ObjectId)
			.Distinct()
			.ToArray();
		var intents = new List<PlayerAlliancePacketIntent>();
		var sequence = 0;
		var leaveMessage = CreateLeaveMessage(reason, leavedPlayer.Name, banPersonName);
		var memberInfoPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(
			allianceId,
			leavedPlayer,
			PlayerAllianceMemberInfoEvent.Leave);

		foreach (var member in membersAfterLeave)
		{
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.SystemMessage,
				SystemMessage: leaveMessage));

			if (reason == PlayerAllianceLeaveReason.Disband)
				continue;

			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.MemberInfo,
				MemberInfoPlan: memberInfoPlan));
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.AllianceInfo,
				AllianceInfoPlan: PlayerAllianceInfoPacketPlan.FromSnapshot(
					allianceId,
					leaderObjectId,
					allianceGroupSize: membersAfterLeave.Count,
					activePlayerMapId: member.Position.WorldId,
					viceCaptainIdsAfterLeave,
					actualLootRules,
					teamType,
					messageId: 0,
					message: string.Empty,
					leagueId: isInLeague ? 1 : 0)));
		}

		if (reason == PlayerAllianceLeaveReason.Ban)
		{
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				leavedPlayer.ObjectId,
				PlayerAlliancePacketIntentKind.SystemMessage,
				SystemMessage: SmSystemMessage.ForceBanMe(banPersonName)));
		}
		else if (reason == PlayerAllianceLeaveReason.Disband)
		{
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				leavedPlayer.ObjectId,
				PlayerAlliancePacketIntentKind.SystemMessage,
				SystemMessage: SmSystemMessage.PartyAllianceDispersed()));
		}

		var wouldBroadcastLeague = isInLeague
			&& (reason == PlayerAllianceLeaveReason.Ban || reason == PlayerAllianceLeaveReason.Leave);
		var wouldDisband = shouldDisband
			&& teamType != PlayerAllianceTeamType.AutoAlliance
			&& reason is PlayerAllianceLeaveReason.Ban or PlayerAllianceLeaveReason.Leave or PlayerAllianceLeaveReason.LeaveTimeout;

		return new PlayerAllianceLeavedPlan(
			allianceId,
			leavedPlayer.ObjectId,
			reason,
			viceCaptainIdsAfterLeave,
			intents,
			WouldTriggerLeaderChange: reason != PlayerAllianceLeaveReason.Disband && leavedPlayerWasLeader,
			WouldDisband: wouldDisband,
			WouldBroadcastLeague: wouldBroadcastLeague);
	}

	private static SmSystemMessage CreateLeaveMessage(
		PlayerAllianceLeaveReason reason,
		string leavedPlayerName,
		string banPersonName)
	{
		return reason switch
		{
			PlayerAllianceLeaveReason.Leave => SmSystemMessage.ForceLeaveHim(leavedPlayerName),
			PlayerAllianceLeaveReason.LeaveTimeout => SmSystemMessage.PartyAllianceHeLeavedPartyOfflineTimeout(leavedPlayerName),
			PlayerAllianceLeaveReason.Ban => SmSystemMessage.ForceBanHim(banPersonName, leavedPlayerName),
			PlayerAllianceLeaveReason.Disband => SmSystemMessage.PartyAllianceDispersed(),
			_ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unsupported alliance leave reason."),
		};
	}
}
