namespace Aion.GameServer.Services;

public enum QuestCompletionCallbackPlanStatus
{
	NoHandlers,
	Ready,
	StoppedByHandlerException,
}

public enum QuestCompletionCallbackAction
{
	InvokeHandler,
	SkipMissingHandler,
}

public sealed record QuestCompletionCallbackRegistration(
	int RegisteredQuestId,
	string HandlerJavaSource,
	bool HandlerExists = true,
	bool ThrowsBeforeReturning = false,
	bool UsesDefaultFollowUp = false,
	int? FollowUpQuestId = null,
	QuestCompletionFollowUpPlan? FollowUpPlan = null
);

public sealed record QuestCompletionCallbackDescriptor(
	int Order,
	QuestCompletionCallbackAction Action,
	int RegisteredQuestId,
	int CompletedQuestId,
	string HandlerJavaSource,
	bool IsLive,
	bool UsesSharedQuestEnv,
	bool StopsRemainingHandlers = false,
	bool UsesDefaultFollowUp = false,
	int? FollowUpQuestId = null,
	QuestCompletionFollowUpPlan? FollowUpPlan = null
);

public sealed record QuestCompletionCallbackPlan(
	QuestCompletionCallbackPlanStatus Status,
	IReadOnlyList<QuestCompletionCallbackDescriptor> Descriptors
)
{
	public bool HasHandlers => Status != QuestCompletionCallbackPlanStatus.NoHandlers;
}

public static class QuestCompletionCallbackPlanService
{
	public static QuestCompletionCallbackPlan CreatePlan(int completedQuestId, IEnumerable<QuestCompletionCallbackRegistration> registrations)
	{
		// Java parity: QuestEngine.onQuestComplete iterates registered completion handlers in order,
		// reuses the shared QuestEnv, and stops further callbacks when a handler throws.
		ArgumentNullException.ThrowIfNull(registrations);

		var descriptors = new List<QuestCompletionCallbackDescriptor>();
		var registeredQuestIds = new HashSet<int>();
		var order = 1;

		foreach (var registration in registrations)
		{
			if (!registeredQuestIds.Add(registration.RegisteredQuestId))
				continue;

			var action = registration.HandlerExists ? QuestCompletionCallbackAction.InvokeHandler : QuestCompletionCallbackAction.SkipMissingHandler;
			descriptors.Add(
				new QuestCompletionCallbackDescriptor(
					order++,
					action,
					registration.RegisteredQuestId,
					completedQuestId,
					registration.HandlerJavaSource,
					IsLive: false,
					UsesSharedQuestEnv: true,
					StopsRemainingHandlers: registration.ThrowsBeforeReturning,
					UsesDefaultFollowUp: registration.UsesDefaultFollowUp,
					FollowUpQuestId: registration.FollowUpQuestId,
					FollowUpPlan: registration.FollowUpPlan
				)
			);

			if (registration.ThrowsBeforeReturning)
			{
				return new QuestCompletionCallbackPlan(QuestCompletionCallbackPlanStatus.StoppedByHandlerException, descriptors);
			}
		}

		return new QuestCompletionCallbackPlan(
			descriptors.Count == 0 ? QuestCompletionCallbackPlanStatus.NoHandlers : QuestCompletionCallbackPlanStatus.Ready,
			descriptors
		);
	}
}
