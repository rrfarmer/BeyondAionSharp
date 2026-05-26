using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class QuestFinishCustomRewardRuntimeInputAssemblerService
{
	public static QuestFinishCustomRewardRuntimeInputAssemblerResult CreateOptions(
		QuestFinishCustomRewardRuntimeInputAssemblerInput input)
	{
		// Java parity breadcrumb: FactionPackService.sendRewards converts
		// player.getAccount().getCreationDate() through ServerTime.ofEpochMilli(...).toLocalDateTime().
		if (!input.EnableCustomRewardExecution)
		{
			return QuestFinishCustomRewardRuntimeInputAssemblerResult.Disabled();
		}

		if (!input.AccountCreationEpochMillis.HasValue)
		{
			return QuestFinishCustomRewardRuntimeInputAssemblerResult.MissingDependencyResult("accountCreationEpochMillis");
		}

		if (input.NextObjectId == null)
		{
			return QuestFinishCustomRewardRuntimeInputAssemblerResult.MissingDependencyResult("nextObjectId");
		}

		if (input.ItemTemplates == null)
		{
			return QuestFinishCustomRewardRuntimeInputAssemblerResult.MissingDependencyResult("itemTemplates");
		}

		var factionPackAccountCreationLocalTime = ConvertEpochMillisToServerLocalTime(
			input.AccountCreationEpochMillis.Value,
			input.Options.Core.GetTimeZone());

		return QuestFinishCustomRewardRuntimeInputAssemblerResult.Created(
			new QuestXpCustomRewardRuntimeInputAdapterOptions(
				EnableCustomRewardExecution: true,
				input.NextObjectId,
				input.ReceivedTime,
				factionPackAccountCreationLocalTime,
				input.ItemTemplates));
	}

	public static DateTime ConvertEpochMillisToServerLocalTime(long epochMillis, TimeZoneInfo serverTimeZone)
	{
		// Java parity breadcrumb: ServerTime.ofEpochMilli uses Instant.ofEpochMilli and ZoneId,
		// then FactionPackService compares the resulting LocalDateTime to static local windows.
		return TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(epochMillis), serverTimeZone).DateTime;
	}
}

public sealed record QuestFinishCustomRewardRuntimeInputAssemblerInput(
	bool EnableCustomRewardExecution,
	GameServerOptions Options,
	long? AccountCreationEpochMillis = null,
	Func<int>? NextObjectId = null,
	DateTime ReceivedTime = default,
	ItemTemplateTable? ItemTemplates = null);

public sealed record QuestFinishCustomRewardRuntimeInputAssemblerResult(
	QuestFinishCustomRewardRuntimeInputAssemblerStatus Status,
	QuestXpCustomRewardRuntimeInputAdapterOptions Options,
	string JavaSource,
	string? MissingDependency = null)
{
	public bool Applied => Status == QuestFinishCustomRewardRuntimeInputAssemblerStatus.Created;

	public static QuestFinishCustomRewardRuntimeInputAssemblerResult Disabled()
	{
		return new QuestFinishCustomRewardRuntimeInputAssemblerResult(
			QuestFinishCustomRewardRuntimeInputAssemblerStatus.Disabled,
			QuestXpCustomRewardRuntimeInputAdapterOptions.Disabled,
			"Quest finish custom reward runtime input assembler disabled by C# opt-in gate");
	}

	public static QuestFinishCustomRewardRuntimeInputAssemblerResult MissingDependencyResult(
		string missingDependency)
	{
		return new QuestFinishCustomRewardRuntimeInputAssemblerResult(
			QuestFinishCustomRewardRuntimeInputAssemblerStatus.MissingDependency,
			QuestXpCustomRewardRuntimeInputAdapterOptions.Disabled,
			"Quest finish custom reward runtime input assembler missing C# runtime dependency",
			missingDependency);
	}

	public static QuestFinishCustomRewardRuntimeInputAssemblerResult Created(
		QuestXpCustomRewardRuntimeInputAdapterOptions options)
	{
		return new QuestFinishCustomRewardRuntimeInputAssemblerResult(
			QuestFinishCustomRewardRuntimeInputAssemblerStatus.Created,
			options,
			"QuestService.giveReward -> PlayerCommonData.addExp -> PlayerController.onLevelChange -> FactionPackService.sendRewards");
	}
}

public enum QuestFinishCustomRewardRuntimeInputAssemblerStatus
{
	Disabled,
	MissingDependency,
	Created,
}
