using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Services;

public static class QuestFinishCustomRewardSessionRuntimeInputAdapterService
{
	public static QuestFinishCustomRewardRuntimeInputAssemblerResult CreateOptions(
		QuestFinishCustomRewardSessionRuntimeInputAdapterInput input)
	{
		// Java parity breadcrumb: QuestService.giveReward reaches PlayerController.onLevelChange,
		// where FactionPackService reads player.getAccount().getCreationDate(), IDFactory.nextId,
		// and DataManager.ITEM_DATA before sending custom reward system mail.
		Func<int>? nextObjectId = input.IdFactory == null ? null : input.IdFactory.NextId;
		return QuestFinishCustomRewardRuntimeInputAssemblerService.CreateOptions(
			new QuestFinishCustomRewardRuntimeInputAssemblerInput(
				input.EnableCustomRewardExecution,
				input.Options,
				input.Player?.AccountCreationEpochMillis,
				nextObjectId,
				input.ReceivedTime,
				input.ItemTemplates));
	}
}

public sealed record QuestFinishCustomRewardSessionRuntimeInputAdapterInput(
	bool EnableCustomRewardExecution,
	GameServerOptions Options,
	Player? Player = null,
	IDFactory? IdFactory = null,
	DateTime ReceivedTime = default,
	ItemTemplateTable? ItemTemplates = null);
