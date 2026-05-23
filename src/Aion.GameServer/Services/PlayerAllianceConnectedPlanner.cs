using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceConnectedPlanner
{
	public PlayerAllianceConnectedPlan? CreateConnectedPlan(
		int allianceId,
		int leaderObjectId,
		IReadOnlyList<Player> members,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		int connectedPlayerObjectId,
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance)
	{
		// Java parity: model/team/alliance/events/PlayerConnectedEvent sends SM_ALLIANCE_INFO, then RECONNECT member-info packets.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var connected = members.FirstOrDefault(member => member.ObjectId == connectedPlayerObjectId);
		if (connected == null)
			return null;

		var actualLootRules = lootRules ?? PlayerGroupLootRules.Default();
		var intents = new List<PlayerAlliancePacketIntent>();
		var sequence = 0;

		intents.Add(new PlayerAlliancePacketIntent(
			sequence++,
			connectedPlayerObjectId,
			PlayerAlliancePacketIntentKind.AllianceInfo,
			AllianceInfoPlan: PlayerAllianceInfoPacketPlan.FromSnapshot(
				allianceId,
				leaderObjectId,
				allianceGroupSize: members.Count,
				activePlayerMapId: connected.Position.WorldId,
				currentViceCaptainObjectIds,
				actualLootRules,
				teamType,
				messageId: 0,
				message: string.Empty)));
		intents.Add(new PlayerAlliancePacketIntent(
			sequence++,
			connectedPlayerObjectId,
			PlayerAlliancePacketIntentKind.MemberInfo,
			MemberInfoPlan: PlayerAllianceMemberInfoPacketPlan.FromPlayer(
				allianceId,
				connected,
				PlayerAllianceMemberInfoEvent.Reconnect)));

		foreach (var member in members)
		{
			if (member.ObjectId == connectedPlayerObjectId)
				continue;

			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.MemberInfo,
				MemberInfoPlan: PlayerAllianceMemberInfoPacketPlan.FromPlayer(
					allianceId,
					connected,
					PlayerAllianceMemberInfoEvent.Reconnect)));
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				connectedPlayerObjectId,
				PlayerAlliancePacketIntentKind.MemberInfo,
				MemberInfoPlan: PlayerAllianceMemberInfoPacketPlan.FromPlayer(
					allianceId,
					member,
					PlayerAllianceMemberInfoEvent.Reconnect)));
		}

		return new PlayerAllianceConnectedPlan(allianceId, connectedPlayerObjectId, intents);
	}
}
