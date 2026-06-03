using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class AutoGroupLookingPartyRegistrationService
{
	private readonly object _sync = new();
	private readonly Dictionary<int, List<AutoGroupLookingPartyRegistration>> _lookingPartiesByMaskId = [];

	public AutoGroupStartLookingResult StartLooking(
		Player player,
		int maskId,
		AutoGroupEntryRequestType entryRequestType,
		AutoGroupTable? autoGroups,
		PlayerGroupRuntime? groupRuntime = null,
		PlayerAllianceRuntime? allianceRuntime = null,
		InstanceCooltimeTable? instanceCooltimes = null,
		DateTimeOffset? now = null,
		bool announceBattlegroundRegistrations = false)
	{
		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(maskId);
		if (autoGroup == null)
			return AutoGroupStartLookingResult.MissingAutoGroup(maskId, entryRequestType);

		var guard = AutoGroupRegistrationGuardPlanService.CreatePlan(
			player.Level,
			autoGroup.MinLevel,
			autoGroup.MaxLevel,
			isPvPArenaType: false,
			pvpArenaAvailable: true,
			hasCooldown: false);
		if (!guard.CanRegister)
			return AutoGroupStartLookingResult.Blocked(maskId, entryRequestType, guard);

		var memberObjectIds = ResolveMemberObjectIds(player, groupRuntime, allianceRuntime);
		var entryGuard = CreateEntryRequestGuard(
			player,
			autoGroup,
			entryRequestType,
			memberObjectIds,
			groupRuntime,
			allianceRuntime,
			instanceCooltimes,
			now);
		if (!entryGuard.CanRegister)
			return AutoGroupStartLookingResult.Blocked(maskId, entryRequestType, entryGuard);

		lock (_sync)
		{
			if (_lookingPartiesByMaskId.TryGetValue(maskId, out var parties)
				&& parties.Any(party => party.MemberObjectIds.Contains(player.ObjectId)))
			{
				return AutoGroupStartLookingResult.AlreadyRegistered(maskId, entryRequestType);
			}

			var registration = new AutoGroupLookingPartyRegistration(
				maskId,
				player.ObjectId,
				memberObjectIds,
				player.Race,
				entryRequestType,
				now ?? DateTimeOffset.UtcNow);
			if (parties == null)
			{
				parties = [];
				_lookingPartiesByMaskId[maskId] = parties;
			}

			parties.Add(registration);
			var announcement = CreateBattlegroundRegistrationAnnouncement(
				player,
				autoGroup,
				entryRequestType,
				parties,
				announceBattlegroundRegistrations);
			return AutoGroupStartLookingResult.Registered(maskId, entryRequestType, registration, guard, announcement);
		}
	}

	public AutoGroupLookingPartyRegistration RegisterLookingParty(
		int maskId,
		IReadOnlyCollection<int> memberObjectIds,
		string race = "",
		AutoGroupEntryRequestType entryRequestType = AutoGroupEntryRequestType.NewGroupEntry,
		DateTimeOffset? registrationTime = null)
	{
		if (memberObjectIds.Count == 0)
			throw new ArgumentException("At least one member object id is required.", nameof(memberObjectIds));

		var registration = new AutoGroupLookingPartyRegistration(
			maskId,
			memberObjectIds.First(),
			memberObjectIds.ToArray(),
			race,
			entryRequestType,
			registrationTime ?? DateTimeOffset.UtcNow);
		lock (_sync)
		{
			if (!_lookingPartiesByMaskId.TryGetValue(maskId, out var parties))
			{
				parties = [];
				_lookingPartiesByMaskId[maskId] = parties;
			}

			parties.Add(registration);
		}

		return registration;
	}

	public AutoGroupQueueMatchPlan CreateQueueMatchPlan(
		int maskId,
		AutoGroupTable? autoGroups,
		InstanceCooltimeTable? instanceCooltimes)
	{
		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(maskId);
		if (autoGroup == null)
			return AutoGroupQueueMatchPlan.MissingAutoGroup(maskId);

		AutoGroupLookingPartyRegistration[] orderedParties;
		lock (_sync)
		{
			if (!_lookingPartiesByMaskId.TryGetValue(maskId, out var parties) || parties.Count == 0)
				return AutoGroupQueueMatchPlan.NoQueuedParties(maskId, autoGroup.InstanceMapId);

			// Java parity: AutoGroupService.checkQueueForNewMatches sorts LookingForParty
			// with Comparable before probing each possible starting party.
			orderedParties = parties
				.OrderByDescending(party => party.EntryRequestType)
				.ThenByDescending(party => party.MemberObjectIds.Count)
				.ThenBy(party => party.RegistrationTime)
				.ToArray();
		}

		if (!autoGroup.IsPeriodicInstance)
		{
			return AutoGroupQueueMatchPlan.UnsupportedAutoGroupKind(
				maskId,
				autoGroup.InstanceMapId,
				orderedParties,
				"AutoGroupService.checkQueueForNewMatches uses AutoGroupType.createAutoInstance; this C# planning slice currently models AutoPvpInstance periodic capacity rules only.");
		}

		if (instanceCooltimes == null)
		{
			return AutoGroupQueueMatchPlan.MissingCapacityData(
				maskId,
				autoGroup.InstanceMapId,
				orderedParties);
		}

		var totalCapacity = GetPvpTotalCapacity(autoGroup, instanceCooltimes);
		if (totalCapacity <= 0)
		{
			return AutoGroupQueueMatchPlan.MissingCapacityData(
				maskId,
				autoGroup.InstanceMapId,
				orderedParties);
		}

		for (var i = 0; i < orderedParties.Length; i++)
		{
			var accepted = new List<AutoGroupLookingPartyRegistration>();
			var raceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			if (!TryAddPeriodicPvpParty(orderedParties[i], autoGroup, instanceCooltimes, raceCounts))
				continue;

			accepted.Add(orderedParties[i]);
			if (accepted.Sum(party => party.MemberObjectIds.Count) != totalCapacity)
			{
				for (var j = i + 1; j < orderedParties.Length; j++)
				{
					if (!TryAddPeriodicPvpParty(orderedParties[j], autoGroup, instanceCooltimes, raceCounts))
						continue;

					accepted.Add(orderedParties[j]);
					if (accepted.Sum(party => party.MemberObjectIds.Count) == totalCapacity)
						break;
				}
			}

			if (accepted.Sum(party => party.MemberObjectIds.Count) == totalCapacity)
			{
				return AutoGroupQueueMatchPlan.Ready(
					maskId,
					autoGroup.InstanceMapId,
					orderedParties,
					accepted,
					totalCapacity);
			}
		}

		return AutoGroupQueueMatchPlan.NotReady(maskId, autoGroup.InstanceMapId, orderedParties, totalCapacity);
	}

	public int GetLookingPartyCount(int maskId)
	{
		lock (_sync)
			return _lookingPartiesByMaskId.TryGetValue(maskId, out var parties) ? parties.Count : 0;
	}

	public bool IsSearching(int playerObjectId, int maskId)
	{
		lock (_sync)
			return _lookingPartiesByMaskId.TryGetValue(maskId, out var parties)
				&& parties.Any(party => party.MemberObjectIds.Contains(playerObjectId));
	}

	public async Task<AutoGroupCancelRegistrationResult> CancelRegistrationAsync(
		int playerObjectId,
		int maskId,
		AutoGroupTable? autoGroups,
		IGameClientConnectionRegistry connectionRegistry,
		CancellationToken cancellationToken = default)
	{
		AutoGroupLookingPartyRegistration? removedParty = null;
		var removedMemberOnly = false;
		lock (_sync)
		{
			if (!_lookingPartiesByMaskId.TryGetValue(maskId, out var parties))
				return AutoGroupCancelRegistrationResult.NoRegistration(maskId, playerObjectId);

			var party = parties.FirstOrDefault(candidate => candidate.MemberObjectIds.Contains(playerObjectId));
			if (party == null)
				return AutoGroupCancelRegistrationResult.NoRegistration(maskId, playerObjectId);

			if (party.LeaderObjectId == playerObjectId)
			{
				parties.Remove(party);
				if (parties.Count == 0)
					_lookingPartiesByMaskId.Remove(maskId);
				removedParty = party;
			}
			else
			{
				var remainingMemberObjectIds = party.MemberObjectIds
					.Where(memberObjectId => memberObjectId != playerObjectId)
					.ToArray();
				var index = parties.IndexOf(party);
				parties[index] = party with { MemberObjectIds = remainingMemberObjectIds };
				removedMemberOnly = true;
			}
		}

		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(maskId);
		var recipients = removedParty?.MemberObjectIds ?? [playerObjectId];
		var sentPackets = 0;
		if (autoGroup != null)
		{
			foreach (var recipientObjectId in recipients)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, new SmAutoGroup(autoGroup, windowId: 2)))
					sentPackets++;
			}
		}

		return new AutoGroupCancelRegistrationResult(
			maskId,
			playerObjectId,
			removedParty != null
				? AutoGroupCancelRegistrationStatus.LeaderPartyRemoved
				: AutoGroupCancelRegistrationStatus.MemberRemoved,
			removedParty?.MemberObjectIds ?? [playerObjectId],
			sentPackets,
			HasAutoGroupData: autoGroup != null,
			RemovedMemberOnly: removedMemberOnly);
	}

	public async Task<AutoGroupStopRegistrationsByMaskIdResult> StopRegistrationsByMaskIdAsync(
		int maskId,
		AutoGroupTable? autoGroups,
		IGameClientConnectionRegistry connectionRegistry,
		CancellationToken cancellationToken = default)
	{
		AutoGroupLookingPartyRegistration[] removedParties;
		lock (_sync)
		{
			if (!_lookingPartiesByMaskId.Remove(maskId, out var parties) || parties.Count == 0)
			{
				return new AutoGroupStopRegistrationsByMaskIdResult(
					maskId,
					RemovedPartyCount: 0,
					RemovedMemberObjectIds: Array.Empty<int>(),
					SentPackets: 0,
					HasAutoGroupData: autoGroups?.GetTemplateByInstanceMaskId(maskId) != null);
			}

			removedParties = parties.ToArray();
		}

		var removedMemberObjectIds = removedParties
			.SelectMany(party => party.MemberObjectIds)
			.ToArray();
		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(maskId);
		var sentPackets = 0;
		if (autoGroup != null)
		{
			foreach (var memberObjectId in removedMemberObjectIds)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(memberObjectId, new SmAutoGroup(autoGroup, windowId: 2)))
					sentPackets++;
			}
		}

		return new AutoGroupStopRegistrationsByMaskIdResult(
			maskId,
			removedParties.Length,
			removedMemberObjectIds,
			sentPackets,
			HasAutoGroupData: autoGroup != null);
	}

	private static IReadOnlyList<int> ResolveMemberObjectIds(
		Player player,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime)
	{
		if (player.TeamMembership == PlayerTeamMembership.Group && player.CurrentTeamId > 0)
		{
			var groupMemberIds = groupRuntime?.GetMemberObjectIds(player.CurrentTeamId)
				?? player.CurrentTeamMemberObjectIds;
			if (groupMemberIds.Count > 0)
				return groupMemberIds;
		}

		if (player.TeamMembership == PlayerTeamMembership.Alliance && player.CurrentTeamId > 0)
		{
			var allianceMemberIds = allianceRuntime?.GetMemberObjectIds(player.CurrentTeamId)
				?? player.CurrentTeamMemberObjectIds;
			if (allianceMemberIds.Count > 0)
				return allianceMemberIds;
		}

		return [player.ObjectId];
	}

	private AutoGroupRegistrationGuardPlan CreateEntryRequestGuard(
		Player player,
		AutoGroupSummary autoGroup,
		AutoGroupEntryRequestType entryRequestType,
		IReadOnlyList<int> memberObjectIds,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime,
		InstanceCooltimeTable? instanceCooltimes,
		DateTimeOffset? now)
	{
		// Java parity: services/autogroup/AutoGroupUtility.canRegister* after
		// AutoGroupService.canRegister common level/PvP/cooldown guards.
		return entryRequestType switch
		{
			AutoGroupEntryRequestType.NewGroupEntry => CreateSoloEntryGuard(
				player,
				autoGroup.RegisterNew,
				autoGroup.MaskId,
				entryRequestType,
				"AutoGroupUtility.canRegisterNewEntry"),
			AutoGroupEntryRequestType.QuickGroupEntry => CreateSoloEntryGuard(
				player,
				autoGroup.RegisterQuick,
				autoGroup.MaskId,
				entryRequestType,
				"AutoGroupUtility.canRegisterQuickEntry"),
			AutoGroupEntryRequestType.GroupEntry => CreateGroupEntryGuard(
				player,
				autoGroup,
				memberObjectIds,
				groupRuntime,
				allianceRuntime,
				instanceCooltimes,
				now),
			_ => AllowedEntryGuard(player.Level, autoGroup.MaskId, entryRequestType),
		};
	}

	private static AutoGroupRegistrationGuardPlan CreateSoloEntryGuard(
		Player player,
		bool templateSupportsEntry,
		int maskId,
		AutoGroupEntryRequestType entryRequestType,
		string javaSource)
	{
		if (!templateSupportsEntry)
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedEntryUnsupported,
				player.Level,
				DenialMessage: null,
				CanRegister: false,
				$"{javaSource} -> template flag false for mask {maskId} -> return false (no system message)"
			);
		}

		if (IsInTeam(player))
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedNotLeader,
				player.Level,
				SmSystemMessage.CantInstanceNotLeader(),
				CanRegister: false,
				$"{javaSource} -> player.isInTeam() -> STR_MSG_CANT_INSTANCE_NOT_LEADER"
			);
		}

		return AllowedEntryGuard(player.Level, maskId, entryRequestType);
	}

	private AutoGroupRegistrationGuardPlan CreateGroupEntryGuard(
		Player player,
		AutoGroupSummary autoGroup,
		IReadOnlyList<int> memberObjectIds,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime,
		InstanceCooltimeTable? instanceCooltimes,
		DateTimeOffset? now)
	{
		if (!autoGroup.RegisterGroup)
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedEntryUnsupported,
				player.Level,
				DenialMessage: null,
				CanRegister: false,
				$"AutoGroupUtility.canRegisterGroupEntry -> !template.hasRegisterGroup for mask {autoGroup.MaskId} -> return false (no system message)"
			);
		}

		if (!IsInTeam(player) || !IsTeamLeader(player, memberObjectIds, groupRuntime, allianceRuntime))
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedNotLeader,
				player.Level,
				SmSystemMessage.CantInstanceNotLeader(),
				CanRegister: false,
				"AutoGroupUtility.checkGroupRequirements -> team == null || !team.isLeader(player) -> STR_MSG_CANT_INSTANCE_NOT_LEADER"
			);
		}

		if (autoGroup.IsPeriodicInstance && instanceCooltimes != null)
		{
			var maxMemberPerTeam = instanceCooltimes.GetMaxMemberCount(autoGroup.InstanceMapId, player.Race);
			if (maxMemberPerTeam > 0 && memberObjectIds.Count > maxMemberPerTeam)
			{
				return new AutoGroupRegistrationGuardPlan(
					AutoGroupRegistrationGuardPlanStatus.BlockedTooManyMembers,
					player.Level,
					SmSystemMessage.CantInstanceTooManyMembers(maxMemberPerTeam, autoGroup.InstanceMapId),
					CanRegister: false,
					"AutoGroupUtility.checkGroupRequirements -> periodic team.size() > INSTANCE_COOLTIME_DATA.getMaxMemberCount -> STR_MSG_CANT_INSTANCE_TOO_MANY_MEMBERS"
				);
			}
		}
		else if ((autoGroup.IsHarmonyArena || autoGroup.IsTrainingHarmonyArena) && memberObjectIds.Count > 3)
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedTooManyMembers,
				player.Level,
				SmSystemMessage.CantInstanceTooManyMembers(3, autoGroup.InstanceMapId),
				CanRegister: false,
				"AutoGroupUtility.checkGroupRequirements -> Harmony/training Harmony team.size() > 3 -> STR_MSG_CANT_INSTANCE_TOO_MANY_MEMBERS"
			);
		}

		var memberGuard = CreateGroupMemberRequirementGuard(
			player,
			autoGroup,
			groupRuntime,
			allianceRuntime,
			instanceCooltimes,
			now ?? DateTimeOffset.UtcNow);
		if (!memberGuard.CanRegister)
			return memberGuard;

		return AllowedEntryGuard(player.Level, autoGroup.MaskId, AutoGroupEntryRequestType.GroupEntry);
	}

	private AutoGroupRegistrationGuardPlan CreateGroupMemberRequirementGuard(
		Player requester,
		AutoGroupSummary autoGroup,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime,
		InstanceCooltimeTable? instanceCooltimes,
		DateTimeOffset now)
	{
		// Java parity: AutoGroupUtility.checkGroupRequirements iterates team.getMembers(),
		// skips the leader, then denies the requester when any member has cooldown,
		// is outside the autogroup level range, or is already searching the same mask.
		foreach (var member in ResolveMemberPlayers(requester, groupRuntime, allianceRuntime))
		{
			if (member.ObjectId == requester.ObjectId)
				continue;

			if (autoGroup.IsHarmonyArena && !HasRequiredHarmonyTicket(member))
			{
				return new AutoGroupRegistrationGuardPlan(
					AutoGroupRegistrationGuardPlanStatus.BlockedHarmonyMemberMissingItem,
					requester.Level,
					SmSystemMessage.CantInstanceEnterMember(member.Name),
					CanRegister: false,
					"AutoGroupUtility.checkGroupRequirements -> agt.isHarmonyArena() && !PvPArenaService.checkItem(member, agt) -> member STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM and requester STR_MSG_CANT_INSTANCE_ENTER_MEMBER",
					[
						new AutoGroupMemberDenialIntent(member.ObjectId, SmSystemMessage.InstanceCantEnterWithoutItem()),
					]
				);
			}

			if (instanceCooltimes != null
				&& PlayerPortalCooldownService.IsPortalUseDisabled(member, autoGroup.InstanceMapId, instanceCooltimes, now))
			{
				return MemberCannotEnterGuard(requester.Level, member.Name, "AutoGroupUtility.hasCoolDown(member, mapId)");
			}

			if (member.Level < autoGroup.MinLevel || member.Level > autoGroup.MaxLevel)
				return MemberCannotEnterGuard(requester.Level, member.Name, "!agt.isInLvlRange(member.getLevel())");

			if (IsSearching(member.ObjectId, autoGroup.MaskId))
				return MemberCannotEnterGuard(requester.Level, member.Name, "AutoGroupService.isSearching(member, maskId)");
		}

		return AllowedEntryGuard(requester.Level, autoGroup.MaskId, AutoGroupEntryRequestType.GroupEntry);
	}

	private AutoGroupRegistrationGuardPlan MemberCannotEnterGuard(
		int requesterLevel,
		string memberName,
		string javaCondition)
	{
		return new AutoGroupRegistrationGuardPlan(
			AutoGroupRegistrationGuardPlanStatus.BlockedMemberCannotEnter,
			requesterLevel,
			SmSystemMessage.CantInstanceEnterMember(memberName),
			CanRegister: false,
			$"AutoGroupUtility.checkGroupRequirements -> {javaCondition} -> STR_MSG_CANT_INSTANCE_ENTER_MEMBER({memberName})"
		);
	}

	private static AutoGroupRegistrationGuardPlan AllowedEntryGuard(
		int playerLevel,
		int maskId,
		AutoGroupEntryRequestType entryRequestType)
	{
		return new AutoGroupRegistrationGuardPlan(
			AutoGroupRegistrationGuardPlanStatus.CanRegister,
			playerLevel,
			DenialMessage: null,
			CanRegister: true,
			$"AutoGroupUtility.canRegister* -> entry guard passed (mask={maskId}, entry={entryRequestType})"
		);
	}

	private static bool IsInTeam(Player player)
	{
		return player.CurrentTeamId > 0
			&& player.TeamMembership is PlayerTeamMembership.Group or PlayerTeamMembership.Alliance;
	}

	private static bool IsTeamLeader(
		Player player,
		IReadOnlyList<int> memberObjectIds,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime)
	{
		if (player.TeamMembership == PlayerTeamMembership.Group)
			return groupRuntime?.IsLeader(player.CurrentTeamId, player)
				?? memberObjectIds.FirstOrDefault() == player.ObjectId;

		if (player.TeamMembership == PlayerTeamMembership.Alliance)
			return allianceRuntime?.IsLeader(player.CurrentTeamId, player)
				?? memberObjectIds.FirstOrDefault() == player.ObjectId;

		return false;
	}

	private static bool HasRequiredHarmonyTicket(Player member)
	{
		// Java parity: PvPArenaService.checkItem -> Harmony -> inventory.getItemCountByItemId(186000184) > 0.
		return member.InventoryItems
			.Where(item => item.ItemId == PvPArenaAvailabilityPlanService.HarmonyArenaTicketItemId)
			.Sum(item => item.Count) > 0;
	}

	private static IReadOnlyList<Player> ResolveMemberPlayers(
		Player player,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime)
	{
		if (player.TeamMembership == PlayerTeamMembership.Group && player.CurrentTeamId > 0)
			return groupRuntime?.GetMemberPlayers(player.CurrentTeamId) ?? [player];

		if (player.TeamMembership == PlayerTeamMembership.Alliance && player.CurrentTeamId > 0)
			return allianceRuntime?.GetMemberPlayers(player.CurrentTeamId) ?? [player];

		return [player];
	}

	private static AutoGroupBattlegroundRegistrationAnnouncement? CreateBattlegroundRegistrationAnnouncement(
		Player player,
		AutoGroupSummary autoGroup,
		AutoGroupEntryRequestType entryRequestType,
		IReadOnlyCollection<AutoGroupLookingPartyRegistration> parties,
		bool announceBattlegroundRegistrations)
	{
		if (!announceBattlegroundRegistrations
			|| !autoGroup.IsPeriodicInstance
			|| entryRequestType != AutoGroupEntryRequestType.GroupEntry
			|| parties.Count(party => string.Equals(party.Race, player.Race, StringComparison.OrdinalIgnoreCase)) != 1)
		{
			return null;
		}

		var raceL10n = GetRaceL10n(player.Race);
		var autoGroupL10n = ChatUtil.L10n(autoGroup.NameId);
		if (raceL10n == null || autoGroupL10n == null)
			return null;

		return new AutoGroupBattlegroundRegistrationAnnouncement(
			$"{raceL10n} have registered for {autoGroupL10n}.",
			player.Race,
			autoGroup.MinLevel,
			autoGroup.MaxLevel);
	}

	private static string? GetRaceL10n(string race)
	{
		// Java parity: model/Race implements L10n with ELYOS=900240 and ASMODIANS=900241.
		if (string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase))
			return ChatUtil.L10n(900240);
		if (string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase))
			return ChatUtil.L10n(900241);
		return null;
	}

	private static bool TryAddPeriodicPvpParty(
		AutoGroupLookingPartyRegistration party,
		AutoGroupSummary autoGroup,
		InstanceCooltimeTable instanceCooltimes,
		IDictionary<string, int> raceCounts)
	{
		// Java parity: AutoPvpInstance.addLookingForParty rejects parties that
		// exceed the race-specific max-member count, then tentatively registers all members.
		var race = party.Race;
		var maxPlayersForRace = instanceCooltimes.GetMaxMemberCount(autoGroup.InstanceMapId, race);
		if (maxPlayersForRace <= 0)
			return false;

		raceCounts.TryGetValue(race, out var currentRaceCount);
		if (party.MemberObjectIds.Count + currentRaceCount > maxPlayersForRace)
			return false;

		raceCounts[race] = currentRaceCount + party.MemberObjectIds.Count;
		return true;
	}

	private static int GetPvpTotalCapacity(AutoGroupSummary autoGroup, InstanceCooltimeTable instanceCooltimes)
	{
		// Java parity: AutoPvpInstance.getMaxPlayers() before instance creation returns
		// dark-race max players plus light-race max players.
		return instanceCooltimes.GetMaxMemberCount(autoGroup.InstanceMapId, "ASMODIANS")
			+ instanceCooltimes.GetMaxMemberCount(autoGroup.InstanceMapId, "ELYOS");
	}
}

