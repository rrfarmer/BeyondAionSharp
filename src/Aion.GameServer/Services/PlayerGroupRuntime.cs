using Aion.GameServer.Model.GameObjects;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, List<Player>> _membersByTeamId = [];

	public PlayerGroupSnapshot CreateOrUpdateGroup(int teamId, IReadOnlyList<Player> members)
	{
		// Java parity: model/team/group/PlayerGroupService.createGroup stores PlayerGroup by id, then PlayerGroup.addMember sets Player.playerGroup.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);
		if (members.Count == 0)
			throw new ArgumentException("A group requires at least one member.", nameof(members));

		lock (_sync)
		{
			var runtimeMembers = CopyDistinctMembers(members);
			_membersByTeamId[teamId] = runtimeMembers;
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
			}

			if (runtimeMembers.Any(existing => existing.ObjectId == member.ObjectId))
				return ApplySnapshot(teamId, runtimeMembers);

			runtimeMembers.Add(member);
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
			if (teamId == 0 || !_membersByTeamId.TryGetValue(teamId, out var runtimeMembers))
			{
				ClearGroup(member);
				return null;
			}

			runtimeMembers.RemoveAll(existing => existing.ObjectId == member.ObjectId);
			ClearGroup(member);

			if (runtimeMembers.Count == 0)
			{
				_membersByTeamId.Remove(teamId);
				return null;
			}

			return ApplySnapshot(teamId, runtimeMembers);
		}
	}

	public PlayerGroupSnapshot? Resolve(Player player)
	{
		// Java parity: model/gameobjects/player/Player.getPlayerGroup; registry-owned snapshots are attached to the player.
		return PlayerGroupSnapshotResolver.Resolve(player);
	}

	private static List<Player> CopyDistinctMembers(IReadOnlyList<Player> members)
	{
		var runtimeMembers = new List<Player>(members.Count);
		var seenObjectIds = new HashSet<int>();
		foreach (var member in members)
		{
			if (!seenObjectIds.Add(member.ObjectId))
				throw new ArgumentException($"Duplicate group member object id {member.ObjectId}.", nameof(members));

			runtimeMembers.Add(member);
		}

		return runtimeMembers;
	}

	private static PlayerGroupSnapshot ApplySnapshot(int teamId, IReadOnlyList<Player> members)
	{
		var snapshot = PlayerGroupSnapshot.FromMembers(teamId, members);
		foreach (var member in members)
		{
			member.TeamMembership = PlayerTeamMembership.Group;
			member.CurrentTeamId = snapshot.TeamId;
			member.CurrentTeamMemberObjectIds = snapshot.MemberObjectIds;
			member.CurrentGroupSnapshot = snapshot;
		}

		return snapshot;
	}

	private static void ClearGroup(Player member)
	{
		member.TeamMembership = PlayerTeamMembership.None;
		member.CurrentTeamId = 0;
		member.CurrentTeamMemberObjectIds = Array.Empty<int>();
		member.CurrentGroupSnapshot = null;
	}
}
