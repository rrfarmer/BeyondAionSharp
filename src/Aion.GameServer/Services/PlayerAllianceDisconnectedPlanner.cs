using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceDisconnectedPlanner
{
	public PlayerAllianceDisconnectedPlan CreateDisconnectedPlan(
		int allianceId,
		int leaderObjectId,
		IReadOnlyList<Player> membersBeforeDisconnect,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		int disconnectedPlayerObjectId,
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		bool isInLeague = false,
		bool noOnlineMembersRemain = false)
	{
		// Java parity: model/team/alliance/events/PlayerDisconnectedEvent sends offline system/member/alliance packets to every other member.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var disconnected = membersBeforeDisconnect.FirstOrDefault(member => member.ObjectId == disconnectedPlayerObjectId);
		if (disconnected == null)
		{
			return new PlayerAllianceDisconnectedPlan(
				allianceId,
				disconnectedPlayerObjectId,
				PlayerAllianceDisconnectedPlanStatus.DisconnectedMemberMissing,
				PacketIntents: []);
		}

		if (disconnectedPlayerObjectId == leaderObjectId)
		{
			return new PlayerAllianceDisconnectedPlan(
				allianceId,
				disconnectedPlayerObjectId,
				PlayerAllianceDisconnectedPlanStatus.LeaderDisconnectDeferred,
				PacketIntents: [],
				WouldTriggerLeaderChange: true,
				WouldDisbandIfNoOnlineMembersRemain: noOnlineMembersRemain,
				WouldBroadcastLeague: isInLeague && !noOnlineMembersRemain);
		}

		var actualLootRules = lootRules ?? PlayerGroupLootRules.Default();
		var memberInfoPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(
			allianceId,
			disconnected,
			PlayerAllianceMemberInfoEvent.Disconnected);
		var intents = new List<PlayerAlliancePacketIntent>();
		var sequence = 0;
		foreach (var member in membersBeforeDisconnect)
		{
			if (member.ObjectId == disconnectedPlayerObjectId)
				continue;

			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.SystemMessage,
				SystemMessage: SmSystemMessage.ForceHeBecomeOffline(disconnected.Name)));
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
					allianceGroupSize: membersBeforeDisconnect.Count,
					activePlayerMapId: member.Position.WorldId,
					currentViceCaptainObjectIds,
					actualLootRules,
					teamType,
					messageId: 0,
					message: string.Empty,
					leagueId: isInLeague ? 1 : 0)));
		}

		return new PlayerAllianceDisconnectedPlan(
			allianceId,
			disconnectedPlayerObjectId,
			PlayerAllianceDisconnectedPlanStatus.Planned,
			intents,
			WouldDisbandIfNoOnlineMembersRemain: noOnlineMembersRemain,
			WouldBroadcastLeague: isInLeague && !noOnlineMembersRemain);
	}
}
