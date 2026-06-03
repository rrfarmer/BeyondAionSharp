using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

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
		InstanceCooltimeTable? instanceCooltimes = null)
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
			instanceCooltimes);
		if (!entryGuard.CanRegister)
			return AutoGroupStartLookingResult.Blocked(maskId, entryRequestType, entryGuard);

		lock (_sync)
		{
			if (_lookingPartiesByMaskId.TryGetValue(maskId, out var parties)
				&& parties.Any(party => party.MemberObjectIds.Contains(player.ObjectId)))
			{
				return AutoGroupStartLookingResult.AlreadyRegistered(maskId, entryRequestType);
			}

			var registration = new AutoGroupLookingPartyRegistration(maskId, player.ObjectId, memberObjectIds);
			if (parties == null)
			{
				parties = [];
				_lookingPartiesByMaskId[maskId] = parties;
			}

			parties.Add(registration);
			return AutoGroupStartLookingResult.Registered(maskId, entryRequestType, registration, guard);
		}
	}

	public AutoGroupLookingPartyRegistration RegisterLookingParty(
		int maskId,
		IReadOnlyCollection<int> memberObjectIds)
	{
		if (memberObjectIds.Count == 0)
			throw new ArgumentException("At least one member object id is required.", nameof(memberObjectIds));

		var registration = new AutoGroupLookingPartyRegistration(maskId, memberObjectIds.First(), memberObjectIds.ToArray());
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

	private static AutoGroupRegistrationGuardPlan CreateEntryRequestGuard(
		Player player,
		AutoGroupSummary autoGroup,
		AutoGroupEntryRequestType entryRequestType,
		IReadOnlyList<int> memberObjectIds,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime,
		InstanceCooltimeTable? instanceCooltimes)
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
				instanceCooltimes),
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

	private static AutoGroupRegistrationGuardPlan CreateGroupEntryGuard(
		Player player,
		AutoGroupSummary autoGroup,
		IReadOnlyList<int> memberObjectIds,
		PlayerGroupRuntime? groupRuntime,
		PlayerAllianceRuntime? allianceRuntime,
		InstanceCooltimeTable? instanceCooltimes)
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

		return AllowedEntryGuard(player.Level, autoGroup.MaskId, AutoGroupEntryRequestType.GroupEntry);
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
}

public sealed record AutoGroupLookingPartyRegistration(
	int MaskId,
	int LeaderObjectId,
	IReadOnlyList<int> MemberObjectIds);

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
	AutoGroupRegistrationGuardPlan? GuardPlan)
{
	public bool RegisteredQueue => Status == AutoGroupStartLookingStatus.Registered;

	public static AutoGroupStartLookingResult Registered(
		int maskId,
		AutoGroupEntryRequestType entryRequestType,
		AutoGroupLookingPartyRegistration registration,
		AutoGroupRegistrationGuardPlan guardPlan)
	{
		return new AutoGroupStartLookingResult(
			AutoGroupStartLookingStatus.Registered,
			maskId,
			entryRequestType,
			registration,
			guardPlan);
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
