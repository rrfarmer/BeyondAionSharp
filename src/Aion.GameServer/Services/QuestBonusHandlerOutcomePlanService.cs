namespace Aion.GameServer.Services;

public sealed class QuestBonusHandlerOutcomePlanService
{
	private static readonly IReadOnlyList<QuestBonusHandlerRegistration> AuditedRegistrations =
	[
		new(80016, "MOVIE", QuestBonusHandlerKind.Movie, [103, 104]),
		new(80018, "MOVIE", QuestBonusHandlerKind.Movie, [135, 136]),
		new(80034, "LUNAR", QuestBonusHandlerKind.LunarGate),
		new(80035, "LUNAR", QuestBonusHandlerKind.LunarGate),
		new(80036, "LUNAR", QuestBonusHandlerKind.LunarGate),
		new(80037, "LUNAR", QuestBonusHandlerKind.LunarGate),
		new(80038, "LUNAR", QuestBonusHandlerKind.LunarGate),
		new(80039, "LUNAR", QuestBonusHandlerKind.LunarGate),
		new(80137, "RIFT", QuestBonusHandlerKind.RiftGate),
		new(80139, "RIFT", QuestBonusHandlerKind.RiftGate),
		new(80145, "RIFT", QuestBonusHandlerKind.RiftGate),
		new(80147, "RIFT", QuestBonusHandlerKind.RiftGate),
		new(80149, "RIFT", QuestBonusHandlerKind.RiftGate),
	];

	public QuestBonusHandlerOutcomePlan CreatePlan(QuestBonusHandlerOutcomeInput input) =>
		CreatePlan(input, AuditedRegistrations);

	public QuestBonusHandlerOutcomePlan CreateHandlerExceptionPlan(
		QuestBonusHandlerOutcomeInput input,
		QuestBonusHandlerRegistration? registration = null)
	{
		ArgumentNullException.ThrowIfNull(input);

		// Java parity: QuestEngine#onBonusApplyEvent catches any exception
		// escaping handler execution and returns HandlerResult.FAILED, which
		// suppresses the later BonusService call in QuestService#getRewardItems.
		return new QuestBonusHandlerOutcomePlan(
			input,
			QuestBonusHandlerResult.Failed,
			QuestBonusHandlerOutcomeStatus.HandlerException,
			registration?.QuestId,
			registration?.HandlerKind,
			[],
			[]);
	}

	public QuestBonusHandlerOutcomePlan CreatePlan(
		QuestBonusHandlerOutcomeInput input,
		IEnumerable<QuestBonusHandlerRegistration> registrations)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(registrations);

		// Java parity: questEngine/QuestEngine#onBonusApplyEvent. This remains
		// non-live and does not invoke dynamic handlers; it models the audited
		// handler result and side-effect intent surface only.
		var normalizedBonusType = Normalize(input.BonusType);
		var matchingRegistrations = registrations
			.Where(registration => string.Equals(Normalize(registration.BonusType), normalizedBonusType, StringComparison.Ordinal))
			.ToArray();
		if (matchingRegistrations.Length == 0)
			return Unknown(input, QuestBonusHandlerOutcomeStatus.NoRegisteredHandler);

		var loadedQuestIds = input.LoadedQuestIds;
		var selectedRegistration = matchingRegistrations.FirstOrDefault(registration =>
			loadedQuestIds == null || loadedQuestIds.Contains(registration.QuestId));
		if (selectedRegistration == null)
			return Unknown(input, QuestBonusHandlerOutcomeStatus.NoLoadedHandler);

