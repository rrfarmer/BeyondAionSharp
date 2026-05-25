using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public static class QuestRewardSideEffectPlanService
{
	private const int WarehouseExpansionLimit = 11;

	public static QuestTitleRewardPlan CreateTitleRewardPlan(
		Player? player,
		int titleId,
		TitleTemplateTable titleTemplates)
	{
		// Java parity: model/gameobjects/player/title/TitleList.addTitle(titleId, true, 0).
		var titleTemplate = titleTemplates.GetTitleTemplate(titleId);
		if (titleTemplate == null)
		{
			return QuestTitleRewardPlan.InvalidTitle(titleId);
		}

		if (player == null)
		{
			return QuestTitleRewardPlan.MissingPlayer(titleId, titleTemplate);
		}

		if (!string.Equals(titleTemplate.Race, "PC_ALL", StringComparison.Ordinal)
			&& !string.Equals(titleTemplate.Race, player.Race, StringComparison.Ordinal))
		{
			return QuestTitleRewardPlan.InvalidRace(player.ObjectId, titleId, titleTemplate);
		}

		if (player.Titles.Any(title => title.Id == titleId))
		{
			return QuestTitleRewardPlan.AlreadyKnown(player.ObjectId, titleId, titleTemplate);
		}

		var title = new PlayerTitle(titleId, ExpireTimeSeconds: 0);
		return QuestTitleRewardPlan.CreateApplied(player.ObjectId, title, titleTemplate);
	}

	public static QuestExpansionRewardPlan CreateCubeExpansionPlan(Player? player, int cubeExpansionLimit)
	{
		// Java parity: services/CubeExpandService.questExpand -> expand(player, 3).
		if (player == null)
			return QuestExpansionRewardPlan.MissingPlayer(QuestExpansionRewardKind.Cube);

		var requestedExpansions = player.NpcExpands + player.QuestExpands + player.ItemExpands + 1;
		if (requestedExpansions < 0 || requestedExpansions > cubeExpansionLimit)
		{
			return QuestExpansionRewardPlan.CannotExpand(
				QuestExpansionRewardKind.Cube,
				player.ObjectId,
				requestedExpansions,
				cubeExpansionLimit,
				player.QuestExpands,
				InventoryCapacity.GetCubeLimit(player));
		}

		var nextQuestExpands = player.QuestExpands + 1;
		var nextLimit = InventoryCapacity.GetCubeLimit(new Player
		{
			NpcExpands = player.NpcExpands,
			QuestExpands = nextQuestExpands,
			ItemExpands = player.ItemExpands,
		});

		return QuestExpansionRewardPlan.CreateApplied(
			QuestExpansionRewardKind.Cube,
			player.ObjectId,
			requestedExpansions,
			cubeExpansionLimit,
			player.QuestExpands,
			nextQuestExpands,
			InventoryCapacity.GetCubeLimit(player),
			nextLimit,
			[QuestRewardPacketIntent.InventorySizeExtended, QuestRewardPacketIntent.CubeUpdate],
			"CubeExpandService.questExpand");
	}

	public static QuestExpansionRewardPlan CreateWarehouseExpansionPlan(Player? player)
	{
		// Java parity: services/WarehouseService.expand(player, false).
		if (player == null)
			return QuestExpansionRewardPlan.MissingPlayer(QuestExpansionRewardKind.Warehouse);

		var requestedExpansions = player.WarehouseNpcExpands + player.WarehouseBonusExpands + 1;
		if (requestedExpansions < 0 || requestedExpansions > WarehouseExpansionLimit)
		{
			return QuestExpansionRewardPlan.CannotExpand(
				QuestExpansionRewardKind.Warehouse,
				player.ObjectId,
				requestedExpansions,
				WarehouseExpansionLimit,
				player.WarehouseBonusExpands,
				InventoryCapacity.GetWarehouseLimit(player));
		}

		var nextBonusExpands = player.WarehouseBonusExpands + 1;
		var nextLimit = InventoryCapacity.GetWarehouseLimit(new Player
		{
			WarehouseNpcExpands = player.WarehouseNpcExpands,
			WarehouseBonusExpands = nextBonusExpands,
		});

		return QuestExpansionRewardPlan.CreateApplied(
			QuestExpansionRewardKind.Warehouse,
			player.ObjectId,
			requestedExpansions,
			WarehouseExpansionLimit,
			player.WarehouseBonusExpands,
			nextBonusExpands,
			InventoryCapacity.GetWarehouseLimit(player),
			nextLimit,
			[QuestRewardPacketIntent.WarehouseSizeExtended, QuestRewardPacketIntent.RegularWarehouseInfo],
			"WarehouseService.expand(player, false)");
	}

	public static QuestGpRewardResult CreateGpRewardPlan(
		Player? player,
		int rewardGp,
		IReadOnlyList<float> gpRates)
	{
		// Java parity: services/QuestService.giveReward -> rewards.getGp(),
		// Rates.GP, then GloryPointsService.addGp(playerObjId, gp). This planner is non-live.
		if (player == null)
			return QuestGpRewardResult.MissingPlayer(rewardGp);
		if (rewardGp == 0)
			return QuestGpRewardResult.NoGpReward(player.ObjectId, player.AbyssRank.Gp);

		var appliedRewardGp = QuestRewardService.ApplyQuestGpRate(player.AccountMembership, rewardGp, gpRates);
		var previousGp = player.AbyssRank.Gp;
		var plan = GloryPointsService.CreateAddGpPlan(player, player.ObjectId, appliedRewardGp);
		return QuestGpRewardResult.FromGloryPointsPlan(
			plan,
			player.ObjectId,
			rewardGp,
			appliedRewardGp,
			previousGp);
	}
}

