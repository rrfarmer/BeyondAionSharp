using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, List<PlayerGroupMember>> _membersByTeamId = [];
	private readonly Dictionary<int, PlayerGroupDescriptor> _descriptorsByTeamId = [];
	private readonly Dictionary<int, Dictionary<int, int>> _targetObjectIdsByBrandIdByTeamId = [];

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
			_targetObjectIdsByBrandIdByTeamId.TryAdd(teamId, []);
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
				_targetObjectIdsByBrandIdByTeamId.TryAdd(teamId, []);
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

	public PlayerGroupEnteredPacketPlan? CreateEnteredPacketPlan(int teamId, Player enteringPlayer)
	{
		// Java parity: model/team/group/events/PlayerGroupEnteredEvent sends SM_GROUP_INFO after PlayerGroupService.addPlayerToGroup.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var members)
				|| !_descriptorsByTeamId.TryGetValue(teamId, out var descriptor)
				|| members.All(member => member.ObjectId != enteringPlayer.ObjectId))
				return null;

			return new PlayerGroupEnteredPacketPlan(
				teamId,
				enteringPlayer.ObjectId,
				SendGroupInfoToEnteringPlayer: true,
				PlayerGroupInfoPacketPlan.FromDescriptor(descriptor, enteringPlayer.Position.WorldId),
				CreateEnteredSystemMessageIntents(enteringPlayer, members),
				CreateEnteredMemberInfoIntents(teamId, enteringPlayer, members),
				new PlayerGroupBrandIntent(enteringPlayer.ObjectId, GetBrandSnapshot(teamId)),
				new PlayerGroupAbyssRankUpdateIntent(enteringPlayer.ObjectId, teamId, IncludeSelf: true));
		}
	}

	public PlayerGroupBrandUpdatePlan? UpdateBrand(int teamId, int brandId, int targetObjectId)
	{
		// Java parity: model/team/TemporaryPlayerTeam.updateBrand stores target id and broadcasts SM_SHOW_BRAND.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var members))
				return null;

			if (!_targetObjectIdsByBrandIdByTeamId.TryGetValue(teamId, out var targetObjectIdsByBrandId))
			{
				targetObjectIdsByBrandId = [];
				_targetObjectIdsByBrandIdByTeamId[teamId] = targetObjectIdsByBrandId;
			}

			targetObjectIdsByBrandId[brandId] = targetObjectId;
			var intents = members
				.Select(member => new PlayerGroupBrandIntent(member.ObjectId, new Dictionary<int, int> { [brandId] = targetObjectId }))
				.ToArray();

			return new PlayerGroupBrandUpdatePlan(teamId, brandId, targetObjectId, intents);
		}
	}

	public PlayerGroupLootRulesChangedPacketPlan? ChangeLootRules(int teamId, PlayerGroupLootRules lootRules)
	{
		// Java parity: model/team/group/events/ChangeGroupLootRulesEvent sets LootGroupRules and broadcasts SM_GROUP_INFO.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var members)
				|| !_descriptorsByTeamId.TryGetValue(teamId, out var descriptor))
				return null;

			var updatedDescriptor = descriptor with { LootRules = lootRules };
			_descriptorsByTeamId[teamId] = updatedDescriptor;
			var broadcasts = members
				.Select(member => new PlayerGroupInfoBroadcastIntent(
					member.ObjectId,
					PlayerGroupInfoPacketPlan.FromDescriptor(updatedDescriptor, member.Player.Position.WorldId)))
				.ToArray();

			return new PlayerGroupLootRulesChangedPacketPlan(teamId, broadcasts);
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
				_targetObjectIdsByBrandIdByTeamId.Remove(teamId);
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

	private static IReadOnlyList<PlayerGroupSystemMessageIntent> CreateEnteredSystemMessageIntents(
		Player enteringPlayer,
		IReadOnlyList<PlayerGroupMember> members)
	{
		// Java parity: model/team/group/events/PlayerGroupEnteredEvent sends STR_PARTY_ENTERED_PARTY and STR_PARTY_HE_ENTERED_PARTY.
		var enteringPlayerObjectId = enteringPlayer.ObjectId;
		var intents = new List<PlayerGroupSystemMessageIntent>
		{
			new(enteringPlayerObjectId, SmSystemMessage.PartyEnteredParty()),
		};

		foreach (var member in members)
		{
			if (member.ObjectId == enteringPlayerObjectId)
				continue;

			intents.Add(new PlayerGroupSystemMessageIntent(member.ObjectId, SmSystemMessage.PartyHeEnteredParty(enteringPlayer.Name)));
		}

		return intents;
	}

	private static IReadOnlyList<PlayerGroupMemberInfoIntent> CreateEnteredMemberInfoIntents(
		int teamId,
		Player enteringPlayer,
		IReadOnlyList<PlayerGroupMember> members)
	{
		// Java parity: model/team/group/events/PlayerGroupEnteredEvent sends JOIN to entering player and ENTER pairs with existing members.
		var enteringPlayerObjectId = enteringPlayer.ObjectId;
		var enteringMember = members.First(member => member.ObjectId == enteringPlayerObjectId);
		var intents = new List<PlayerGroupMemberInfoIntent>
		{
			new(
				enteringPlayerObjectId,
				enteringPlayerObjectId,
				PlayerGroupEvent.Join,
				PlayerGroupMemberInfoPacketPlan.FromMember(teamId, enteringMember, PlayerGroupEvent.Join)),
		};

		foreach (var member in members)
		{
			if (member.ObjectId == enteringPlayerObjectId)
				continue;

			intents.Add(new PlayerGroupMemberInfoIntent(
				member.ObjectId,
				enteringPlayerObjectId,
				PlayerGroupEvent.Enter,
				PlayerGroupMemberInfoPacketPlan.FromMember(teamId, enteringMember, PlayerGroupEvent.Enter)));
			intents.Add(new PlayerGroupMemberInfoIntent(
				enteringPlayerObjectId,
				member.ObjectId,
				PlayerGroupEvent.Enter,
				PlayerGroupMemberInfoPacketPlan.FromMember(teamId, member, PlayerGroupEvent.Enter)));
		}

		return intents;
	}

	private IReadOnlyDictionary<int, int> GetBrandSnapshot(int teamId)
	{
		return _targetObjectIdsByBrandIdByTeamId.TryGetValue(teamId, out var targetObjectIdsByBrandId)
			? new Dictionary<int, int>(targetObjectIdsByBrandId)
			: new Dictionary<int, int>();
	}

	private static PlayerGroupReconnectPacketPlan CreateReconnectPacketPlan(
		int teamId,
		Player reconnectingPlayer,
		IReadOnlyList<PlayerGroupMember> members,
		PlayerGroupDescriptor descriptor)
	{
		// Java parity: model/team/group/events/PlayerConnectedEvent sends SM_GROUP_INFO and SM_GROUP_MEMBER_INFO JOIN/ENTER packets.
		var reconnectingPlayerObjectId = reconnectingPlayer.ObjectId;
		var membersByObjectId = members.ToDictionary(member => member.ObjectId);
		var reconnectingMember = membersByObjectId[reconnectingPlayerObjectId];
		var intents = new List<PlayerGroupMemberInfoIntent>
		{
			new(
				reconnectingPlayerObjectId,
				reconnectingPlayerObjectId,
				PlayerGroupEvent.Join,
				PlayerGroupMemberInfoPacketPlan.FromMember(teamId, reconnectingMember, PlayerGroupEvent.Join)),
		};

		foreach (var member in members)
		{
			if (member.ObjectId == reconnectingPlayerObjectId)
				continue;

			intents.Add(new PlayerGroupMemberInfoIntent(
				member.ObjectId,
				reconnectingPlayerObjectId,
				PlayerGroupEvent.Enter,
				PlayerGroupMemberInfoPacketPlan.FromMember(teamId, reconnectingMember, PlayerGroupEvent.Enter)));
			intents.Add(new PlayerGroupMemberInfoIntent(
				reconnectingPlayerObjectId,
				member.ObjectId,
				PlayerGroupEvent.Enter,
				PlayerGroupMemberInfoPacketPlan.FromMember(teamId, member, PlayerGroupEvent.Enter)));
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
