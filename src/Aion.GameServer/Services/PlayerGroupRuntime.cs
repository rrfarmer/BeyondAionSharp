using Aion.GameServer.Model.GameObjects;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, List<PlayerGroupMember>> _membersByTeamId = [];
	private readonly Dictionary<int, PlayerGroupDescriptor> _descriptorsByTeamId = [];

	public PlayerGroupSnapshot CreateOrUpdateGroup(
		int teamId,
		IReadOnlyList<Player> members,
		PlayerGroupType teamType = PlayerGroupType.Group)
	{
		// Java parity: model/team/group/PlayerGroupService.createGroup stores PlayerGroup by id, then PlayerGroup.addMember sets Player.playerGroup.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);
		if (members.Count == 0)
			throw new ArgumentException("A group requires at least one member.", nameof(members));

		lock (_sync)
		{
			var runtimeMembers = CopyDistinctMembers(members);
			if (runtimeMembers.Count > PlayerGroupDescriptor.JavaMaxMemberCount)
				throw new InvalidOperationException("Player group exceeds Java max member count.");

			_membersByTeamId[teamId] = runtimeMembers;
			_descriptorsByTeamId[teamId] = PlayerGroupDescriptor.FromLeader(teamId, runtimeMembers[0].Player, teamType);
			return ApplySnapshot(teamId, runtimeMembers);
		}
	}

	public PlayerGroupSnapshot AddMember(int teamId, Player member)
	{
		// Java parity: model/team/group/PlayerGroupService.addPlayer delegates to PlayerGroup.addMember.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var runtimeMembers))
			{
				runtimeMembers = [];
				_membersByTeamId.Add(teamId, runtimeMembers);
				_descriptorsByTeamId[teamId] = PlayerGroupDescriptor.FromLeader(teamId, member);
			}

			if (runtimeMembers.Any(existing => existing.ObjectId == member.ObjectId))
				throw new InvalidOperationException("Team member is already added.");

			var descriptor = _descriptorsByTeamId[teamId];
			if (descriptor.IsFull(runtimeMembers.Count))
				throw new InvalidOperationException("Player group is full.");

			runtimeMembers.Add(new PlayerGroupMember(member));
			return ApplySnapshot(teamId, runtimeMembers);
		}
	}

	public PlayerGroupSnapshot? RemoveMember(Player member)
	{
		// Java parity: model/team/group/PlayerGroupService.removePlayer delegates to PlayerGroup.onRemoveMember, which clears Player.playerGroup.
		lock (_sync)
		{
			var teamId = member.CurrentGroupSnapshot?.TeamId
				?? (member.TeamMembership == PlayerTeamMembership.Group ? member.CurrentTeamId : 0);
			if (teamId == 0)
			{
				ClearGroup(member);
				return null;
			}

			if (!_membersByTeamId.TryGetValue(teamId, out var runtimeMembers))
			{
				ClearGroup(member);
				return null;
			}

			var removedCount = runtimeMembers.RemoveAll(existing => existing.ObjectId == member.ObjectId);
			if (removedCount == 0)
				throw new InvalidOperationException("Team member is already removed.");

			ClearGroup(member);

			if (runtimeMembers.Count == 0)
			{
				_membersByTeamId.Remove(teamId);
				_descriptorsByTeamId.Remove(teamId);
				return null;
			}

			if (_descriptorsByTeamId.TryGetValue(teamId, out var descriptor)
				&& descriptor.LeaderObjectId == member.ObjectId)
				_descriptorsByTeamId[teamId] = descriptor with { LeaderObjectId = runtimeMembers[0].ObjectId };

			return ApplySnapshot(teamId, runtimeMembers);
		}
	}

	public PlayerGroupDescriptor? GetDescriptor(int teamId)
	{
		// Java parity: model/team/group/PlayerGroup exposes getTeamId, getLeader, getTeamType, and getMaxMemberCount.
		lock (_sync)
			return _descriptorsByTeamId.GetValueOrDefault(teamId);
	}

	public bool HasMember(int teamId, int objectId)
	{
		// Java parity: model/team/GeneralTeam.hasMember.
		lock (_sync)
			return _membersByTeamId.TryGetValue(teamId, out var members)
				&& members.Any(member => member.ObjectId == objectId);
	}

	public PlayerGroupMember? GetMember(int teamId, int objectId)
	{
		// Java parity: model/team/GeneralTeam.getMember returns the stored TeamMember wrapper.
		lock (_sync)
			return _membersByTeamId.TryGetValue(teamId, out var members)
				? members.FirstOrDefault(member => member.ObjectId == objectId)
				: null;
	}

	public IReadOnlyList<int> GetMemberObjectIds(int teamId)
	{
		// Java parity: model/team/GeneralTeam.getMembers mapped to object ids for the snapshot bridge.
		lock (_sync)
			return _membersByTeamId.TryGetValue(teamId, out var members)
				? members.Select(member => member.ObjectId).ToArray()
				: Array.Empty<int>();
	}

	public bool IsLeader(int teamId, Player player)
	{
		// Java parity: model/team/GeneralTeam.isLeader compares against the leader's Player object.
		lock (_sync)
			return _descriptorsByTeamId.TryGetValue(teamId, out var descriptor)
				&& descriptor.LeaderObjectId == player.ObjectId;
	}

	public bool IsFull(int teamId)
	{
		// Java parity: model/team/GeneralTeam.isFull.
		lock (_sync)
			return _membersByTeamId.TryGetValue(teamId, out var members)
				&& _descriptorsByTeamId.TryGetValue(teamId, out var descriptor)
				&& descriptor.IsFull(members.Count);
	}

	public bool UpdateMemberLastOnlineTime(Player player, DateTimeOffset now)
	{
		// Java parity: model/team/group/PlayerGroupService.onPlayerLogout updates PlayerGroupMember.lastOnlineTime before PlayerDisconnectedEvent.
		lock (_sync)
		{
			var teamId = player.CurrentGroupSnapshot?.TeamId
				?? (player.TeamMembership == PlayerTeamMembership.Group ? player.CurrentTeamId : 0);
			if (teamId == 0 || !_membersByTeamId.TryGetValue(teamId, out var members))
				return false;

			var member = members.FirstOrDefault(candidate => candidate.ObjectId == player.ObjectId);
			if (member == null)
				return false;

			member.UpdateLastOnlineTime(now);
			return true;
		}
	}

	public bool TryReconnectMember(Player player)
	{
		return ReconnectMember(player).Reconnected;
	}

	public PlayerGroupReconnectResult ReconnectMember(Player player)
	{
		// Java parity: model/team/group/events/PlayerConnectedEvent replaces the stored PlayerGroupMember with the logging-in Player.
		lock (_sync)
		{
			foreach (var (teamId, members) in _membersByTeamId)
			{
				var index = members.FindIndex(member => member.ObjectId == player.ObjectId);
				if (index < 0)
					continue;

				var previousMember = members[index];
				ClearGroup(previousMember.Player);
				members[index] = new PlayerGroupMember(player);
				ApplySnapshot(teamId, members);
				return new PlayerGroupReconnectResult(true, CreateReconnectPacketPlan(teamId, player, members, _descriptorsByTeamId[teamId]));
			}

			return PlayerGroupReconnectResult.NotFound();
		}
	}

	public PlayerGroupSnapshot? Resolve(Player player)
	{
		// Java parity: model/gameobjects/player/Player.getPlayerGroup; registry-owned snapshots are attached to the player.
		return PlayerGroupSnapshotResolver.Resolve(player);
	}

	private static List<PlayerGroupMember> CopyDistinctMembers(IReadOnlyList<Player> members)
	{
		var runtimeMembers = new List<PlayerGroupMember>(members.Count);
		var seenObjectIds = new HashSet<int>();
		foreach (var member in members)
		{
			if (!seenObjectIds.Add(member.ObjectId))
				throw new ArgumentException($"Duplicate group member object id {member.ObjectId}.", nameof(members));

			runtimeMembers.Add(new PlayerGroupMember(member));
		}

		return runtimeMembers;
	}

	private static PlayerGroupSnapshot ApplySnapshot(int teamId, IReadOnlyList<PlayerGroupMember> members)
	{
		var snapshot = PlayerGroupSnapshot.FromMembers(teamId, members.Select(member => member.Player).ToArray());
		foreach (var member in members)
		{
			member.Player.TeamMembership = PlayerTeamMembership.Group;
			member.Player.CurrentTeamId = snapshot.TeamId;
			member.Player.CurrentTeamMemberObjectIds = snapshot.MemberObjectIds;
			member.Player.CurrentGroupSnapshot = snapshot;
		}

		return snapshot;
	}

	private static PlayerGroupReconnectPacketPlan CreateReconnectPacketPlan(
		int teamId,
		Player reconnectingPlayer,
		IReadOnlyList<PlayerGroupMember> members,
		PlayerGroupDescriptor descriptor)
	{
		// Java parity: model/team/group/events/PlayerConnectedEvent sends SM_GROUP_INFO and SM_GROUP_MEMBER_INFO JOIN/ENTER packets.
		var reconnectingPlayerObjectId = reconnectingPlayer.ObjectId;
		var intents = new List<PlayerGroupMemberInfoIntent>
		{
			new(reconnectingPlayerObjectId, reconnectingPlayerObjectId, PlayerGroupEvent.Join),
		};

		foreach (var member in members)
		{
			if (member.ObjectId == reconnectingPlayerObjectId)
				continue;

			intents.Add(new PlayerGroupMemberInfoIntent(member.ObjectId, reconnectingPlayerObjectId, PlayerGroupEvent.Enter));
			intents.Add(new PlayerGroupMemberInfoIntent(reconnectingPlayerObjectId, member.ObjectId, PlayerGroupEvent.Enter));
		}

		return new PlayerGroupReconnectPacketPlan(
			teamId,
			reconnectingPlayerObjectId,
			SendGroupInfoToReconnectingPlayer: true,
			intents,
			PlayerGroupInfoPacketPlan.FromDescriptor(descriptor, reconnectingPlayer.Position.WorldId));
	}

	private static void ClearGroup(Player member)
	{
		member.TeamMembership = PlayerTeamMembership.None;
		member.CurrentTeamId = 0;
		member.CurrentTeamMemberObjectIds = Array.Empty<int>();
		member.CurrentGroupSnapshot = null;
	}
}