public sealed record QuestTitleRewardPlan(
	QuestTitleRewardStatus Status,
	int ObjectId,
	int TitleId,
	PlayerTitle? Title,
	TitleTemplateSummary? TitleTemplate,
	bool JavaWouldThrow,
	bool RequiresImmediatePersistence,
	bool RequiresExpireRegistration,
	IReadOnlyList<QuestRewardPacketIntent> PacketIntents,
	string? TitleName,
	string? PlainTextMessage,
	string JavaSource)
{
	public bool Applied => Status == QuestTitleRewardStatus.Applied;

	public static QuestTitleRewardPlan CreateApplied(
		int objectId,
		PlayerTitle title,
		TitleTemplateSummary titleTemplate)
	{
		return new QuestTitleRewardPlan(
			QuestTitleRewardStatus.Applied,
			objectId,
			title.Id,
			title,
			titleTemplate,
			JavaWouldThrow: false,
			RequiresImmediatePersistence: true,
			RequiresExpireRegistration: true,
			[QuestRewardPacketIntent.QuestTitleSystemMessage, QuestRewardPacketIntent.FullTitleInfo],
			ChatUtil.L10n(titleTemplate.NameId),
			PlainTextMessage: null,
			"TitleList.addTitle(title, true, 0)");
	}

	public static QuestTitleRewardPlan InvalidTitle(int titleId)
	{
		return new QuestTitleRewardPlan(
			QuestTitleRewardStatus.InvalidTitle,
			0,
			titleId,
			null,
			null,
			JavaWouldThrow: true,
			RequiresImmediatePersistence: false,
			RequiresExpireRegistration: false,
			Array.Empty<QuestRewardPacketIntent>(),
			TitleName: null,
			PlainTextMessage: null,
			"TitleList.addTitle throws IllegalArgumentException for missing title template");
	}

	public static QuestTitleRewardPlan MissingPlayer(int titleId, TitleTemplateSummary titleTemplate)
	{
		return new QuestTitleRewardPlan(
			QuestTitleRewardStatus.MissingPlayer,
			0,
			titleId,
			null,
			titleTemplate,
			JavaWouldThrow: false,
			RequiresImmediatePersistence: false,
			RequiresExpireRegistration: false,
			Array.Empty<QuestRewardPacketIntent>(),
			ChatUtil.L10n(titleTemplate.NameId),
			PlainTextMessage: null,
			"TitleList.addTitle returns false when owner is null");
	}

	public static QuestTitleRewardPlan InvalidRace(int objectId, int titleId, TitleTemplateSummary titleTemplate)
	{
		return new QuestTitleRewardPlan(
			QuestTitleRewardStatus.InvalidRace,
			objectId,
			titleId,
			null,
			titleTemplate,
			JavaWouldThrow: false,
			RequiresImmediatePersistence: false,
			RequiresExpireRegistration: false,
			[QuestRewardPacketIntent.PlainTextRaceFailureMessage],
			ChatUtil.L10n(titleTemplate.NameId),
			"This title is not available for your race.",
			"TitleList.addTitle race guard");
	}

	public static QuestTitleRewardPlan AlreadyKnown(int objectId, int titleId, TitleTemplateSummary titleTemplate)
	{
		return new QuestTitleRewardPlan(
			QuestTitleRewardStatus.AlreadyKnown,
			objectId,
			titleId,
			null,
			titleTemplate,
			JavaWouldThrow: false,
			RequiresImmediatePersistence: false,
			RequiresExpireRegistration: false,
			[QuestRewardPacketIntent.TooltipLearnedTitle],
			ChatUtil.L10n(titleTemplate.NameId),
			PlainTextMessage: null,
			"TitleList.addTitle duplicate-title guard");
	}
}