public sealed record AutoGroupLookingPartyRegistration(
	int MaskId,
	int LeaderObjectId,
	IReadOnlyList<int> MemberObjectIds,
	string Race = "",
	AutoGroupEntryRequestType EntryRequestType = AutoGroupEntryRequestType.NewGroupEntry,
	DateTimeOffset RegistrationTime = default);

public sealed record AutoGroupQueueMatchPlan(
	AutoGroupQueueMatchPlanStatus Status,
	int MaskId,
	int InstanceMapId,
	IReadOnlyList<AutoGroupLookingPartyRegistration> OrderedQueuedParties,
	IReadOnlyList<AutoGroupLookingPartyRegistration> MatchedParties,
	int RequiredPlayerCount,
	string JavaSource)
{
	public IReadOnlyList<int> MatchedMemberObjectIds => MatchedParties
		.SelectMany(party => party.MemberObjectIds)
		.ToArray();

	public static AutoGroupQueueMatchPlan MissingAutoGroup(int maskId)
	{
		return new AutoGroupQueueMatchPlan(
			AutoGroupQueueMatchPlanStatus.MissingAutoGroup,
			maskId,
			InstanceMapId: 0,
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			RequiredPlayerCount: 0,
			"AutoGroupService.checkQueueForNewMatches -> AutoGroupType.getAGTByMaskId returned null");
	}

	public static AutoGroupQueueMatchPlan NoQueuedParties(int maskId, int instanceMapId)
	{
		return new AutoGroupQueueMatchPlan(
			AutoGroupQueueMatchPlanStatus.NoQueuedParties,
			maskId,
			instanceMapId,
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			RequiredPlayerCount: 0,
			"AutoGroupService.checkQueueForNewMatches -> queuedParties null or empty");
	}

	public static AutoGroupQueueMatchPlan UnsupportedAutoGroupKind(
		int maskId,
		int instanceMapId,
		IReadOnlyList<AutoGroupLookingPartyRegistration> orderedQueuedParties,
		string javaSource)
	{
		return new AutoGroupQueueMatchPlan(
			AutoGroupQueueMatchPlanStatus.UnsupportedAutoGroupKind,
			maskId,
			instanceMapId,
			orderedQueuedParties,
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			RequiredPlayerCount: 0,
			javaSource);
	}

	public static AutoGroupQueueMatchPlan MissingCapacityData(
		int maskId,
		int instanceMapId,
		IReadOnlyList<AutoGroupLookingPartyRegistration> orderedQueuedParties)
	{
		return new AutoGroupQueueMatchPlan(
			AutoGroupQueueMatchPlanStatus.MissingCapacityData,
			maskId,
			instanceMapId,
			orderedQueuedParties,
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			RequiredPlayerCount: 0,
			"AutoPvpInstance.getMaxPlayers -> INSTANCE_COOLTIME_DATA max-member data is required before queue matching can be planned");
	}

	public static AutoGroupQueueMatchPlan NotReady(
		int maskId,
		int instanceMapId,
		IReadOnlyList<AutoGroupLookingPartyRegistration> orderedQueuedParties,
		int requiredPlayerCount)
	{
		return new AutoGroupQueueMatchPlan(
			AutoGroupQueueMatchPlanStatus.NotReady,
			maskId,
			instanceMapId,
			orderedQueuedParties,
			Array.Empty<AutoGroupLookingPartyRegistration>(),
			requiredPlayerCount,
			"AutoGroupService.checkQueueForNewMatches -> no probe produced AGQuestion.READY");
	}

	public static AutoGroupQueueMatchPlan Ready(
		int maskId,
		int instanceMapId,
		IReadOnlyList<AutoGroupLookingPartyRegistration> orderedQueuedParties,
		IReadOnlyList<AutoGroupLookingPartyRegistration> matchedParties,
		int requiredPlayerCount)
	{
		return new AutoGroupQueueMatchPlan(
			AutoGroupQueueMatchPlanStatus.Ready,
			maskId,
			instanceMapId,
			orderedQueuedParties,
			matchedParties,
			requiredPlayerCount,
			"AutoGroupService.checkQueueForNewMatches -> AutoPvpInstance.addLookingForParty reached AGQuestion.READY; createNewInstance remains deferred");
	}
}

