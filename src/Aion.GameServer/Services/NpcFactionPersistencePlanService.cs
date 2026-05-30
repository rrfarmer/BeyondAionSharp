using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum NpcFactionPersistenceState
{
	New,
	UpdateRequired,
	Updated,
	Deleted,
	NoAction,
}

public enum NpcFactionPersistenceOperationAction
{
	Insert,
	Update,
}

public enum NpcFactionPersistencePlanStatus
{
	NoChanges,
	Ready,
}

public sealed record NpcFactionPersistenceStateEntry(PlayerNpcFactionState FactionState, NpcFactionPersistenceState PersistenceState);

public sealed record NpcFactionPersistenceOperationDescriptor(
	int Order,
	NpcFactionPersistenceOperationAction Action,
	int FactionId,
	string JavaSource,
	bool IsLive,
	PlayerNpcFactionState FactionState
);

public sealed record NpcFactionPersistencePlan(
	NpcFactionPersistencePlanStatus Status,
	IReadOnlyList<NpcFactionPersistenceOperationDescriptor> Descriptors
)
{
	public bool HasOperations => Status == NpcFactionPersistencePlanStatus.Ready;
}

public static class NpcFactionPersistencePlanService
{
	// Java parity: dao/PlayerNpcFactionsDAO persists faction slot changes through insert/update operations
	// after the runtime NpcFactions logic has already decided which entries are new or dirty.
	private const string InsertJavaSource = "game-server/src/com/aionemu/gameserver/dao/PlayerNpcFactionsDAO.java#insertNpcFaction";
	private const string UpdateJavaSource = "game-server/src/com/aionemu/gameserver/dao/PlayerNpcFactionsDAO.java#updateNpcFaction";

	public static NpcFactionPersistencePlan CreatePlan(IEnumerable<NpcFactionPersistenceStateEntry> factionStates)
	{
		// Java parity: this planner preserves the DAO-facing write order for newly created and updated
		// faction rows without performing any live persistence.
		ArgumentNullException.ThrowIfNull(factionStates);

		var descriptors = new List<NpcFactionPersistenceOperationDescriptor>();
		var order = 1;

		foreach (var entry in factionStates)
		{
			switch (entry.PersistenceState)
			{
				case NpcFactionPersistenceState.New:
					descriptors.Add(
						new NpcFactionPersistenceOperationDescriptor(
							order++,
							NpcFactionPersistenceOperationAction.Insert,
							entry.FactionState.FactionId,
							InsertJavaSource,
							IsLive: false,
							FactionState: entry.FactionState
						)
					);
					break;
				case NpcFactionPersistenceState.UpdateRequired:
					descriptors.Add(
						new NpcFactionPersistenceOperationDescriptor(
							order++,
							NpcFactionPersistenceOperationAction.Update,
							entry.FactionState.FactionId,
							UpdateJavaSource,
							IsLive: false,
							FactionState: entry.FactionState
						)
					);
					break;
			}
		}

		return new NpcFactionPersistencePlan(
			descriptors.Count == 0 ? NpcFactionPersistencePlanStatus.NoChanges : NpcFactionPersistencePlanStatus.Ready,
			descriptors
		);
	}
}
