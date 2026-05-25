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

	public bool HasActiveFaction(int factionId)
	{
		return _factions.TryGetValue(factionId, out var faction) && faction.IsActive;
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
}

public enum PlayerNpcFactionQuestState
{
	// Java typo is intentional: ENpcFactionQuestState.NOTING.
	Noting = 0,
	Start = 1,
	Complete = 2,
}