public enum AutoGroupQueueMatchPlanStatus
{
	MissingAutoGroup,
	NoQueuedParties,
	UnsupportedAutoGroupKind,
	MissingCapacityData,
	NotReady,
	Ready,
}

public sealed record AutoGroupBattlegroundRegistrationAnnouncement(
	string Message,
	string RegisteringRace,
	int MinLevel,
	int MaxLevel)
{
	public const byte BrightYellowCenterChatType = 36;

	public bool ShouldReceive(Player player)
	{
		return !string.Equals(player.Race, RegisteringRace, StringComparison.OrdinalIgnoreCase)
			&& player.Level >= MinLevel
			&& player.Level <= MaxLevel;
	}
}

public sealed record AutoGroupStopRegistrationsByMaskIdResult(
	int MaskId,
	int RemovedPartyCount,
	IReadOnlyList<int> RemovedMemberObjectIds,
	int SentPackets,
	bool HasAutoGroupData);

public sealed record AutoGroupCancelRegistrationResult(
	int MaskId,
	int PlayerObjectId,
	AutoGroupCancelRegistrationStatus Status,
	IReadOnlyList<int> NotifiedMemberObjectIds,
	int SentPackets,
	bool HasAutoGroupData,
	bool RemovedMemberOnly)
{
	public static AutoGroupCancelRegistrationResult NoRegistration(int maskId, int playerObjectId)
	{
		return new AutoGroupCancelRegistrationResult(
			maskId,
			playerObjectId,
			AutoGroupCancelRegistrationStatus.NoRegistration,
			Array.Empty<int>(),
			SentPackets: 0,
			HasAutoGroupData: false,
			RemovedMemberOnly: false);
	}
}