		var questState = input.QuestStates.GetValueOrDefault(selectedRegistration.QuestId);
		return selectedRegistration.HandlerKind switch
		{
			QuestBonusHandlerKind.Movie => CreateMovieOutcome(input, selectedRegistration, questState),
			QuestBonusHandlerKind.LunarGate or QuestBonusHandlerKind.RiftGate => CreateGateOutcome(input, selectedRegistration, questState),
			_ => Unknown(input, QuestBonusHandlerOutcomeStatus.HandlerReturnedUnknown, selectedRegistration),
		};
	}

	private static QuestBonusHandlerOutcomePlan CreateMovieOutcome(
		QuestBonusHandlerOutcomeInput input,
		QuestBonusHandlerRegistration registration,
		QuestBonusHandlerQuestState? questState)
	{
		if (!string.Equals(Normalize(questState?.Status), "REWARD", StringComparison.Ordinal))
			return Failed(input, registration);

		var directItems = questState?.CompleteCount == 9
			? new[] { new QuestFinishRewardItem(188051106, 1) }
			: [];
		var sideEffects = registration.MovieIds.Count > 0
			? new[] { new QuestBonusHandlerSideEffectIntent(QuestBonusHandlerSideEffectKind.RandomMovie, registration.MovieIds) }
			: [];

		return new QuestBonusHandlerOutcomePlan(
			input,
			QuestBonusHandlerResult.Success,
			QuestBonusHandlerOutcomeStatus.HandlerSucceeded,
			registration.QuestId,
			registration.HandlerKind,
			directItems,
			sideEffects);
	}

	private static QuestBonusHandlerOutcomePlan CreateGateOutcome(
		QuestBonusHandlerOutcomeInput input,
		QuestBonusHandlerRegistration registration,
		QuestBonusHandlerQuestState? questState)
	{
		var status = Normalize(questState?.Status);
		if ((status == "START" || status == "COMPLETE") && questState?.Var0 == 0)
		{
			return new QuestBonusHandlerOutcomePlan(
				input,
				QuestBonusHandlerResult.Success,
				QuestBonusHandlerOutcomeStatus.HandlerSucceeded,
				registration.QuestId,
				registration.HandlerKind,
				[],
				[]);
		}

		return Failed(input, registration);
	}

	private static QuestBonusHandlerOutcomePlan Failed(
		QuestBonusHandlerOutcomeInput input,
		QuestBonusHandlerRegistration registration) =>
		new(
			input,
			QuestBonusHandlerResult.Failed,
			QuestBonusHandlerOutcomeStatus.HandlerFailed,
			registration.QuestId,
			registration.HandlerKind,
			[],
			[]);

	private static QuestBonusHandlerOutcomePlan Unknown(
		QuestBonusHandlerOutcomeInput input,
		QuestBonusHandlerOutcomeStatus status,
		QuestBonusHandlerRegistration? registration = null) =>
		new(
			input,
			QuestBonusHandlerResult.Unknown,
			status,
			registration?.QuestId,
			registration?.HandlerKind,
			[],
			[]);

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}

public sealed record QuestBonusHandlerOutcomeInput(
	string BonusType,
	IReadOnlyDictionary<int, QuestBonusHandlerQuestState> QuestStates,
	IReadOnlySet<int>? LoadedQuestIds = null);

public sealed record QuestBonusHandlerQuestState(
	string Status,
	int Var0 = 0,
	int CompleteCount = 0);

public sealed record QuestBonusHandlerRegistration(
	int QuestId,
	string BonusType,
	QuestBonusHandlerKind HandlerKind,
	IReadOnlyList<int>? MovieIds = null)
{
	public IReadOnlyList<int> MovieIds { get; init; } = MovieIds ?? [];
}

public sealed record QuestBonusHandlerOutcomePlan(
	QuestBonusHandlerOutcomeInput Input,
	QuestBonusHandlerResult Result,
	QuestBonusHandlerOutcomeStatus Status,
	int? HandlerQuestId,
	QuestBonusHandlerKind? HandlerKind,
	IReadOnlyList<QuestFinishRewardItem> DirectRewardItems,
	IReadOnlyList<QuestBonusHandlerSideEffectIntent> SideEffects);

public sealed record QuestBonusHandlerSideEffectIntent(
	QuestBonusHandlerSideEffectKind Kind,
	IReadOnlyList<int> CandidateIds);

public enum QuestBonusHandlerKind
{
	Movie,
	LunarGate,
	RiftGate,
}

public enum QuestBonusHandlerResult
{
	Unknown,
	Success,
	Failed,
}

public enum QuestBonusHandlerOutcomeStatus
{
	NoRegisteredHandler,
	NoLoadedHandler,
	HandlerReturnedUnknown,
	HandlerSucceeded,
	HandlerFailed,
	HandlerException,
}

public enum QuestBonusHandlerSideEffectKind
{
	RandomMovie,
}
