using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class QuestXpCustomRewardRuntimeInputAdapterService
{
	private readonly CustomLevelRewardExecutionService _customLevelRewardExecutionService;

	public QuestXpCustomRewardRuntimeInputAdapterService(CustomLevelRewardExecutionService customLevelRewardExecutionService)
	{
		_customLevelRewardExecutionService = customLevelRewardExecutionService;
	}

	public async ValueTask<QuestXpCustomRewardRuntimeInputAdapterResult> CreateInputAsync(
		Player? player,
		QuestXpLevelChangeContextFactoryInput input,
		QuestXpCustomRewardRuntimeInputAdapterOptions options,
		CancellationToken cancellationToken = default)
	{
		// Java parity breadcrumb: PlayerController.onLevelChange invokes BonusPackService before FactionPackService.
		if (!options.EnableCustomRewardExecution)
			return QuestXpCustomRewardRuntimeInputAdapterResult.Disabled(input);

		if (options.NextObjectId == null)
			return QuestXpCustomRewardRuntimeInputAdapterResult.MissingDependencyResult(input, "nextObjectId");

		var bonusResult = await _customLevelRewardExecutionService.CreateBonusPackExecutionPlanAsync(
			player,
			options.NextObjectId,
			options.ReceivedTime,
			options.ItemTemplates,
			cancellationToken);
		var factionResult = await _customLevelRewardExecutionService.CreateFactionPackExecutionPlanAsync(
			player,
			options.FactionPackAccountCreationLocalTime,
			options.NextObjectId,
			options.ReceivedTime,
			options.ItemTemplates,
			cancellationToken);

		return QuestXpCustomRewardRuntimeInputAdapterResult.Created(input with
		{
			BonusPackExecutionResult = bonusResult,
			FactionPackExecutionResult = factionResult,
			FactionPackAccountCreationLocalTime = options.FactionPackAccountCreationLocalTime,
			ItemTemplates = options.ItemTemplates ?? input.ItemTemplates,
		});
	}
}

public sealed record QuestXpCustomRewardRuntimeInputAdapterOptions(
	bool EnableCustomRewardExecution = false,
	Func<int>? NextObjectId = null,
	DateTime ReceivedTime = default,
	DateTime FactionPackAccountCreationLocalTime = default,
	ItemTemplateTable? ItemTemplates = null)
{
	public static QuestXpCustomRewardRuntimeInputAdapterOptions Disabled { get; } = new();
}

public sealed record QuestXpCustomRewardRuntimeInputAdapterResult(
	QuestXpCustomRewardRuntimeInputAdapterStatus Status,
	QuestXpLevelChangeContextFactoryInput Input,
	string JavaSource,
	string? MissingDependency = null)
{
	public bool Applied => Status == QuestXpCustomRewardRuntimeInputAdapterStatus.Created;

	public static QuestXpCustomRewardRuntimeInputAdapterResult Disabled(QuestXpLevelChangeContextFactoryInput input)
	{
		return new QuestXpCustomRewardRuntimeInputAdapterResult(
			QuestXpCustomRewardRuntimeInputAdapterStatus.Disabled,
			input,
			"PlayerController.onLevelChange custom reward runtime input adapter disabled by C# opt-in gate");
	}

	public static QuestXpCustomRewardRuntimeInputAdapterResult MissingDependencyResult(
		QuestXpLevelChangeContextFactoryInput input,
		string missingDependency)
	{
		return new QuestXpCustomRewardRuntimeInputAdapterResult(
			QuestXpCustomRewardRuntimeInputAdapterStatus.MissingDependency,
			input,
			"PlayerController.onLevelChange custom reward runtime input adapter missing C# runtime dependency",
			missingDependency);
	}

	public static QuestXpCustomRewardRuntimeInputAdapterResult Created(QuestXpLevelChangeContextFactoryInput input)
	{
		return new QuestXpCustomRewardRuntimeInputAdapterResult(
			QuestXpCustomRewardRuntimeInputAdapterStatus.Created,
			input,
			"PlayerController.onLevelChange -> BonusPackService.addPlayerCustomReward -> FactionPackService.addPlayerCustomReward");
	}
}

public enum QuestXpCustomRewardRuntimeInputAdapterStatus
{
	Disabled,
	MissingDependency,
	Created,
}
