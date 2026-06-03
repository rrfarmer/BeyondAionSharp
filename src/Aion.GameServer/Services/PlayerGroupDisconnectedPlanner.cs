using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupDisconnectedPlanner(PlayerGroupRuntime groups)
{
	public PlayerGroupDisconnectedPlan Plan(Player disconnected)
	{
		// Java parity: model/team/group/PlayerGroupService.onPlayerLogout updates last-online, then PlayerDisconnectedEvent handles fanout.
		var teamId = disconnected.CurrentGroupSnapshot?.TeamId
			?? (disconnected.TeamMembership == PlayerTeamMembership.Group ? disconnected.CurrentTeamId : 0);
		if (teamId == 0)
			return PlayerGroupDisconnectedPlan.MissingGroup(disconnected.ObjectId);

		var descriptor = groups.GetDescriptor(teamId);
		var players = groups.GetMemberPlayers(teamId);
		if (descriptor == null || players.Count == 0)
			return PlayerGroupDisconnectedPlan.MissingGroup(disconnected.ObjectId, teamId);

		var members = players.Select(player => new PlayerGroupMember(player)).ToArray();
		var disconnectedMember = members.FirstOrDefault(member => member.ObjectId == disconnected.ObjectId);
		if (disconnectedMember == null)
			return PlayerGroupDisconnectedPlan.MissingMember(teamId, disconnected.ObjectId);

		if (!members.Any(member => member.IsOnline))
			return PlayerGroupDisconnectedPlan.NoOnlineMembersDisband(teamId, disconnected.ObjectId);

		PlayerGroupLeaderChangePlan? leaderChangePlan = null;
		var wouldTriggerLeaderChange = descriptor.LeaderObjectId == disconnected.ObjectId;
		var fallbackLeaderObjectId = wouldTriggerLeaderChange
			? SelectFallbackLeaderObjectId(members, disconnected.ObjectId)
			: null;
		if (fallbackLeaderObjectId.HasValue)
			leaderChangePlan = CreateLeaderChangePlan(teamId, descriptor, members, fallbackLeaderObjectId.Value);

		var intents = CreateDisconnectedPacketIntents(teamId, disconnectedMember, members);
		return new PlayerGroupDisconnectedPlan(
			teamId,
			disconnected.ObjectId,
			PlayerGroupDisconnectedPlanStatus.Planned,
			WouldDisbandIfNoOnlineMembersRemain: false,
			wouldTriggerLeaderChange,
			fallbackLeaderObjectId,
			leaderChangePlan,
			intents);
	}

	private static int? SelectFallbackLeaderObjectId(IReadOnlyList<PlayerGroupMember> members, int disconnectedLeaderObjectId)
	{
		// Java parity: ChangeLeaderEvent.changeLeaderToNextAvailablePlayer chooses the first online non-leader member.
		return members.FirstOrDefault(member => member.IsOnline && member.ObjectId != disconnectedLeaderObjectId)?.ObjectId;
	}

	private static PlayerGroupLeaderChangePlan CreateLeaderChangePlan(
		int teamId,
		PlayerGroupDescriptor descriptor,
		IReadOnlyList<PlayerGroupMember> members,
		int fallbackLeaderObjectId)
	{
		// Java parity: ChangeGroupLeaderEvent.changeLeaderTo sends SM_GROUP_INFO and leader messages to group.forEach members.
		var updatedDescriptor = descriptor with { LeaderObjectId = fallbackLeaderObjectId };
		var fallbackLeader = members.First(member => member.ObjectId == fallbackLeaderObjectId);
		var sequence = 0;
		var intents = members
			.Select(member => new PlayerGroupLeaderChangePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerGroupInfoPacketPlan.FromDescriptor(updatedDescriptor, member.Player.Position.WorldId),
				member.ObjectId == fallbackLeaderObjectId
					? SmSystemMessage.PartyYouBecomeNewLeader()
					: SmSystemMessage.PartyHeIsNewLeader(fallbackLeader.Name)))
			.ToArray();

		return new PlayerGroupLeaderChangePlan(teamId, fallbackLeaderObjectId, intents);
	}

	private static IReadOnlyList<PlayerGroupDisconnectedPacketIntent> CreateDisconnectedPacketIntents(
		int teamId,
		PlayerGroupMember disconnectedMember,
		IReadOnlyList<PlayerGroupMember> members)
	{
		var intents = new List<PlayerGroupDisconnectedPacketIntent>();
		var disconnectedPlan = PlayerGroupMemberInfoPacketPlan.FromMember(teamId, disconnectedMember, PlayerGroupEvent.Disconnected);
		var sequence = 0;
		foreach (var member in members)
		{
			if (member.ObjectId == disconnectedMember.ObjectId)
				continue;

			intents.Add(new PlayerGroupDisconnectedPacketIntent(
				sequence++,
				member.ObjectId,
				disconnectedMember.ObjectId,
				PlayerGroupDisconnectedPacketIntentKind.SystemMessage,
				SystemMessage: SmSystemMessage.PartyHeBecomeOffline(disconnectedMember.Name)));
			intents.Add(new PlayerGroupDisconnectedPacketIntent(
				sequence++,
				member.ObjectId,
				disconnectedMember.ObjectId,
				PlayerGroupDisconnectedPacketIntentKind.MemberInfo,
				MemberInfoPlan: disconnectedPlan));

			// Java oddity: PlayerDisconnectedEvent also sends DISCONNECTED member-info about each remaining member to the disconnecting player.
			intents.Add(new PlayerGroupDisconnectedPacketIntent(
				sequence++,
				disconnectedMember.ObjectId,
				member.ObjectId,
				PlayerGroupDisconnectedPacketIntentKind.MemberInfo,
				MemberInfoPlan: PlayerGroupMemberInfoPacketPlan.FromMember(teamId, member, PlayerGroupEvent.Disconnected)));
		}

		return intents;
	}
}

