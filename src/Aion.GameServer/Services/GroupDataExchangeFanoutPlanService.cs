using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum GroupDataExchangeFanoutPlanStatus
{
	IgnoredNoActivePlayer,
	IgnoredEmptyData,
	RejectedDataTooLarge,
	NearbyBroadcastVisiblePlayersAndSelf,
	GroupBroadcastMembersExceptSelf,
	AllianceGroupBroadcastMembersExceptSelf,
	LeagueAllianceGroupBroadcastMembersExceptSelf,
	IgnoredNoRecipients,
	IgnoredUnsupportedGroupType,
}

public sealed record GroupDataExchangeFanoutPlan(
	GroupDataExchangeFanoutPlanStatus Status,
	int SourcePlayerObjectId,
	byte Action,
	byte GroupType,
	byte Unknown2,
	int DataLength,
	GameServerPacket? Packet,
	IReadOnlyList<int> RecipientObjectIds,
	bool IncludeSourcePlayer,
	string JavaUtilityMethod,
	string JavaSource,
	bool IsLive);

public static class GroupDataExchangeFanoutPlanService
{
	public const int MaxExchangeDataSize = GameServerPacket.MaxUsablePacketBodySize - 6;

	public static GroupDataExchangeFanoutPlan CreatePlan(
		Player? player,
		byte action,
		byte groupType,
		byte unknown2,
		byte[] data,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime,
		PlayerLeagueRuntime leagueRuntime)
	{
		// Java parity: network/aion/clientpackets/CM_GROUP_DATA_EXCHANGE.runImpl.
		if (player == null)
			return Ignored(GroupDataExchangeFanoutPlanStatus.IgnoredNoActivePlayer, 0, action, groupType, unknown2, data.Length);

		if (data.Length == 0)
			return Ignored(GroupDataExchangeFanoutPlanStatus.IgnoredEmptyData, player.ObjectId, action, groupType, unknown2, data.Length);

		if (data.Length > MaxExchangeDataSize)
			return Ignored(GroupDataExchangeFanoutPlanStatus.RejectedDataTooLarge, player.ObjectId, action, groupType, unknown2, data.Length);

		if (action == 1)
		{
			return new GroupDataExchangeFanoutPlan(
				GroupDataExchangeFanoutPlanStatus.NearbyBroadcastVisiblePlayersAndSelf,
				player.ObjectId,
				action,
				groupType,
				unknown2,
				data.Length,
				SmGroupDataExchange.NearbyBroadcast(data),
				Array.Empty<int>(),
				IncludeSourcePlayer: true,
				"PacketSendUtility.broadcastPacketAndReceive(player, new SM_GROUP_DATA_EXCHANGE(data))",
				"CM_GROUP_DATA_EXCHANGE.runImpl action == 1 branch",
				IsLive: false);
		}

		var recipients = ResolveTeamRecipients(player, groupType, groupRuntime, allianceRuntime, leagueRuntime)
			.Where(objectId => objectId != player.ObjectId)
			.ToArray();
		if (recipients.Length == 0)
		{
			var status = groupType is 0 or 1 or 2
				? GroupDataExchangeFanoutPlanStatus.IgnoredNoRecipients
				: GroupDataExchangeFanoutPlanStatus.IgnoredUnsupportedGroupType;
			return Ignored(status, player.ObjectId, action, groupType, unknown2, data.Length);
		}

		return new GroupDataExchangeFanoutPlan(
			CreateTeamStatus(groupType),
			player.ObjectId,
			action,
			groupType,
			unknown2,
			data.Length,
			SmGroupDataExchange.GroupBroadcast(data, action, unknown2),
			recipients,
			IncludeSourcePlayer: false,
			"PacketSendUtility.sendPacket(member, new SM_GROUP_DATA_EXCHANGE(data, action, unk2))",
			CreateTeamJavaSource(groupType),
			IsLive: false);
	}

	private static IReadOnlyList<int> ResolveTeamRecipients(
		Player player,
		byte groupType,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime,
		PlayerLeagueRuntime leagueRuntime)
	{
		return groupType switch
		{
			0 => ResolveGroupRecipients(player, groupRuntime),
			1 => ResolveAllianceGroupRecipients(player, allianceRuntime, requireLeague: false, leagueRuntime),
			2 => ResolveAllianceGroupRecipients(player, allianceRuntime, requireLeague: true, leagueRuntime),
			_ => Array.Empty<int>(),
		};
	}

	private static IReadOnlyList<int> ResolveGroupRecipients(Player player, PlayerGroupRuntime groupRuntime)
	{
		var group = groupRuntime.Resolve(player);
		return group == null ? Array.Empty<int>() : groupRuntime.GetMemberObjectIds(group.TeamId);
	}

	private static IReadOnlyList<int> ResolveAllianceGroupRecipients(
		Player player,
		PlayerAllianceRuntime allianceRuntime,
		bool requireLeague,
		PlayerLeagueRuntime leagueRuntime)
	{
		var alliance = allianceRuntime.Resolve(player);
		if (alliance == null)
			return Array.Empty<int>();
		if (requireLeague && leagueRuntime.ResolveByAllianceId(alliance.AllianceId) == null)
			return Array.Empty<int>();

		var member = allianceRuntime.GetMember(alliance.AllianceId, player.ObjectId);
		return member == null
			? Array.Empty<int>()
			: allianceRuntime.GetMemberObjectIdsByGroupId(alliance.AllianceId, member.AllianceGroupId);
	}

	private static GroupDataExchangeFanoutPlanStatus CreateTeamStatus(byte groupType)
	{
		return groupType switch
		{
			0 => GroupDataExchangeFanoutPlanStatus.GroupBroadcastMembersExceptSelf,
			1 => GroupDataExchangeFanoutPlanStatus.AllianceGroupBroadcastMembersExceptSelf,
			2 => GroupDataExchangeFanoutPlanStatus.LeagueAllianceGroupBroadcastMembersExceptSelf,
			_ => GroupDataExchangeFanoutPlanStatus.IgnoredUnsupportedGroupType,
		};
	}

	private static string CreateTeamJavaSource(byte groupType)
	{
		return groupType switch
		{
			0 => "CM_GROUP_DATA_EXCHANGE.runImpl groupType 0 -> player.getPlayerGroup().getOnlineMembers(), excluding player",
			1 => "CM_GROUP_DATA_EXCHANGE.runImpl groupType 1 -> player.getPlayerAllianceGroup().getOnlineMembers(), excluding player",
			2 => "CM_GROUP_DATA_EXCHANGE.runImpl groupType 2 -> player.isInLeague() then player.getPlayerAllianceGroup().getOnlineMembers(), excluding player",
			_ => "CM_GROUP_DATA_EXCHANGE.runImpl unsupported groupType leaves players null",
		};
	}

	private static GroupDataExchangeFanoutPlan Ignored(
		GroupDataExchangeFanoutPlanStatus status,
		int sourcePlayerObjectId,
		byte action,
		byte groupType,
		byte unknown2,
		int dataLength)
	{
		return new GroupDataExchangeFanoutPlan(
			status,
			sourcePlayerObjectId,
			action,
			groupType,
			unknown2,
			dataLength,
			Packet: null,
			Array.Empty<int>(),
			IncludeSourcePlayer: false,
			string.Empty,
			CreateTeamJavaSource(groupType),
			IsLive: false);
	}
}
