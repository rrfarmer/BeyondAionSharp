using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceEnteredPlanner
{
	public PlayerAllianceEnteredPlan? CreateEnteredPlan(
		int allianceId,
		int leaderObjectId,
		IReadOnlyList<Player> membersAfterJoin,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		int invitedPlayerObjectId,
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		bool isInLeague = false,
		PlayerAllianceBrandIntent? brandIntent = null)
	{
		// Java parity: model/team/alliance/events/PlayerAllianceEnteredEvent sends ordered info/member/system packets after addPlayerToAlliance.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var invited = membersAfterJoin.FirstOrDefault(member => member.ObjectId == invitedPlayerObjectId);
		if (invited == null)
			return null;

		var actualLootRules = lootRules ?? PlayerGroupLootRules.Default();
		var allianceInfoByRecipient = membersAfterJoin.ToDictionary(
			member => member.ObjectId,
			member => PlayerAllianceInfoPacketPlan.FromSnapshot(
				allianceId,
				leaderObjectId,
				allianceGroupSize: membersAfterJoin.Count,
				activePlayerMapId: member.Position.WorldId,
				currentViceCaptainObjectIds,
				actualLootRules,
				teamType,
				messageId: 0,
				message: string.Empty,
				leagueId: isInLeague ? 1 : 0));
		var joinPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(
			allianceId,
			invited,
			PlayerAllianceMemberInfoEvent.Join);

		var intents = new List<PlayerAlliancePacketIntent>();
		var sequence = 0;
		intents.Add(new PlayerAlliancePacketIntent(
			sequence++,
			invitedPlayerObjectId,
			PlayerAlliancePacketIntentKind.AllianceInfo,
			AllianceInfoPlan: allianceInfoByRecipient[invitedPlayerObjectId]));
		intents.Add(new PlayerAlliancePacketIntent(
			sequence++,
			invitedPlayerObjectId,
			PlayerAlliancePacketIntentKind.SystemMessage,
			SystemMessage: SmSystemMessage.ForceEnteredForce()));
		intents.Add(new PlayerAlliancePacketIntent(
			sequence++,
			invitedPlayerObjectId,
			PlayerAlliancePacketIntentKind.MemberInfo,
			MemberInfoPlan: joinPlan));

		foreach (var member in membersAfterJoin)
		{
			if (member.ObjectId == invitedPlayerObjectId)
				continue;

			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.MemberInfo,
				MemberInfoPlan: joinPlan));
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.SystemMessage,
				SystemMessage: SmSystemMessage.ForceHeEnteredForce(invited.Name)));
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerAlliancePacketIntentKind.AllianceInfo,
				AllianceInfoPlan: allianceInfoByRecipient[member.ObjectId]));
			intents.Add(new PlayerAlliancePacketIntent(
				sequence++,
				invitedPlayerObjectId,
				PlayerAlliancePacketIntentKind.MemberInfo,
				MemberInfoPlan: PlayerAllianceMemberInfoPacketPlan.FromPlayer(
					allianceId,
					member,
					PlayerAllianceMemberInfoEvent.Enter)));
		}

		return new PlayerAllianceEnteredPlan(
			allianceId,
			invitedPlayerObjectId,
			intents,
			WouldSendBrands: true,
			WouldBroadcastAbyssRank: true,
			WouldBroadcastLeague: isInLeague,
			BrandIntent: brandIntent);
	}
}
