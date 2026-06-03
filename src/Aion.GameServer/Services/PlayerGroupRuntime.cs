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
	private readonly PlayerBaseLeavePlanner _baseLeavePlanner = new();
	private readonly FindGroupRecruitmentPlanService? _findGroupService;
	private readonly byte _serverId;

	public PlayerGroupRuntime(FindGroupRecruitmentPlanService? findGroupService = null, byte serverId = 0)
	{
		_findGroupService = findGroupService;
		_serverId = serverId;
	}

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

	public PlayerGroupMemberInfoUpdatePlan? CreateMemberInfoUpdatePlan(
		int teamId,
		Player player,
		PlayerGroupEvent groupEvent,
		int slot = 0)
	{
		// Java parity: model/team/group/events/PlayerGroupUpdateEvent sends SM_GROUP_MEMBER_INFO to Predicates.Players.allExcept(player).
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var members))
				return null;

			var subjectMember = members.FirstOrDefault(member => member.ObjectId == player.ObjectId);
			if (subjectMember == null)
				return null;

			var packetPlan = PlayerGroupMemberInfoPacketPlan.FromMember(teamId, subjectMember, groupEvent, slot);
			var intents = members
				.Where(member => member.ObjectId != player.ObjectId)
				.Select(member => new PlayerGroupMemberInfoIntent(
					member.ObjectId,
					player.ObjectId,
					groupEvent,
					packetPlan))
				.ToArray();

			return new PlayerGroupMemberInfoUpdatePlan(teamId, player.ObjectId, groupEvent, slot, intents);
		}
	}

	public PlayerGroupMentorStatusChangePlan? CreateMentorStatusChangePlan(
		int teamId,
		Player player,
		bool isMentor)
	{
		// Java parity: model/team/group/events/PlayerStartMentoringEvent and PlayerGroupStopMentoringEvent.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var members))
				return null;

			var mentorMember = members.FirstOrDefault(member => member.ObjectId == player.ObjectId);
			if (mentorMember == null)
				return null;

			if (isMentor && !members.Any(member => member.ObjectId != player.ObjectId && member.Player.Level + 10 <= player.Level))
				return null;

			player.IsMentor = isMentor;
			var packetPlan = PlayerGroupMemberInfoPacketPlan.FromMember(teamId, mentorMember, PlayerGroupEvent.Movement);
			var systemMessages = CreateMentorSystemMessageIntents(player, members, isMentor);
			var memberInfoIntents = members
				.Select(member => new PlayerGroupMemberInfoIntent(
					member.ObjectId,
					player.ObjectId,
					PlayerGroupEvent.Movement,
					packetPlan))
				.ToArray();

			return new PlayerGroupMentorStatusChangePlan(
				teamId,
				player.ObjectId,
				isMentor,
				systemMessages,
				memberInfoIntents,
				new PlayerGroupMentorAbyssRankUpdateIntent(player.ObjectId, isMentor));
		}
	}

	public PlayerGroupLeaderChangePlan? ChangeLeader(int teamId, int newLeaderObjectId)
	{
		// Java parity: model/team/group/events/ChangeGroupLeaderEvent.changeLeaderTo.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var members)
				|| !_descriptorsByTeamId.TryGetValue(teamId, out var descriptor))
				return null;

			return ChangeLeaderCore(teamId, members, descriptor, newLeaderObjectId);
		}
	}

	public PlayerGroupLeavePlan? RemoveMemberWithLeavePlan(
		Player member,
		PlayerGroupLeaveReason reason = PlayerGroupLeaveReason.Leave,
		string banPersonName = "")
	{
		// Java parity: model/team/group/events/PlayerGroupLeavedEvent removes the member, fanouts leave packets, and then runs base PlayerLeavedEvent.
		lock (_sync)
		{
			var teamId = member.CurrentGroupSnapshot?.TeamId
				?? (member.TeamMembership == PlayerTeamMembership.Group ? member.CurrentTeamId : 0);
			if (teamId == 0)
			{
				ClearGroup(member);
				return null;
			}

			if (!_membersByTeamId.TryGetValue(teamId, out var runtimeMembers)
				|| !_descriptorsByTeamId.TryGetValue(teamId, out var descriptor))
			{
				ClearGroup(member);
				return null;
			}

			var leavedMember = runtimeMembers.FirstOrDefault(existing => existing.ObjectId == member.ObjectId);
			if (leavedMember == null)
				throw new InvalidOperationException("Team member is already removed.");

			var wasLeader = descriptor.LeaderObjectId == member.ObjectId;
			var wasMentor = member.IsMentor;
			var leavePacketPlan = PlayerGroupMemberInfoPacketPlan.FromMember(teamId, leavedMember, PlayerGroupEvent.Leave);
			runtimeMembers.Remove(leavedMember);
			ClearGroup(member);

			var wouldDisband = descriptor.TeamType != PlayerGroupType.AutoGroup && runtimeMembers.Count == 1;
			var packetIntents = CreateGroupLeavePacketIntents(runtimeMembers, leavePacketPlan, member.Name, reason, banPersonName);
			PlayerGroupLeaderChangePlan? leaderChangePlan = null;
			FindGroupRecruitmentMutationPlan? findGroupRecruitmentRemoval = null;

			if (runtimeMembers.Count == 0)
			{
				_membersByTeamId.Remove(teamId);
				_descriptorsByTeamId.Remove(teamId);
				_targetObjectIdsByBrandIdByTeamId.Remove(teamId);
			}
			else if (wouldDisband)
			{
				// Java parity: PlayerGroupService.disband calls FindGroupService.removeRecruitment(group)
				// before removing the group and replaying disband leave packets.
				findGroupRecruitmentRemoval = _findGroupService?.RemoveRecruitment(teamId, _serverId, unknown1: 0, unknown2: 0, unknown3: 0);
				// Java parity: GroupDisbandEvent replays PlayerGroupLeavedEvent with DISBAND for the last member before the original base leave packet.
				AppendGroupDisbandPacketIntents(packetIntents, runtimeMembers);
				foreach (var remainingMember in runtimeMembers)
					ClearGroup(remainingMember.Player);
				_membersByTeamId.Remove(teamId);
				_descriptorsByTeamId.Remove(teamId);
				_targetObjectIdsByBrandIdByTeamId.Remove(teamId);
			}
			else
			{
				if (wasLeader)
				{
					var fallbackLeader = runtimeMembers.FirstOrDefault(candidate => candidate.IsOnline) ?? runtimeMembers[0];
					leaderChangePlan = ChangeLeaderCore(teamId, runtimeMembers, descriptor, fallbackLeader.ObjectId);
				}
				else
				{
					ApplySnapshot(teamId, runtimeMembers);
				}
			}

			var baseLeavePlan = _baseLeavePlanner.CreateLeaveSideEffectPlan(
				member.ObjectId,
				member.IsOnline,
				wasRegisteredToTeamInstance: false);

			return new PlayerGroupLeavePlan(
				teamId,
				member.ObjectId,
				reason,
				packetIntents,
				baseLeavePlan,
				leaderChangePlan,
				wouldDisband,
				wasMentor,
				baseLeavePlan.WouldNotifyEventServiceOnLeftTeam,
				findGroupRecruitmentRemoval);
		}
	}

	public PlayerGroupDisconnectedDisbandPlan? DisbandAfterDisconnectedNoOnlineMembers(int teamId)
	{
		// Java parity: PlayerDisconnectedEvent calls PlayerGroupService.disband(group) only
		// when group.getOnlineMembers().isEmpty().
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(teamId, 0);

		lock (_sync)
		{
			if (!_membersByTeamId.TryGetValue(teamId, out var runtimeMembers)
				|| !_descriptorsByTeamId.ContainsKey(teamId)
				|| runtimeMembers.Any(member => member.IsOnline))
				return null;

			var members = runtimeMembers.ToArray();
			var findGroupRecruitmentRemoval = _findGroupService?.RemoveRecruitment(
				teamId,
				_serverId,
				unknown1: 0,
				unknown2: 0,
				unknown3: 0);
			var baseLeavePlans = members
				.Select(member => _baseLeavePlanner.CreateLeaveSideEffectPlan(
					member.ObjectId,
					member.IsOnline,
					wasRegisteredToTeamInstance: false))
				.ToArray();

			foreach (var member in members)
				ClearGroup(member.Player);
			_membersByTeamId.Remove(teamId);
			_descriptorsByTeamId.Remove(teamId);
			_targetObjectIdsByBrandIdByTeamId.Remove(teamId);

			return new PlayerGroupDisconnectedDisbandPlan(
				teamId,
				members.Select(member => member.ObjectId).ToArray(),
				findGroupRecruitmentRemoval,
				baseLeavePlans,
				RemovedRuntimeGroup: true,
				"PlayerDisconnectedEvent -> PlayerGroupService.disband removes FindGroup recruitment, removes the group map entry, then GroupDisbandEvent replays PlayerGroupLeavedEvent(DISBAND) for every offline member");
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

	public IReadOnlyList<Player> GetMemberPlayers(int teamId)
	{
		// Java parity: model/team/GeneralTeam.getMembers exposes the current Player objects for requirement checks.
		lock (_sync)
			return _membersByTeamId.TryGetValue(teamId, out var members)
				? members.Select(member => member.Player).ToArray()
				: Array.Empty<Player>();
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

	private PlayerGroupLeaderChangePlan? ChangeLeaderCore(
		int teamId,
		IReadOnlyList<PlayerGroupMember> members,
		PlayerGroupDescriptor descriptor,
		int newLeaderObjectId)
	{
		var newLeader = members.FirstOrDefault(member => member.ObjectId == newLeaderObjectId);
		if (newLeader == null)
			return null;

		var updatedDescriptor = descriptor with { LeaderObjectId = newLeaderObjectId };
		_descriptorsByTeamId[teamId] = updatedDescriptor;
		ApplySnapshot(teamId, members);

		var sequence = 0;
		var intents = members
			.Select(member => new PlayerGroupLeaderChangePacketIntent(
				sequence++,
				member.ObjectId,
				PlayerGroupInfoPacketPlan.FromDescriptor(updatedDescriptor, member.Player.Position.WorldId),
				member.ObjectId == newLeaderObjectId
					? SmSystemMessage.PartyYouBecomeNewLeader()
					: SmSystemMessage.PartyHeIsNewLeader(newLeader.Player.Name)))
			.ToArray();

		return new PlayerGroupLeaderChangePlan(teamId, newLeaderObjectId, intents);
	}

	private static List<PlayerGroupLeavePacketIntent> CreateGroupLeavePacketIntents(
		IReadOnlyList<PlayerGroupMember> remainingMembers,
		PlayerGroupMemberInfoPacketPlan leavePacketPlan,
		string leavedPlayerName,
		PlayerGroupLeaveReason reason,
		string banPersonName)
	{
		var intents = new List<PlayerGroupLeavePacketIntent>();
		var sequence = 0;
		foreach (var remainingMember in remainingMembers)
		{
			intents.Add(new PlayerGroupLeavePacketIntent(
				sequence++,
				remainingMember.ObjectId,
				PlayerGroupLeavePacketIntentKind.MemberInfo,
				leavePacketPlan));
			intents.Add(new PlayerGroupLeavePacketIntent(
				sequence++,
				remainingMember.ObjectId,
				PlayerGroupLeavePacketIntentKind.SystemMessage,
				SystemMessage: CreateLeaveMessage(reason, leavedPlayerName, banPersonName)));
		}

		return intents;
	}

	private static void AppendGroupDisbandPacketIntents(
		List<PlayerGroupLeavePacketIntent> intents,
		IReadOnlyList<PlayerGroupMember> remainingMembers)
	{
		var sequence = intents.Count == 0 ? 0 : intents.Max(intent => intent.Sequence) + 1;
		foreach (var remainingMember in remainingMembers)
		{
			intents.Add(new PlayerGroupLeavePacketIntent(
				sequence++,
				remainingMember.ObjectId,
				PlayerGroupLeavePacketIntentKind.SystemMessage,
				SystemMessage: SmSystemMessage.PartyIsDispersed()));

			if (!remainingMember.IsOnline)
				continue;

			intents.Add(new PlayerGroupLeavePacketIntent(
				sequence++,
				remainingMember.ObjectId,
				PlayerGroupLeavePacketIntentKind.LeaveGroupMember));
		}
	}

	private static SmSystemMessage CreateLeaveMessage(
		PlayerGroupLeaveReason reason,
		string leavedPlayerName,
		string banPersonName)
	{
		return reason switch
		{
			PlayerGroupLeaveReason.Leave => SmSystemMessage.PartyHeLeaveParty(leavedPlayerName),
			PlayerGroupLeaveReason.Ban => SmSystemMessage.PartyHeIsBanished(leavedPlayerName),
			PlayerGroupLeaveReason.Disband => SmSystemMessage.PartyIsDispersed(),
			PlayerGroupLeaveReason.LeaveTimeout => SmSystemMessage.PartyHeBecomeOfflineTimeout(leavedPlayerName),
			_ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unsupported group leave reason."),
		};
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

	private static IReadOnlyList<PlayerGroupSystemMessageIntent> CreateMentorSystemMessageIntents(
		Player player,
		IReadOnlyList<PlayerGroupMember> members,
		bool isMentor)
	{
		// Java parity: mentoring events send a self message, then party messages to every other group member.
		var playerObjectId = player.ObjectId;
		var intents = new List<PlayerGroupSystemMessageIntent>
		{
			new(playerObjectId, isMentor ? SmSystemMessage.MentorStart() : SmSystemMessage.MentorEnd()),
		};

		foreach (var member in members)
		{
			if (member.ObjectId == playerObjectId)
				continue;

			intents.Add(new PlayerGroupSystemMessageIntent(
				member.ObjectId,
				isMentor
					? SmSystemMessage.MentorStartPartyMessage(player.Name)
					: SmSystemMessage.MentorEndPartyMessage(player.Name)));
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