public sealed record PlayerGroupDisconnectedPlan(
	int TeamId,
	int DisconnectedPlayerObjectId,
	PlayerGroupDisconnectedPlanStatus Status,
	bool WouldDisbandIfNoOnlineMembersRemain,
	bool WouldTriggerLeaderChange,
	int? FallbackLeaderObjectId,
	PlayerGroupLeaderChangePlan? LeaderChangePlan,
	IReadOnlyList<PlayerGroupDisconnectedPacketIntent> PacketIntents)
{
	public bool IsPlanned => Status == PlayerGroupDisconnectedPlanStatus.Planned;

	public static PlayerGroupDisconnectedPlan MissingGroup(int disconnectedPlayerObjectId, int teamId = 0)
	{
		return new PlayerGroupDisconnectedPlan(
			teamId,
			disconnectedPlayerObjectId,
			PlayerGroupDisconnectedPlanStatus.MissingGroup,
			WouldDisbandIfNoOnlineMembersRemain: false,
			WouldTriggerLeaderChange: false,
			FallbackLeaderObjectId: null,
			LeaderChangePlan: null,
			PacketIntents: Array.Empty<PlayerGroupDisconnectedPacketIntent>());
	}

	public static PlayerGroupDisconnectedPlan MissingMember(int teamId, int disconnectedPlayerObjectId)
	{
		return new PlayerGroupDisconnectedPlan(
			teamId,
			disconnectedPlayerObjectId,
			PlayerGroupDisconnectedPlanStatus.MissingMember,
			WouldDisbandIfNoOnlineMembersRemain: false,
			WouldTriggerLeaderChange: false,
			FallbackLeaderObjectId: null,
			LeaderChangePlan: null,
			PacketIntents: Array.Empty<PlayerGroupDisconnectedPacketIntent>());
	}

	public static PlayerGroupDisconnectedPlan NoOnlineMembersDisband(int teamId, int disconnectedPlayerObjectId)
	{
		return new PlayerGroupDisconnectedPlan(
			teamId,
			disconnectedPlayerObjectId,
			PlayerGroupDisconnectedPlanStatus.NoOnlineMembersDisband,
			WouldDisbandIfNoOnlineMembersRemain: true,
			WouldTriggerLeaderChange: false,
			FallbackLeaderObjectId: null,
			LeaderChangePlan: null,
			PacketIntents: Array.Empty<PlayerGroupDisconnectedPacketIntent>());
	}
}

public enum PlayerGroupDisconnectedPlanStatus
{
	Planned,
	MissingGroup,
	MissingMember,
	NoOnlineMembersDisband,
}

public enum PlayerGroupDisconnectedPacketIntentKind
{
	SystemMessage,
	MemberInfo,
}

public sealed record PlayerGroupDisconnectedPacketIntent(
	int Sequence,
	int RecipientObjectId,
	int SubjectObjectId,
	PlayerGroupDisconnectedPacketIntentKind Kind,
	PlayerGroupMemberInfoPacketPlan? MemberInfoPlan = null,
	SmSystemMessage? SystemMessage = null)
{
	public GameServerPacket CreatePacket()
	{
		return Kind switch
		{
			PlayerGroupDisconnectedPacketIntentKind.SystemMessage when SystemMessage != null => SystemMessage,
			PlayerGroupDisconnectedPacketIntentKind.MemberInfo when MemberInfoPlan != null => new SmGroupMemberInfo(MemberInfoPlan),
			_ => throw new InvalidOperationException("Group disconnected packet intent is missing packet metadata."),
		};
	}
}
