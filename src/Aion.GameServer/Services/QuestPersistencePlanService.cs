using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestPersistenceState
{
	New,
	UpdateRequired,
	Updated,
	Deleted,
	NoAction,
}

public enum QuestPersistenceOperationAction
{
	Delete,
	Insert,
	Update,
}

public enum QuestPersistencePlanStatus
{
	NoChanges,
	Ready,
}

public sealed record QuestPersistenceStateEntry(PlayerQuestState QuestState, QuestPersistenceState PersistenceState);

public sealed record QuestPersistenceOperationDescriptor(
	int Order,
	QuestPersistenceOperationAction Action,
	int QuestId,
	string JavaSource,
	bool IsLive,
	PlayerQuestState? QuestState = null,
	bool FromDeletedQuestIdSet = false
);

public sealed record QuestPersistencePlan(QuestPersistencePlanStatus Status, IReadOnlyList<QuestPersistenceOperationDescriptor> Descriptors)
{
	public bool HasOperations => Status == QuestPersistencePlanStatus.Ready;
}

public static class QuestPersistencePlanService
{
	// Java parity: dao/PlayerQuestListDAO persists quest-state mutations as delete, insert, and update
	// operations after QuestState changes have already been computed in memory.
	private const string DeleteJavaSource = "game-server/src/com/aionemu/gameserver/dao/PlayerQuestListDAO.java#deleteQuest";
	private const string InsertJavaSource = "game-server/src/com/aionemu/gameserver/dao/PlayerQuestListDAO.java#addQuests";
	private const string UpdateJavaSource = "game-server/src/com/aionemu/gameserver/dao/PlayerQuestListDAO.java#updateQuests";

	public static QuestPersistencePlan CreatePlan(IEnumerable<QuestPersistenceStateEntry> questStates, IEnumerable<int>? deletedQuestIds = null)
	{
		// Java parity: this plan keeps the Java DAO ordering explicit by scheduling deletes first,
		// then inserts, then updates, including externally supplied deleted quest ids.
		ArgumentNullException.ThrowIfNull(questStates);

		var entries = questStates.ToList();
		var deletedIds = deletedQuestIds?.ToList() ?? [];
		var descriptors = new List<QuestPersistenceOperationDescriptor>();
		var order = 1;

		foreach (var entry in entries.Where(entry => entry.PersistenceState == QuestPersistenceState.Deleted).OrderBy(entry => entry.QuestState.QuestId))
		{
			descriptors.Add(
				new QuestPersistenceOperationDescriptor(
					order++,
					QuestPersistenceOperationAction.Delete,
					entry.QuestState.QuestId,
					DeleteJavaSource,
					IsLive: false,
					QuestState: entry.QuestState
				)
			);
		}

		foreach (var questId in deletedIds)
		{
			descriptors.Add(
				new QuestPersistenceOperationDescriptor(
					order++,
					QuestPersistenceOperationAction.Delete,
					questId,
					DeleteJavaSource,
					IsLive: false,
					FromDeletedQuestIdSet: true
				)
			);
		}

		foreach (var entry in entries.Where(entry => entry.PersistenceState == QuestPersistenceState.New).OrderBy(entry => entry.QuestState.QuestId))
		{
			descriptors.Add(
				new QuestPersistenceOperationDescriptor(
					order++,
					QuestPersistenceOperationAction.Insert,
					entry.QuestState.QuestId,
					InsertJavaSource,
					IsLive: false,
					QuestState: entry.QuestState
				)
			);
		}

		foreach (
			var entry in entries.Where(entry => entry.PersistenceState == QuestPersistenceState.UpdateRequired).OrderBy(entry => entry.QuestState.QuestId)
		)
		{
			descriptors.Add(
				new QuestPersistenceOperationDescriptor(
					order++,
					QuestPersistenceOperationAction.Update,
					entry.QuestState.QuestId,
					UpdateJavaSource,
					IsLive: false,
					QuestState: entry.QuestState
				)
			);
		}

		return new QuestPersistencePlan(descriptors.Count == 0 ? QuestPersistencePlanStatus.NoChanges : QuestPersistencePlanStatus.Ready, descriptors);
	}
}