public enum QuestTitleRewardStatus
{
	Applied,
	MissingPlayer,
	InvalidTitle,
	InvalidRace,
	AlreadyKnown,
}

public sealed record QuestExpansionRewardPlan(
	QuestExpansionRewardStatus Status,
	QuestExpansionRewardKind Kind,
	int ObjectId,
	int RequestedExpansionLevel,
	int ExpansionLimit,
	int PreviousExpansionCount,
	int NewExpansionCount,
	int PreviousSlotLimit,
	int NewSlotLimit,
	bool RequiresPlayerPersistence,
	IReadOnlyList<QuestRewardPacketIntent> PacketIntents,
	string JavaSource)
{
	public bool Applied => Status == QuestExpansionRewardStatus.Applied;

	public static QuestExpansionRewardPlan CreateApplied(
		QuestExpansionRewardKind kind,
		int objectId,
		int requestedExpansionLevel,
		int expansionLimit,
		int previousExpansionCount,
		int newExpansionCount,
		int previousSlotLimit,
		int newSlotLimit,
		IReadOnlyList<QuestRewardPacketIntent> packetIntents,
		string javaSource)
	{
		return new QuestExpansionRewardPlan(
			QuestExpansionRewardStatus.Applied,
			kind,
			objectId,
			requestedExpansionLevel,
			expansionLimit,
			previousExpansionCount,
			newExpansionCount,
			previousSlotLimit,
			newSlotLimit,
			RequiresPlayerPersistence: true,
			packetIntents,
			javaSource);
	}

	public static QuestExpansionRewardPlan CannotExpand(
		QuestExpansionRewardKind kind,
		int objectId,
		int requestedExpansionLevel,
		int expansionLimit,
		int previousExpansionCount,
		int previousSlotLimit)
	{
		return new QuestExpansionRewardPlan(
			QuestExpansionRewardStatus.CannotExpand,
			kind,
			objectId,
			requestedExpansionLevel,
			expansionLimit,
			previousExpansionCount,
			previousExpansionCount,
			previousSlotLimit,
			previousSlotLimit,
			RequiresPlayerPersistence: false,
			[QuestRewardPacketIntent.CannotExpandSystemMessage],
			kind == QuestExpansionRewardKind.Cube
				? "CubeExpandService.canExpand"
				: "WarehouseService.canExpand");
	}

	public static QuestExpansionRewardPlan MissingPlayer(QuestExpansionRewardKind kind)
	{
		return new QuestExpansionRewardPlan(
			QuestExpansionRewardStatus.MissingPlayer,
			kind,
			0,
			0,
			kind == QuestExpansionRewardKind.Cube ? 0 : 11,
			0,
			0,
			0,
			0,
			RequiresPlayerPersistence: false,
			Array.Empty<QuestRewardPacketIntent>(),
			kind == QuestExpansionRewardKind.Cube
				? "CubeExpandService.questExpand"
				: "WarehouseService.expand(player, false)");
	}
}

public enum QuestExpansionRewardStatus
{
	Applied,
	MissingPlayer,
	CannotExpand,
}

public enum QuestExpansionRewardKind
{
	Cube,
	Warehouse,
}

public enum QuestRewardPacketIntent
{
	QuestTitleSystemMessage,
	FullTitleInfo,
	TooltipLearnedTitle,
	PlainTextRaceFailureMessage,
	InventorySizeExtended,
	CubeUpdate,
	WarehouseSizeExtended,
	RegularWarehouseInfo,
	CannotExpandSystemMessage,
}
