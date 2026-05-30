using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class QuestLevelChangedCallbackPlanService
{
	public static QuestLevelChangedCallbackPlan CreatePlan(
		string? playerRace,
		IEnumerable<QuestLevelChangedRegistration>? registrations,
		IEnumerable<PlayerQuestState>? questStates
	)
	{
		// Java parity: QuestEngine.registerOnLevelChanged stores race-scoped registrations and
		// QuestEngine.onLevelChanged dispatches only non-complete quests with registered handlers.
		// This planner turns that callback eligibility into ordered descriptors.
		if (registrations == null)
			return QuestLevelChangedCallbackPlan.MissingRegistrations(playerRace);

		var questStatesById = questStates?.ToDictionary(state => state.QuestId) ?? new Dictionary<int, PlayerQuestState>();
		var descriptors = new List<QuestLevelChangedCallbackDescriptor>();
		var seenQuestIds = new HashSet<int>();
		foreach (var registration in registrations)
		{
			var normalizedRace = NormalizeRace(playerRace);
			if (!IsRegisteredForRace(registration.RacePermitted, normalizedRace))
			{
				descriptors.Add(
					new QuestLevelChangedCallbackDescriptor(
						registration.QuestId,
						QuestLevelChangedCallbackDescriptorStatus.SkippedRace,
						registration.RacePermitted,
						registration.HasHandler,
						QuestState: null,
						"QuestEngine.registerOnLevelChanged",
						Notes: "Java stores registered quest ids in race-specific on-level-up lists; this registration is not in the player's race list."
					)
				);
				continue;
			}

			if (!seenQuestIds.Add(registration.QuestId))
				continue;

			questStatesById.TryGetValue(registration.QuestId, out var questState);
			if (questState?.IsComplete == true)
			{
				descriptors.Add(
					new QuestLevelChangedCallbackDescriptor(
						registration.QuestId,
						QuestLevelChangedCallbackDescriptorStatus.SkippedComplete,
						registration.RacePermitted,
						registration.HasHandler,
						questState,
						"QuestEngine.onLevelChanged -> QuestState.COMPLETE guard",
						Notes: "Java skips level-change handlers when the quest state already exists with QuestStatus.COMPLETE."
					)
				);
				continue;
			}

			if (!registration.HasHandler)
			{
				descriptors.Add(
					new QuestLevelChangedCallbackDescriptor(
						registration.QuestId,
						QuestLevelChangedCallbackDescriptorStatus.SkippedMissingHandler,
						registration.RacePermitted,
						registration.HasHandler,
						questState,
						"QuestEngine.onLevelChanged -> getQuestHandlerByQuestId",
						Notes: "Java does not dispatch when no quest handler is registered for the quest id."
					)
				);
				continue;
			}

			descriptors.Add(
				new QuestLevelChangedCallbackDescriptor(
					registration.QuestId,
					QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch,
					registration.RacePermitted,
					registration.HasHandler,
					questState,
					"QuestEngine.onLevelChanged -> AbstractQuestHandler.onLevelChangedEvent",
					Notes: questState == null
						? "Java dispatches when the player has no quest state for the registered quest."
						: "Java dispatches when the quest state exists but is not COMPLETE."
				)
			);
		}

		var plannedDispatches = descriptors.Count(descriptor => descriptor.Status == QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch);
		var status =
			plannedDispatches > 0 ? QuestLevelChangedCallbackPlanStatus.Applied
			: descriptors.Count == 0 ? QuestLevelChangedCallbackPlanStatus.NoRegisteredCallbacks
			: QuestLevelChangedCallbackPlanStatus.NoDispatches;
		return new QuestLevelChangedCallbackPlan(status, playerRace, descriptors);
	}

	private static bool IsRegisteredForRace(string? racePermitted, string normalizedPlayerRace)
	{
		if (string.IsNullOrWhiteSpace(racePermitted))
			return true;

		return string.Equals(NormalizeRace(racePermitted), normalizedPlayerRace, StringComparison.Ordinal);
	}

	private static string NormalizeRace(string? race)
	{
		if (string.IsNullOrWhiteSpace(race))
			return string.Empty;

		var normalized = race.Trim().ToUpperInvariant();
		return normalized == "ASMODIAN" ? "ASMODIANS" : normalized;
	}
}

public sealed record QuestLevelChangedRegistration(int QuestId, string? RacePermitted, bool HasHandler = true);

public sealed record QuestLevelChangedCallbackPlan(
	QuestLevelChangedCallbackPlanStatus Status,
	string? PlayerRace,
	IReadOnlyList<QuestLevelChangedCallbackDescriptor> Descriptors
)
{
	public bool Applied => Status == QuestLevelChangedCallbackPlanStatus.Applied;

	public static QuestLevelChangedCallbackPlan MissingRegistrations(string? playerRace)
	{
		return new QuestLevelChangedCallbackPlan(
			QuestLevelChangedCallbackPlanStatus.MissingRegistrations,
			playerRace,
			Array.Empty<QuestLevelChangedCallbackDescriptor>()
		);
	}
}

public sealed record QuestLevelChangedCallbackDescriptor(
	int QuestId,
	QuestLevelChangedCallbackDescriptorStatus Status,
	string? RacePermitted,
	bool HasHandler,
	PlayerQuestState? QuestState,
	string JavaSource,
	bool IsLive = false,
	string? Notes = null
);

public enum QuestLevelChangedCallbackPlanStatus
{
	Applied,
	NoDispatches,
	NoRegisteredCallbacks,
	MissingRegistrations,
}

public enum QuestLevelChangedCallbackDescriptorStatus
{
	PlannedDispatch,
	SkippedComplete,
	SkippedMissingHandler,
	SkippedRace,
}
