namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerNpcFactionState(
	int FactionId,
	bool IsActive,
	bool IsMentor,
	int TimeEpochSeconds,
	PlayerNpcFactionQuestState State,
	int QuestId = 0);

public sealed class PlayerNpcFactionsSnapshot
{
	private readonly IReadOnlyDictionary<int, PlayerNpcFactionState> _factions;
	private readonly PlayerNpcFactionState?[] _activeNpcFaction = new PlayerNpcFactionState?[2];
	private readonly int[] _timeLimit = [0, 0];

	public PlayerNpcFactionsSnapshot(IEnumerable<PlayerNpcFactionState> factions, int currentEpochSeconds = 0)
	{
		// Java parity breadcrumb: model/gameobjects/player/npcFaction/NpcFactions.addNpcFaction.
		var byId = new Dictionary<int, PlayerNpcFactionState>();
		foreach (var faction in factions)
		{
			byId[faction.FactionId] = faction;
			var type = faction.IsMentor ? 1 : 0;
			if (faction.IsActive)
				_activeNpcFaction[type] = faction;

			if (faction.TimeEpochSeconds == -1)
			{
				_timeLimit[type] = currentEpochSeconds;
			}
			else if (_timeLimit[type] < faction.TimeEpochSeconds
				&& faction.State == PlayerNpcFactionQuestState.Complete)
			{
				_timeLimit[type] = faction.TimeEpochSeconds;
			}
		}

		_factions = byId;
	}

	public static PlayerNpcFactionsSnapshot Empty { get; } = new(Array.Empty<PlayerNpcFactionState>());

	public IReadOnlyCollection<PlayerNpcFactionState> Factions => _factions.Values.ToArray();

	public bool HasActiveFaction(int factionId)
	{
		return _factions.TryGetValue(factionId, out var faction) && faction.IsActive;
	}

	public bool TryGetFaction(int factionId, out PlayerNpcFactionState? faction)
	{
		return _factions.TryGetValue(factionId, out faction);
	}

	public PlayerNpcFactionState? GetActiveFaction(bool isMentor)
	{
		// Java parity breadcrumb: NpcFactions.getActiveNpcFaction(boolean mentor).
		return _activeNpcFaction[isMentor ? 1 : 0];
	}

	public IReadOnlyList<int> GetReusableDailyQuestIds(int currentEpochSeconds)
	{
		// Java parity breadcrumb: NpcFactions.sendDailyQuest reuses the assigned quest id when
		// an active NOTING faction still has a future time value. Random replacement selection
		// through QuestsData.getQuestsByNpcFaction is intentionally handled separately.
		var questIds = new List<int>();
		for (var i = 0; i < _activeNpcFaction.Length; i++)
		{
			var faction = _activeNpcFaction[i];
			if (faction == null || !faction.IsActive)
				continue;
			if (_timeLimit[i] > currentEpochSeconds)
				continue;
			if (faction.State != PlayerNpcFactionQuestState.Noting)
				continue;
			if (faction.TimeEpochSeconds <= currentEpochSeconds || faction.QuestId == 0)
				continue;

			questIds.Add(faction.QuestId);
		}

		return questIds;
	}

	public bool CanStartAssignedQuest(int factionId, int questId)
	{
		// Java parity breadcrumb: QuestService.startQuest rejects NPC faction quest starts
		// unless the exact faction is active and its assigned quest id matches.
		return _factions.TryGetValue(factionId, out var faction)
			&& faction.IsActive
			&& faction.QuestId == questId;
	}

	public bool CanStartQuest(bool isMentorQuest, int currentEpochSeconds)
	{
		// Java parity breadcrumb: NpcFactions.canStartQuest uses the active mentor/non-mentor slot
		// and a slot-level timeLimit, not the exact faction row's time.
		var type = isMentorQuest ? 1 : 0;
		return _activeNpcFaction[type] != null && _timeLimit[type] < currentEpochSeconds;
	}

	public PlayerNpcFactionCompletionResult CompleteActiveQuest(bool isMentorQuest, int nextResetEpochSeconds)
	{
		// Java parity breadcrumb: NpcFactions.completeQuest updates the active mentor/non-mentor
		// slot chosen from QuestTemplate.isMentor(), not a direct npcfaction_id lookup.
		var type = isMentorQuest ? 1 : 0;
		var activeFaction = _activeNpcFaction[type];
		if (activeFaction is null)
			return new PlayerNpcFactionCompletionResult(
				PlayerNpcFactionCompletionStatus.NoActiveFaction,
				this,
				null);

		var completedFaction = activeFaction with
		{
			TimeEpochSeconds = nextResetEpochSeconds,
			State = PlayerNpcFactionQuestState.Complete,
		};
		var updatedFactions = _factions.Values
			.Select(faction => faction.FactionId == completedFaction.FactionId ? completedFaction : faction)
			.ToArray();

		return new PlayerNpcFactionCompletionResult(
			PlayerNpcFactionCompletionStatus.Applied,
			new PlayerNpcFactionsSnapshot(updatedFactions),
			completedFaction);
	}

	public PlayerNpcFactionAbortResult AbortQuest(int factionId)
	{
		// Java parity breadcrumb: NpcFactions.abortQuest looks up the exact npcfaction_id,
		// requires an active row, and resets only the faction quest state to NOTING.
		if (!_factions.TryGetValue(factionId, out var faction))
			return new PlayerNpcFactionAbortResult(PlayerNpcFactionAbortStatus.NoFaction, this, null, null);
		if (!faction.IsActive)
			return new PlayerNpcFactionAbortResult(PlayerNpcFactionAbortStatus.InactiveFaction, this, faction, faction);

		var abortedFaction = faction with { State = PlayerNpcFactionQuestState.Noting };
		var updatedFactions = _factions.Values
			.Select(candidate => candidate.FactionId == factionId ? abortedFaction : candidate)
			.ToArray();

		return new PlayerNpcFactionAbortResult(
			PlayerNpcFactionAbortStatus.Applied,
			new PlayerNpcFactionsSnapshot(updatedFactions),
			faction,
			abortedFaction);
	}
}

public enum PlayerNpcFactionQuestState
{
	// Java typo is intentional: ENpcFactionQuestState.NOTING.
	Noting = 0,
	Start = 1,
	Complete = 2,
}

public enum PlayerNpcFactionCompletionStatus
{
	Applied,
	NoActiveFaction,
}

public sealed record PlayerNpcFactionCompletionResult(
	PlayerNpcFactionCompletionStatus Status,
	PlayerNpcFactionsSnapshot Snapshot,
	PlayerNpcFactionState? CompletedFaction)
{
	public bool Applied => Status == PlayerNpcFactionCompletionStatus.Applied;
}

public enum PlayerNpcFactionAbortStatus
{
	Applied,
	NoFaction,
	InactiveFaction,
}

public sealed record PlayerNpcFactionAbortResult(
	PlayerNpcFactionAbortStatus Status,
	PlayerNpcFactionsSnapshot Snapshot,
	PlayerNpcFactionState? PreviousFaction,
	PlayerNpcFactionState? AbortedFaction)
{
	public bool Applied => Status == PlayerNpcFactionAbortStatus.Applied;
}
