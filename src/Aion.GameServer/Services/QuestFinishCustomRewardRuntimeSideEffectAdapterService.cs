using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class QuestFinishCustomRewardRuntimeSideEffectAdapterService
{
	private readonly QuestXpCustomRewardRuntimeInputAdapterService _levelChangeInputAdapter;

	public QuestFinishCustomRewardRuntimeSideEffectAdapterService(
		QuestXpCustomRewardRuntimeInputAdapterService levelChangeInputAdapter)
	{
		_levelChangeInputAdapter = levelChangeInputAdapter;
	}

	public async ValueTask<QuestFinishCustomRewardRuntimeSideEffectAdapterResult> CreateContextAsync(
		QuestFinishRewardSideEffectContext context,
		QuestXpCustomRewardRuntimeInputAdapterOptions options,
		CancellationToken cancellationToken = default)
	{
		// Java parity breadcrumb: QuestService.giveReward -> PlayerCommonData.addExp may enter
		// PlayerController.onLevelChange, whose custom reward hooks run after skill learning.
		if (!options.EnableCustomRewardExecution)
		{
			return QuestFinishCustomRewardRuntimeSideEffectAdapterResult.Disabled(context);
		}

		if (context.LevelChangeContextInput is null)
		{
			return QuestFinishCustomRewardRuntimeSideEffectAdapterResult.MissingDependencyResult(
				context,
				"levelChangeContextInput");
		}

		var adapterResult = await _levelChangeInputAdapter.CreateInputAsync(
			context.Player,
			context.LevelChangeContextInput,
			options,
			cancellationToken);

		return adapterResult.Applied
			? QuestFinishCustomRewardRuntimeSideEffectAdapterResult.Created(
				context with { LevelChangeContextInput = adapterResult.Input },
				adapterResult)
			: QuestFinishCustomRewardRuntimeSideEffectAdapterResult.FromInputAdapter(context, adapterResult);
	}
}

public sealed record QuestFinishCustomRewardRuntimeSideEffectAdapterResult(
	QuestXpCustomRewardRuntimeInputAdapterStatus Status,
	QuestFinishRewardSideEffectContext Context,
	string JavaSource,
	QuestXpCustomRewardRuntimeInputAdapterResult? InputAdapterResult = null,
	string? MissingDependency = null)
{
	public bool Applied => Status == QuestXpCustomRewardRuntimeInputAdapterStatus.Created;

	public static QuestFinishCustomRewardRuntimeSideEffectAdapterResult Disabled(
		QuestFinishRewardSideEffectContext context)
	{
		return new QuestFinishCustomRewardRuntimeSideEffectAdapterResult(
			QuestXpCustomRewardRuntimeInputAdapterStatus.Disabled,
			context,
			"QuestService.giveReward -> PlayerCommonData.addExp -> PlayerController.onLevelChange custom rewards disabled by C# opt-in gate");
	}

	public static QuestFinishCustomRewardRuntimeSideEffectAdapterResult MissingDependencyResult(
		QuestFinishRewardSideEffectContext context,
		string missingDependency)
	{
		return new QuestFinishCustomRewardRuntimeSideEffectAdapterResult(
			QuestXpCustomRewardRuntimeInputAdapterStatus.MissingDependency,
			context,
			"Quest finish custom reward side-effect context adapter missing C# runtime dependency",
			MissingDependency: missingDependency);
	}

	public static QuestFinishCustomRewardRuntimeSideEffectAdapterResult Created(
		QuestFinishRewardSideEffectContext context,
		QuestXpCustomRewardRuntimeInputAdapterResult inputAdapterResult)
	{
		return new QuestFinishCustomRewardRuntimeSideEffectAdapterResult(
			QuestXpCustomRewardRuntimeInputAdapterStatus.Created,
			context,
			"QuestService.giveReward -> PlayerCommonData.addExp -> PlayerController.onLevelChange -> BonusPackService.addPlayerCustomReward -> FactionPackService.addPlayerCustomReward",
			inputAdapterResult);
	}

	public static QuestFinishCustomRewardRuntimeSideEffectAdapterResult FromInputAdapter(
		QuestFinishRewardSideEffectContext context,
		QuestXpCustomRewardRuntimeInputAdapterResult inputAdapterResult)
	{
		return new QuestFinishCustomRewardRuntimeSideEffectAdapterResult(
			inputAdapterResult.Status,
			context,
			inputAdapterResult.JavaSource,
			inputAdapterResult,
			inputAdapterResult.MissingDependency);
	}
}