public enum AutoGroupCancelRegistrationStatus
{
	NoRegistration,
	LeaderPartyRemoved,
	MemberRemoved,
}

public sealed record AutoGroupStartLookingResult(
	AutoGroupStartLookingStatus Status,
	int MaskId,
	AutoGroupEntryRequestType EntryRequestType,
	AutoGroupLookingPartyRegistration? Registration,
	AutoGroupRegistrationGuardPlan? GuardPlan,
	AutoGroupBattlegroundRegistrationAnnouncement? BattlegroundAnnouncement = null)
{
	public bool RegisteredQueue => Status == AutoGroupStartLookingStatus.Registered;

	public static AutoGroupStartLookingResult Registered(
		int maskId,
		AutoGroupEntryRequestType entryRequestType,
		AutoGroupLookingPartyRegistration registration,
		AutoGroupRegistrationGuardPlan guardPlan,
		AutoGroupBattlegroundRegistrationAnnouncement? battlegroundAnnouncement = null)
	{
		return new AutoGroupStartLookingResult(
			AutoGroupStartLookingStatus.Registered,
			maskId,
			entryRequestType,
			registration,
			guardPlan,
			battlegroundAnnouncement);
	}

	public static AutoGroupStartLookingResult MissingAutoGroup(int maskId, AutoGroupEntryRequestType entryRequestType)
	{
		return new AutoGroupStartLookingResult(
			AutoGroupStartLookingStatus.MissingAutoGroup,
			maskId,
			entryRequestType,
			Registration: null,
			GuardPlan: null);
	}

	public static AutoGroupStartLookingResult Blocked(
		int maskId,
		AutoGroupEntryRequestType entryRequestType,
		AutoGroupRegistrationGuardPlan guardPlan)
	{
		var status = guardPlan.Status is AutoGroupRegistrationGuardPlanStatus.BlockedEntryUnsupported
			or AutoGroupRegistrationGuardPlanStatus.BlockedNotLeader
			or AutoGroupRegistrationGuardPlanStatus.BlockedTooManyMembers
			or AutoGroupRegistrationGuardPlanStatus.BlockedMemberCannotEnter
			or AutoGroupRegistrationGuardPlanStatus.BlockedHarmonyMemberMissingItem
			? AutoGroupStartLookingStatus.BlockedByEntryGuard
			: AutoGroupStartLookingStatus.BlockedByCommonGuard;

		return new AutoGroupStartLookingResult(
			status,
			maskId,
			entryRequestType,
			Registration: null,
			guardPlan);
	}

	public static AutoGroupStartLookingResult AlreadyRegistered(int maskId, AutoGroupEntryRequestType entryRequestType)
	{
		return new AutoGroupStartLookingResult(
			AutoGroupStartLookingStatus.AlreadyRegistered,
			maskId,
			entryRequestType,
			Registration: null,
			GuardPlan: null);
	}
}

public enum AutoGroupStartLookingStatus
{
	Registered,
	MissingAutoGroup,
	BlockedByCommonGuard,
	BlockedByEntryGuard,
	AlreadyRegistered,
}

public enum AutoGroupEntryRequestType
{
	NewGroupEntry = 0,
	QuickGroupEntry = 1,
	GroupEntry = 2,
}

public static class AutoGroupEntryRequestTypeParser
{
	public static AutoGroupEntryRequestType? GetTypeById(byte id)
	{
		return id switch
		{
			0 => AutoGroupEntryRequestType.NewGroupEntry,
			1 => AutoGroupEntryRequestType.QuickGroupEntry,
			2 => AutoGroupEntryRequestType.GroupEntry,
			_ => null,
		};
	}
}
