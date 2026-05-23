using Aion.GameServer.Model.GameObjects;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, List<PlayerAllianceMember>> _membersByAllianceId = [];
	private readonly Dictionary<int, PlayerAllianceDescriptor> _descriptorsByAllianceId = [];
	private readonly Dictionary<int, List<int>> _viceCaptainObjectIdsByAllianceId = [];

	public PlayerAllianceSnapshot CreateAlliance(
		int allianceId,
		Player leader,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		PlayerGroupLootRules? lootRules = null)
	{
		// Java parity: model/team/alliance/PlayerAlliance constructor creates groups 1000..1003, then addMember places leader in first open group.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (_membersByAllianceId.ContainsKey(allianceId))
				throw new InvalidOperationException("Player alliance already exists.");

			var descriptor = PlayerAllianceDescriptor.FromLeader(allianceId, leader, teamType, lootRules);
			var members = new List<PlayerAllianceMember>
			{
				new(leader, allianceId, descriptor.AllianceGroupIds[0]),
			};
			_membersByAllianceId[allianceId] = members;
			_descriptorsByAllianceId[allianceId] = descriptor;
			_viceCaptainObjectIdsByAllianceId[allianceId] = [];

			return ApplySnapshot(allianceId, members, descriptor);
		}
	}

	public PlayerAllianceSnapshot AddMember(int allianceId, Player member)
	{
		// Java parity: model/team/alliance/PlayerAlliance.addMember delegates to getOpenAllianceGroup().addMember(member).
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
				throw new InvalidOperationException("Player alliance does not exist.");

			if (members.Any(existing => existing.ObjectId == member.ObjectId))
				throw new InvalidOperationException("Alliance member is already added.");

			if (descriptor.IsFull(members.Count))
				throw new InvalidOperationException("Player alliance is full.");

			var allianceGroupId = GetOpenAllianceGroupId(members, descriptor);
			members.Add(new PlayerAllianceMember(member, allianceId, allianceGroupId));
			return ApplySnapshot(allianceId, members, descriptor);
		}
	}

	public PlayerAllianceSnapshot? RemoveMember(Player member)
	{
		// Java parity: model/team/alliance/PlayerAlliance.onRemoveMember delegates to PlayerAllianceGroup.removeMember, which clears group membership.
		lock (_sync)
		{
			var allianceId = member.CurrentAllianceSnapshot?.AllianceId
				?? (member.TeamMembership == PlayerTeamMembership.Alliance ? member.CurrentTeamId : 0);
			if (allianceId == 0)
			{
				ClearAlliance(member);
				return null;
			}

			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
			{
				ClearAlliance(member);
				return null;
			}

			var removedMember = members.FirstOrDefault(existing => existing.ObjectId == member.ObjectId);
			if (removedMember == null)
				throw new InvalidOperationException("Alliance member is already removed.");

			members.Remove(removedMember);
			removedMember.ClearAllianceGroup();
			ClearAlliance(member);

			if (_viceCaptainObjectIdsByAllianceId.TryGetValue(allianceId, out var viceCaptainIds))
				viceCaptainIds.RemoveAll(objectId => objectId == member.ObjectId);

			if (members.Count == 0)
			{
				_membersByAllianceId.Remove(allianceId);
				_descriptorsByAllianceId.Remove(allianceId);
				_viceCaptainObjectIdsByAllianceId.Remove(allianceId);
				return null;
			}

			if (descriptor.LeaderObjectId == member.ObjectId)
			{
				descriptor = descriptor with { LeaderObjectId = members[0].ObjectId };
				_descriptorsByAllianceId[allianceId] = descriptor;
			}

			return ApplySnapshot(allianceId, members, descriptor);
		}
	}

	public PlayerAllianceSnapshot? GetSnapshot(int allianceId)
	{
		// Java parity: model/team/alliance/PlayerAlliance exposes getMembers, getViceCaptainIds, getLeader, and getLootGroupRules.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				&& _descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor)
					? CreateSnapshot(allianceId, members, descriptor)
					: null;
	}

	public PlayerAllianceDescriptor? GetDescriptor(int allianceId)
	{
		// Java parity: model/team/alliance/PlayerAlliance exposes object id, leader, team type, and loot rules.
		lock (_sync)
			return _descriptorsByAllianceId.GetValueOrDefault(allianceId);
	}

	public bool HasMember(int allianceId, int objectId)
	{
		// Java parity: model/team/GeneralTeam.hasMember.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				&& members.Any(member => member.ObjectId == objectId);
	}

	public PlayerAllianceMember? GetMember(int allianceId, int objectId)
	{
		// Java parity: model/team/GeneralTeam.getMember returns the stored TeamMember wrapper.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				? members.FirstOrDefault(member => member.ObjectId == objectId)
				: null;
	}

	public IReadOnlyList<int> GetMemberObjectIds(int allianceId)
	{
		// Java parity: model/team/GeneralTeam.getMembers mapped to object ids for the snapshot bridge.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				? members.Select(member => member.ObjectId).ToArray()
				: Array.Empty<int>();
	}

	public IReadOnlyList<int> GetMemberObjectIdsByGroupId(int allianceId, int allianceGroupId)
	{
		// Java parity: model/team/alliance/PlayerAlliance.getAllianceGroup(groupId).getMembers.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				? members.Where(member => member.AllianceGroupId == allianceGroupId).Select(member => member.ObjectId).ToArray()
				: Array.Empty<int>();
	}

	public bool IsLeader(int allianceId, Player player)
	{
		// Java parity: model/team/GeneralTeam.isLeader compares against the leader's Player object.
		lock (_sync)
			return _descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor)
				&& descriptor.LeaderObjectId == player.ObjectId;
	}

	public bool IsFull(int allianceId)
	{
		// Java parity: model/team/GeneralTeam.isFull for PlayerAlliance max member count.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				&& _descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor)
				&& descriptor.IsFull(members.Count);
	}

	public bool IsViceCaptain(int allianceId, int objectId)
	{
		// Java parity: model/team/alliance/PlayerAlliance.isViceCaptain.
		lock (_sync)
			return _viceCaptainObjectIdsByAllianceId.TryGetValue(allianceId, out var viceCaptainIds)
				&& viceCaptainIds.Contains(objectId);
	}

	public PlayerAllianceSnapshot? SetViceCaptains(int allianceId, IReadOnlyList<int> viceCaptainObjectIds)
	{
		// Java parity: model/team/alliance/PlayerAlliance.getViceCaptainIds returns the mutable vice-captain collection.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
				return null;

			_viceCaptainObjectIdsByAllianceId[allianceId] = viceCaptainObjectIds
				.Where(objectId => members.Any(member => member.ObjectId == objectId))
				.Distinct()
				.Take(4)
				.ToList();

			return ApplySnapshot(allianceId, members, descriptor);
		}
	}

	public PlayerAllianceSnapshot? Resolve(Player player)
	{
		// Java parity: model/gameobjects/player/Player.getPlayerAlliance; registry-owned snapshots are attached to the player.
		return player.TeamMembership == PlayerTeamMembership.Alliance ? player.CurrentAllianceSnapshot : null;
	}

	private static int GetOpenAllianceGroupId(IReadOnlyList<PlayerAllianceMember> members, PlayerAllianceDescriptor descriptor)
	{
		foreach (var allianceGroupId in descriptor.AllianceGroupIds)
		{
			if (members.Count(member => member.AllianceGroupId == allianceGroupId) < descriptor.MaxGroupMemberCount)
				return allianceGroupId;
		}

		throw new InvalidOperationException("Player alliance has no open Java alliance group.");
	}

	private PlayerAllianceSnapshot ApplySnapshot(
		int allianceId,
		IReadOnlyList<PlayerAllianceMember> members,
		PlayerAllianceDescriptor descriptor)
	{
		var snapshot = CreateSnapshot(allianceId, members, descriptor);
		foreach (var member in members)
		{
			member.Player.TeamMembership = PlayerTeamMembership.Alliance;
			member.Player.CurrentTeamId = snapshot.AllianceId;
			member.Player.CurrentTeamMemberObjectIds = snapshot.MemberObjectIds;
			member.Player.CurrentAllianceSnapshot = snapshot;
			member.Player.CurrentGroupSnapshot = null;
		}

		return snapshot;
	}

	private PlayerAllianceSnapshot CreateSnapshot(
		int allianceId,
		IReadOnlyList<PlayerAllianceMember> members,
		PlayerAllianceDescriptor descriptor)
	{
		var memberObjectIdsByGroupId = descriptor.AllianceGroupIds.ToDictionary(
			groupId => groupId,
			groupId => (IReadOnlyList<int>)members
				.Where(member => member.AllianceGroupId == groupId)
				.Select(member => member.ObjectId)
				.ToArray());

		var viceCaptainIds = _viceCaptainObjectIdsByAllianceId.TryGetValue(allianceId, out var currentViceCaptains)
			? currentViceCaptains.ToArray()
			: Array.Empty<int>();

		return new PlayerAllianceSnapshot(
			allianceId,
			descriptor.LeaderObjectId,
			members.Select(member => member.ObjectId).ToArray(),
			memberObjectIdsByGroupId,
			viceCaptainIds,
			descriptor.TeamType,
			descriptor.LootRules);
	}

	private static void ClearAlliance(Player member)
	{
		member.TeamMembership = PlayerTeamMembership.None;
		member.CurrentTeamId = 0;
		member.CurrentTeamMemberObjectIds = Array.Empty<int>();
		member.CurrentAllianceSnapshot = null;
	}
}
