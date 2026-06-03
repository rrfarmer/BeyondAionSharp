using Aion.GameServer.Model.GameObjects;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, List<PlayerAllianceMember>> _membersByAllianceId = [];
	private readonly Dictionary<int, PlayerAllianceDescriptor> _descriptorsByAllianceId = [];
	private readonly Dictionary<int, int> _leagueIdByAllianceId = [];
	private readonly Dictionary<int, List<int>> _viceCaptainObjectIdsByAllianceId = [];
	private readonly Dictionary<int, int> _allianceReadyStatusByAllianceId = [];
	private readonly Dictionary<int, Dictionary<int, int>> _targetObjectIdsByBrandIdByAllianceId = [];
	private readonly PlayerAllianceMemberGroupChangePlanner _groupChangePlanner = new();
	private readonly PlayerAllianceEnteredPlanner _enteredPlanner = new();
	private readonly PlayerAllianceViceCaptainAssignmentPlanner _viceCaptainAssignmentPlanner = new();
	private readonly PlayerAllianceLeaderChangePlanner _leaderChangePlanner = new();
	private readonly PlayerAllianceLeaveWorkflowPlanner _leaveWorkflowPlanner = new();
	private readonly FindGroupRecruitmentPlanService? _findGroupService;
	private readonly byte _serverId;

	public PlayerAllianceRuntime(FindGroupRecruitmentPlanService? findGroupService = null, byte serverId = 0)
	{
		_findGroupService = findGroupService;
		_serverId = serverId;
	}

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
			_leagueIdByAllianceId.Remove(allianceId);
			_viceCaptainObjectIdsByAllianceId[allianceId] = [];
			_allianceReadyStatusByAllianceId[allianceId] = 0;
			_targetObjectIdsByBrandIdByAllianceId[allianceId] = [];

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
				_leagueIdByAllianceId.Remove(allianceId);
				_viceCaptainObjectIdsByAllianceId.Remove(allianceId);
				_allianceReadyStatusByAllianceId.Remove(allianceId);
				_targetObjectIdsByBrandIdByAllianceId.Remove(allianceId);
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

	public PlayerAllianceLeaveWorkflowPlan? RemoveMemberWithLeaveWorkflow(
		Player member,
		PlayerAllianceLeaveReason reason = PlayerAllianceLeaveReason.Leave,
		string banPersonName = "")
	{
		// Java parity: model/team/alliance/events/PlayerAllianceLeavedEvent removes the member, fanouts alliance leave packets, then runs base PlayerLeavedEvent.
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

			var wasLeader = descriptor.LeaderObjectId == member.ObjectId;
			var currentViceCaptainIds = _viceCaptainObjectIdsByAllianceId.GetValueOrDefault(allianceId) ?? [];

			members.Remove(removedMember);
			removedMember.ClearAllianceGroup();
			ClearAlliance(member);

			var viceCaptainIdsAfterLeave = currentViceCaptainIds
				.Where(objectId => objectId != member.ObjectId)
				.ToList();
			_viceCaptainObjectIdsByAllianceId[allianceId] = viceCaptainIdsAfterLeave;

			var shouldDisband = descriptor.TeamType != PlayerAllianceTeamType.AutoAlliance && members.Count == 1;
			var plan = _leaveWorkflowPlanner.CreateLeaveWorkflowPlan(
				allianceId,
				descriptor.LeaderObjectId,
				members.Select(existing => existing.Player).ToArray(),
				member,
				currentViceCaptainIds,
				reason,
				banPersonName,
				descriptor.LootRules,
				descriptor.TeamType,
				wasLeader,
				shouldDisband,
				isInLeague: false,
				wasRegisteredToTeamInstance: false);

			if (members.Count == 0)
			{
				_membersByAllianceId.Remove(allianceId);
				_descriptorsByAllianceId.Remove(allianceId);
				_leagueIdByAllianceId.Remove(allianceId);
				_viceCaptainObjectIdsByAllianceId.Remove(allianceId);
				_allianceReadyStatusByAllianceId.Remove(allianceId);
				_targetObjectIdsByBrandIdByAllianceId.Remove(allianceId);
			}
			else if (plan.AllianceLeavePlan.WouldDisband)
			{
				// Java parity: PlayerAllianceService.disband calls FindGroupService.removeRecruitment(alliance)
				// before alliance disband events clear the remaining member state.
				var findGroupRecruitmentRemoval = _findGroupService?.RemoveRecruitment(allianceId, _serverId, unknown1: 0, unknown2: 0, unknown3: 0);
				foreach (var remainingMember in members)
				{
					remainingMember.ClearAllianceGroup();
					ClearAlliance(remainingMember.Player);
				}

				_membersByAllianceId.Remove(allianceId);
				_descriptorsByAllianceId.Remove(allianceId);
				_leagueIdByAllianceId.Remove(allianceId);
				_viceCaptainObjectIdsByAllianceId.Remove(allianceId);
				_allianceReadyStatusByAllianceId.Remove(allianceId);
				_targetObjectIdsByBrandIdByAllianceId.Remove(allianceId);
				plan = plan with { FindGroupRecruitmentRemoval = findGroupRecruitmentRemoval };
			}
			else
			{
				if (wasLeader)
				{
					descriptor = descriptor with { LeaderObjectId = members[0].ObjectId };
					_descriptorsByAllianceId[allianceId] = descriptor;
				}

				ApplySnapshot(allianceId, members, descriptor);
			}

			return plan;
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

	public PlayerAllianceSnapshot? SetLeagueId(int allianceId, int leagueId)
	{
		// Java parity: model/team/alliance/PlayerAlliance.setLeague updates the live League pointer used by PortalService.port.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);
		ArgumentOutOfRangeException.ThrowIfLessThan(leagueId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
				return null;

			if (leagueId == 0)
				_leagueIdByAllianceId.Remove(allianceId);
			else
				_leagueIdByAllianceId[allianceId] = leagueId;

			return ApplySnapshot(allianceId, members, descriptor);
		}
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

	public IReadOnlyList<Player> GetMemberPlayers(int allianceId)
	{
		// Java parity: model/team/GeneralTeam.getMembers exposes the current Player objects for requirement checks.
		lock (_sync)
			return _membersByAllianceId.TryGetValue(allianceId, out var members)
				? members.Select(member => member.Player).ToArray()
				: Array.Empty<Player>();
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

	public PlayerAllianceViceCaptainAssignmentPlan? AssignViceCaptain(
		int allianceId,
		int eventPlayerObjectId,
		PlayerAllianceAssignType assignType)
	{
		// Java parity: model/team/alliance/PlayerAllianceService.changeViceCaptain -> AssignViceCaptainEvent.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
				return null;

			var currentViceCaptainIds = _viceCaptainObjectIdsByAllianceId.GetValueOrDefault(allianceId) ?? [];
			var plan = _viceCaptainAssignmentPlanner.CreateAssignmentPlan(
				allianceId,
				descriptor.LeaderObjectId,
				members.Select(member => member.Player).ToArray(),
				currentViceCaptainIds,
				eventPlayerObjectId,
				assignType,
				descriptor.LootRules,
				descriptor.TeamType,
				isInLeague: false);

			if (plan.Status == PlayerAllianceRolePlanStatus.Planned)
			{
				_viceCaptainObjectIdsByAllianceId[allianceId] = plan.ViceCaptainObjectIdsAfterEvent.ToList();
				ApplySnapshot(allianceId, members, descriptor);
			}

			return plan;
		}
	}

	public PlayerAllianceLeaderChangePlan? ChangeLeader(
		int allianceId,
		int newLeaderObjectId,
		bool eventPlayerWasSpecified)
	{
		// Java parity: model/team/alliance/events/ChangeAllianceLeaderEvent.changeLeaderTo.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
				return null;

			if (members.All(member => member.ObjectId != newLeaderObjectId))
				return null;

			var currentViceCaptainIds = _viceCaptainObjectIdsByAllianceId.GetValueOrDefault(allianceId) ?? [];
			var plan = _leaderChangePlanner.CreateLeaderChangePlan(
				allianceId,
				descriptor.LeaderObjectId,
				members.Select(member => member.Player).ToArray(),
				currentViceCaptainIds,
				newLeaderObjectId,
				eventPlayerWasSpecified,
				descriptor.LootRules,
				descriptor.TeamType,
				isInLeague: false);

			var updatedDescriptor = descriptor with { LeaderObjectId = newLeaderObjectId };
			_descriptorsByAllianceId[allianceId] = updatedDescriptor;
			_viceCaptainObjectIdsByAllianceId[allianceId] = plan.ViceCaptainObjectIdsAfterEvent.ToList();
			ApplySnapshot(allianceId, members, updatedDescriptor);
			return plan;
		}
	}

	public int? SelectFallbackLeaderObjectId(int allianceId, int leavingLeaderObjectId)
	{
		// Java parity: model/team/alliance/events/ChangeAllianceLeaderEvent.handleEvent prefers an online vice captain, then the next online non-leader member.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members))
				return null;

			var viceCaptainObjectIds = _viceCaptainObjectIdsByAllianceId.GetValueOrDefault(allianceId) ?? [];
			foreach (var viceCaptainObjectId in viceCaptainObjectIds)
			{
				var viceCaptain = members.FirstOrDefault(member => member.ObjectId == viceCaptainObjectId);
				if (viceCaptain is { IsOnline: true } && viceCaptain.ObjectId != leavingLeaderObjectId)
					return viceCaptain.ObjectId;
			}

			return members
				.FirstOrDefault(member => member.IsOnline && member.ObjectId != leavingLeaderObjectId)
				?.ObjectId;
		}
	}

	public PlayerAllianceReadyCheckPlan? CheckReady(int allianceId, Player player, PlayerAllianceReadyCheckCommand command)
	{
		// Java parity: model/team/alliance/events/CheckAllianceReadyEvent updates allianceReadyStatus and broadcasts SM_ALLIANCE_READY_CHECK.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| members.All(member => member.ObjectId != player.ObjectId))
				return null;

			var readyStatusBefore = _allianceReadyStatusByAllianceId.GetValueOrDefault(allianceId);
			var readyStatusAfter = command switch
			{
				PlayerAllianceReadyCheckCommand.Cancel => 0,
				PlayerAllianceReadyCheckCommand.Start => members.Count(member => member.IsOnline) - 1,
				PlayerAllianceReadyCheckCommand.AutoCancel => 0,
				PlayerAllianceReadyCheckCommand.Ready => readyStatusBefore - 1,
				PlayerAllianceReadyCheckCommand.NotReady => readyStatusBefore - 1,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported alliance ready-check command."),
			};
			_allianceReadyStatusByAllianceId[allianceId] = readyStatusAfter;

			var intents = CreateReadyCheckIntents(members, player.ObjectId, command, readyStatusAfter);
			return new PlayerAllianceReadyCheckPlan(
				allianceId,
				player.ObjectId,
				command,
				readyStatusBefore,
				readyStatusAfter,
				intents);
		}
	}

	public PlayerAllianceBrandUpdatePlan? UpdateBrand(int allianceId, int brandId, int targetObjectId)
	{
		// Java parity: model/team/TemporaryPlayerTeam.updateBrand stores target id and broadcasts SM_SHOW_BRAND.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members))
				return null;

			if (!_targetObjectIdsByBrandIdByAllianceId.TryGetValue(allianceId, out var targetObjectIdsByBrandId))
			{
				targetObjectIdsByBrandId = [];
				_targetObjectIdsByBrandIdByAllianceId[allianceId] = targetObjectIdsByBrandId;
			}

			targetObjectIdsByBrandId[brandId] = targetObjectId;
			var broadcasts = members
				.Select(member => new PlayerAllianceBrandIntent(member.ObjectId, new Dictionary<int, int> { [brandId] = targetObjectId }))
				.ToArray();

			return new PlayerAllianceBrandUpdatePlan(allianceId, brandId, targetObjectId, broadcasts);
		}
	}

	public PlayerAllianceBrandIntent? CreateSendBrandsIntent(int allianceId, Player recipient)
	{
		// Java parity: model/team/TemporaryPlayerTeam.sendBrands sends SM_SHOW_BRAND(targetIdsByBrandId) to a single member.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| members.All(member => member.ObjectId != recipient.ObjectId))
				return null;

			return new PlayerAllianceBrandIntent(recipient.ObjectId, GetBrandSnapshot(allianceId));
		}
	}

	public PlayerAllianceEnteredPlan? CreateEnteredPlan(int allianceId, Player invitedPlayer)
	{
		// Java parity: model/team/alliance/events/PlayerAllianceEnteredEvent sends team.sendBrands(player) after the invited player's join packets.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor)
				|| members.All(member => member.ObjectId != invitedPlayer.ObjectId))
				return null;

			var brandIntent = new PlayerAllianceBrandIntent(invitedPlayer.ObjectId, GetBrandSnapshot(allianceId));
			return _enteredPlanner.CreateEnteredPlan(
				allianceId,
				descriptor.LeaderObjectId,
				members.Select(member => member.Player).ToArray(),
				_viceCaptainObjectIdsByAllianceId.GetValueOrDefault(allianceId) ?? [],
				invitedPlayer.ObjectId,
				descriptor.LootRules,
				descriptor.TeamType,
				isInLeague: false,
				brandIntent);
		}
	}

	public PlayerAllianceMemberGroupChangePlan? ChangeMemberGroup(
		int allianceId,
		int firstMemberObjectId,
		int secondMemberObjectId,
		int targetAllianceGroupId)
	{
		// Java parity: model/team/alliance/events/ChangeMemberGroupEvent mutates PlayerAllianceGroup membership before broadcasting member-info packets.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByAllianceId.TryGetValue(allianceId, out var members)
				|| !_descriptorsByAllianceId.TryGetValue(allianceId, out var descriptor))
				return null;

			var firstMember = members.FirstOrDefault(member => member.ObjectId == firstMemberObjectId);
			if (firstMember == null)
				return null;

			if (secondMemberObjectId != 0)
			{
				var secondMember = members.FirstOrDefault(member => member.ObjectId == secondMemberObjectId);
				if (secondMember == null)
					return null;

				var firstAllianceGroupId = firstMember.AllianceGroupId;
				firstMember.MoveToAllianceGroup(secondMember.AllianceGroupId);
				secondMember.MoveToAllianceGroup(firstAllianceGroupId);
			}
			else
			{
				if (!descriptor.AllianceGroupIds.Contains(targetAllianceGroupId))
				{
					// Java parity: ChangeMemberGroupEvent.moveMemberToGroup removes the member from the old
					// PlayerAllianceGroup before PlayerAlliance.getAllianceGroup(groupId) throws requireNonNull.
					firstMember.ClearAllianceGroupReference();
					ApplySnapshot(allianceId, members, descriptor);
					throw new InvalidOperationException($"No such alliance group {targetAllianceGroupId}");
				}

				firstMember.MoveToAllianceGroup(targetAllianceGroupId);
			}

			ApplySnapshot(allianceId, members, descriptor);
			return _groupChangePlanner.CreateMemberGroupChangePlan(
				allianceId,
				members.Select(member => member.Player).ToArray(),
				firstMemberObjectId,
				secondMemberObjectId,
				targetAllianceGroupId);
		}
	}

	public PlayerAllianceSnapshot? Resolve(Player player)
	{
		// Java parity: model/gameobjects/player/Player.getPlayerAlliance; registry-owned snapshots are attached to the player.
		return player.TeamMembership == PlayerTeamMembership.Alliance ? player.CurrentAllianceSnapshot : null;
	}

	public int GetAllianceReadyStatus(int allianceId)
	{
		// Java parity: model/team/alliance/PlayerAlliance.getAllianceReadyStatus.
		lock (_sync)
			return _allianceReadyStatusByAllianceId.GetValueOrDefault(allianceId);
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

	private static IReadOnlyList<PlayerAllianceReadyCheckPacketIntent> CreateReadyCheckIntents(
		IReadOnlyList<PlayerAllianceMember> members,
		int playerObjectId,
		PlayerAllianceReadyCheckCommand command,
		int readyStatusAfter)
	{
		var intents = new List<PlayerAllianceReadyCheckPacketIntent>();
		var sequence = 0;
		foreach (var member in members)
		{
			switch (command)
			{
				case PlayerAllianceReadyCheckCommand.Cancel:
					intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, playerObjectId, 0));
					break;
				case PlayerAllianceReadyCheckCommand.Start:
					intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, playerObjectId, 5));
					intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, playerObjectId, 1));
					break;
				case PlayerAllianceReadyCheckCommand.AutoCancel:
					intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, playerObjectId, 2));
					break;
				case PlayerAllianceReadyCheckCommand.Ready:
					intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, playerObjectId, 5));
					if (readyStatusAfter == 0)
						intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, PlayerObjectId: 0, StatusCode: 3));
					break;
				case PlayerAllianceReadyCheckCommand.NotReady:
					intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, playerObjectId, 4));
					if (readyStatusAfter == 0)
						intents.Add(new PlayerAllianceReadyCheckPacketIntent(sequence++, member.ObjectId, PlayerObjectId: 0, StatusCode: 3));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported alliance ready-check command.");
			}
		}

		return intents;
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
		var leagueId = _leagueIdByAllianceId.GetValueOrDefault(allianceId);

		return new PlayerAllianceSnapshot(
			allianceId,
			descriptor.LeaderObjectId,
			members.Select(member => member.ObjectId).ToArray(),
			memberObjectIdsByGroupId,
			viceCaptainIds,
			descriptor.TeamType,
			descriptor.LootRules,
			leagueId);
	}

	private IReadOnlyDictionary<int, int> GetBrandSnapshot(int allianceId)
	{
		return _targetObjectIdsByBrandIdByAllianceId.TryGetValue(allianceId, out var targetObjectIdsByBrandId)
			? new Dictionary<int, int>(targetObjectIdsByBrandId)
			: new Dictionary<int, int>();
	}

	private static void ClearAlliance(Player member)
	{
		member.TeamMembership = PlayerTeamMembership.None;
		member.CurrentTeamId = 0;
		member.CurrentTeamMemberObjectIds = Array.Empty<int>();
		member.CurrentAllianceSnapshot = null;
	}
}
