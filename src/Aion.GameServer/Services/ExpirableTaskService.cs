using System.Collections.Concurrent;
using System.Globalization;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class ExpirableTaskService
{
	private static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(500);
	private static readonly TimeSpan Period = TimeSpan.FromSeconds(1);
	private static readonly HashSet<int> ItemBeforeExpireWarningSeconds = [1800, 900, 600, 300, 60];
	private const int CubeStorageId = 0;
	private const int RegularWarehouseStorageId = 1;
	private const int AccountWarehouseStorageId = 2;

	private readonly ConcurrentDictionary<int, PlayerExpirableRegistration> _registrations = new();
	private readonly IPlayerEnterWorldRepository _repository;
	private readonly ILogger<ExpirableTaskService> _logger;
	private readonly Func<DateTimeOffset> _clock;

	public ExpirableTaskService(
		ThreadPoolManager threadPoolManager,
		IPlayerEnterWorldRepository repository,
		ILogger<ExpirableTaskService> logger)
		: this(threadPoolManager, repository, logger, () => DateTimeOffset.UtcNow)
	{
	}

	public ExpirableTaskService(
		ThreadPoolManager threadPoolManager,
		IPlayerEnterWorldRepository repository,
		ILogger<ExpirableTaskService> logger,
		Func<DateTimeOffset> clock)
	{
		// Java parity: taskmanager/tasks/ExpireTimerTask extends AbstractPeriodicTaskManager(1000).
		_repository = repository;
		_logger = logger;
		_clock = clock;
		_ = threadPoolManager.ScheduleAtFixedRate(
			_ => new ValueTask(TickAsync()),
			InitialDelay,
			Period);
	}

	public void RegisterPlayerExpirables(
		Player player,
		Func<AionServerPacket, Task> sendPacketAsync,
		Func<AionServerPacket, Task>? broadcastVisibleAsync = null,
		TitleTemplateTable? titleTemplates = null,
		ItemTemplateTable? itemTemplates = null,
		HousingObjectTemplateTable? housingObjectTemplates = null,
		Func<PlayerHouse, RegisteredHouseObjectSummary, HousingObjectTemplateSummary?, Task>? expireHouseObjectAsync = null,
		Func<RegisteredHouseObjectSummary, HousingObjectTemplateSummary?, bool>? canHouseObjectExpireNow = null)
	{
		// Java parity: services/player/PlayerEnterWorldService registers loaded storage items, motions, emotions, and titles.
		UnregisterPlayer(player);
		var registration = new PlayerExpirableRegistration(
			player,
			sendPacketAsync,
			broadcastVisibleAsync,
			titleTemplates,
			itemTemplates,
			housingObjectTemplates,
			expireHouseObjectAsync,
			canHouseObjectExpireNow);
		_registrations[player.ObjectId] = registration;
		foreach (var item in player.InventoryItems)
			AddExpirable(registration, ExpirableKind.Item, item.ObjectId, item.ExpireTime);
		foreach (var item in player.WarehouseItems)
			AddExpirable(registration, ExpirableKind.Item, item.ObjectId, item.ExpireTime);
		foreach (var item in player.AccountWarehouseItems)
			AddExpirable(registration, ExpirableKind.Item, item.ObjectId, item.ExpireTime);
		foreach (var emotion in player.Emotions)
			AddExpirable(registration, ExpirableKind.Emotion, emotion.Id, emotion.ExpireTimeSeconds);
		foreach (var title in player.Titles)
			AddExpirable(registration, ExpirableKind.Title, title.Id, title.ExpireTimeSeconds);
		foreach (var motion in player.Motions)
			AddExpirable(registration, ExpirableKind.Motion, motion.Id, motion.ExpireTimeSeconds);
		if (player.Houses.FirstOrDefault(house => !house.IsInactive) is { } activeHouse)
			RegisterHouseObjects(player, activeHouse);
	}

	public void RegisterEmotion(Player player, PlayerEmotion emotion)
	{
		// Java parity: model/gameobjects/player/emotion/EmotionList.add(..., isNew=true).
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Emotion, emotion.Id, emotion.ExpireTimeSeconds);
	}

	public void RegisterTitle(Player player, PlayerTitle title)
	{
		// Java parity: model/gameobjects/player/title/TitleList.addTitle registers temporary titles.
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Title, title.Id, title.ExpireTimeSeconds);
	}

	public void RegisterMotion(Player player, PlayerMotion motion)
	{
		// Java parity: model/gameobjects/player/motion/MotionList.add(..., persist=true).
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Motion, motion.Id, motion.ExpireTimeSeconds);
	}

	public void RegisterInventoryItem(Player player, InventoryItem item)
	{
		// Java parity: services/item/ItemService.addNonStackableItem registers newly created expirable items.
		if (_registrations.TryGetValue(player.ObjectId, out var registration))
			AddExpirable(registration, ExpirableKind.Item, item.ObjectId, item.ExpireTime);
	}

	public void RegisterHouseObjects(Player player, PlayerHouse activeHouse)
	{
		// Java parity: PlayerEnterWorldService registers active-house HouseObject expirables after the registry is available.
		if (!_registrations.TryGetValue(player.ObjectId, out var registration)
			|| activeHouse.IsInactive
			|| activeHouse.Registry == null)
		{
			return;
		}

		foreach (var houseObject in activeHouse.Registry.Objects)
			AddExpirable(registration, ExpirableKind.HouseObject, houseObject.ObjectId, houseObject.ExpireTimeSeconds);
	}

	public void UnregisterPlayer(Player player)
	{
		// Java parity: services/player/PlayerLeaveWorldService -> ExpireTimerTask.unregisterExpirables.
		_registrations.TryRemove(player.ObjectId, out _);
	}

	public async Task TickAsync(CancellationToken cancellationToken = default)
	{
		var now = _clock();
		foreach (var registration in _registrations.Values.ToArray())
		{
			foreach (var expirable in registration.Expirables.Keys.ToArray())
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				var remainingSeconds = expirable.ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
				if (remainingSeconds >= 0)
				{
					if (ItemBeforeExpireWarningSeconds.Contains(remainingSeconds))
						await BeforeExpireAsync(registration, expirable, remainingSeconds / 60, cancellationToken);
					continue;
				}

				if (expirable.Kind == ExpirableKind.HouseObject && !CanExpireHouseObjectNow(registration, expirable.Id, now))
					continue;

				if (!_registrations.TryGetValue(registration.Player.ObjectId, out var currentRegistration)
					|| !ReferenceEquals(currentRegistration, registration)
					|| !registration.Expirables.TryRemove(expirable, out _))
				{
					continue;
				}

				await ExpireAsync(registration, expirable, cancellationToken);
			}
		}
	}

	private static void AddExpirable(
		PlayerExpirableRegistration registration,
		ExpirableKind kind,
		int id,
		int expireTimeSeconds)
	{
		if (expireTimeSeconds <= 0)
			return;

		registration.Expirables.TryAdd(new ExpirableKey(kind, id, expireTimeSeconds), 0);
	}

	private async Task ExpireAsync(
		PlayerExpirableRegistration registration,
		ExpirableKey expirable,
		CancellationToken cancellationToken)
	{
		try
		{
			switch (expirable.Kind)
			{
				case ExpirableKind.Emotion:
					await ExpireEmotionAsync(registration, expirable.Id, cancellationToken);
					break;
				case ExpirableKind.Title:
					await ExpireTitleAsync(registration, expirable.Id, cancellationToken);
					break;
				case ExpirableKind.Motion:
					await ExpireMotionAsync(registration, expirable.Id, cancellationToken);
					break;
				case ExpirableKind.Item:
					await ExpireItemAsync(registration, expirable.Id, cancellationToken);
					break;
				case ExpirableKind.HouseObject:
					await ExpireHouseObjectAsync(registration, expirable.Id);
					break;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not expire {ExpirableKind} {ExpirableId} for player {PlayerObjectId}", expirable.Kind, expirable.Id, registration.Player.ObjectId);
		}
	}

	private async Task BeforeExpireAsync(
		PlayerExpirableRegistration registration,
		ExpirableKey expirable,
		int remainingMinutes,
		CancellationToken cancellationToken)
	{
		try
		{
			if (expirable.Kind != ExpirableKind.Item)
				return;

			if (FindInventoryItem(registration.Player, expirable.Id) is not { } item)
				return;

			await registration.SendPacketAsync(SmSystemMessage.CashItemTimeLeft(GetItemName(registration, item), remainingMinutes));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not send before-expire notice for {ExpirableKind} {ExpirableId} for player {PlayerObjectId}", expirable.Kind, expirable.Id, registration.Player.ObjectId);
		}
	}

	private async Task ExpireEmotionAsync(
		PlayerExpirableRegistration registration,
		int emotionId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/player/emotion/Emotion.onExpire -> EmotionList.remove.
		var player = registration.Player;
		var removed = false;
		lock (registration.SyncRoot)
		{
			if (player.Emotions.Any(emotion => emotion.Id == emotionId))
			{
				player.Emotions = player.Emotions.Where(emotion => emotion.Id != emotionId).ToArray();
				removed = true;
			}
		}

		if (!removed)
			return;

		await _repository.DeletePlayerEmotionAsync(player.ObjectId, emotionId, cancellationToken);
		await registration.SendPacketAsync(new SmEmotionList(0, player.Emotions));
		await registration.SendPacketAsync(SmSystemMessage.DeleteCashSocialActionByTimeout());
	}

	private async Task ExpireTitleAsync(
		PlayerExpirableRegistration registration,
		int titleId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/player/title/Title.onExpire -> TitleList.removeTitle.
		var player = registration.Player;
		var removed = false;
		var removedDisplayTitle = false;
		var removedBonusTitle = false;
		lock (registration.SyncRoot)
		{
			if (player.Titles.Any(title => title.Id == titleId))
			{
				player.Titles = player.Titles.Where(title => title.Id != titleId).ToArray();
				removed = true;
				if (player.TitleId == titleId)
				{
					player.TitleId = -1;
					removedDisplayTitle = true;
				}

				if (player.BonusTitleId == titleId)
				{
					player.BonusTitleId = -1;
					removedBonusTitle = true;
				}
			}
		}

		if (!removed)
			return;

		await _repository.DeletePlayerTitleAsync(player.ObjectId, titleId, cancellationToken);
		if (removedDisplayTitle)
		{
			await registration.SendPacketAsync(new SmTitleInfo(-1));
			if (registration.BroadcastVisibleAsync != null)
				await registration.BroadcastVisibleAsync(new SmTitleInfo(player, -1));
		}

		if (removedBonusTitle)
			await registration.SendPacketAsync(new SmTitleInfo(6, -1));

		await registration.SendPacketAsync(new SmTitleInfo(player.Titles));
		await registration.SendPacketAsync(SmSystemMessage.DeleteCashTitleByTimeout(GetTitleName(registration, titleId)));
	}

	private async Task ExpireMotionAsync(
		PlayerExpirableRegistration registration,
		int motionId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/player/motion/Motion.onExpire -> MotionList.remove.
		var player = registration.Player;
		var removed = false;
		lock (registration.SyncRoot)
		{
			if (player.Motions.Any(motion => motion.Id == motionId))
			{
				player.Motions = player.Motions.Where(motion => motion.Id != motionId).ToArray();
				removed = true;
			}
		}

		if (!removed)
			return;

		await _repository.DeletePlayerMotionAsync(player.ObjectId, motionId, cancellationToken);
		await registration.SendPacketAsync(SmMotion.Remove(motionId));
		await registration.SendPacketAsync(SmSystemMessage.DeleteCashCustomAnimationByTimeout());
	}

	private async Task ExpireItemAsync(
		PlayerExpirableRegistration registration,
		int itemObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: model/gameobjects/Item.onExpire.
		var player = registration.Player;
		InventoryItem? expiredItem = null;
		lock (registration.SyncRoot)
		{
			expiredItem = FindInventoryItem(player, itemObjectId);
			if (expiredItem == null)
				return;

			player.InventoryItems = player.InventoryItems.Where(item => item.ObjectId != itemObjectId).ToArray();
			player.WarehouseItems = player.WarehouseItems.Where(item => item.ObjectId != itemObjectId).ToArray();
			player.AccountWarehouseItems = player.AccountWarehouseItems.Where(item => item.ObjectId != itemObjectId).ToArray();
		}

		await _repository.DeleteInventoryItemAsync(expiredItem.OwnerId, expiredItem.ObjectId, cancellationToken);
		await SendExpiredItemPacketsAsync(registration, expiredItem);
	}

	private static bool CanExpireHouseObjectNow(PlayerExpirableRegistration registration, int houseObjectId, DateTimeOffset now)
	{
		if (!TryFindHouseObject(registration.Player, houseObjectId, out _, out var houseObject))
			return true;

		var template = registration.HousingObjectTemplates?.GetTemplate(houseObject.TemplateId);
		if (template == null)
			return true;
		// Java parity: EmblemObject.canExpireNow always returns false.
		if (template.TypeId == 11)
			return false;
		// Java parity: UseableItemObject.canExpireNow keeps expired final-reward objects for owner recovery.
		if (template.TypeId == 1 && template.UseActionFinalRewardId > 0 && houseObject.ExpireTimeSeconds <= now.ToUnixTimeSeconds())
			return false;
		if (registration.CanHouseObjectExpireNow != null && !registration.CanHouseObjectExpireNow(houseObject, template))
			return false;
		return true;
	}

	private static async Task ExpireHouseObjectAsync(PlayerExpirableRegistration registration, int houseObjectId)
	{
		if (registration.ExpireHouseObjectAsync == null)
			return;
		if (!TryFindHouseObject(registration.Player, houseObjectId, out var house, out var houseObject) || house == null)
			return;

		var template = registration.HousingObjectTemplates?.GetTemplate(houseObject.TemplateId);
		await registration.ExpireHouseObjectAsync(house, houseObject, template);
	}

	private static async Task SendExpiredItemPacketsAsync(PlayerExpirableRegistration registration, InventoryItem expiredItem)
	{
		var itemName = GetItemName(registration, expiredItem);
		switch (expiredItem.Location)
		{
			case CubeStorageId:
				await registration.SendPacketAsync(new SmDeleteItem(expiredItem.ObjectId));
				await registration.SendPacketAsync(SmCubeUpdate.CubeSize(registration.Player));
				await registration.SendPacketAsync(SmSystemMessage.DeleteCashItemByTimeout(itemName));
				break;
			case RegularWarehouseStorageId:
				await registration.SendPacketAsync(new SmDeleteWarehouseItem(RegularWarehouseStorageId, expiredItem.ObjectId));
				await registration.SendPacketAsync(SmCubeUpdate.RegularWarehouseSize(registration.Player));
				await registration.SendPacketAsync(SmSystemMessage.DeleteCashItemByTimeoutInWarehouse(itemName));
				break;
			case AccountWarehouseStorageId:
				await registration.SendPacketAsync(new SmDeleteWarehouseItem(AccountWarehouseStorageId, expiredItem.ObjectId));
				await registration.SendPacketAsync(SmCubeUpdate.AccountWarehouseSize());
				await registration.SendPacketAsync(SmSystemMessage.DeleteCashItemByTimeoutInWarehouse(itemName));
				break;
		}
	}

	private static InventoryItem? FindInventoryItem(Player player, int itemObjectId)
	{
		return player.InventoryItems.FirstOrDefault(item => item.ObjectId == itemObjectId)
			?? player.WarehouseItems.FirstOrDefault(item => item.ObjectId == itemObjectId)
			?? player.AccountWarehouseItems.FirstOrDefault(item => item.ObjectId == itemObjectId);
	}

	private static string GetItemName(PlayerExpirableRegistration registration, InventoryItem item)
	{
		var itemTemplate = registration.ItemTemplates?.GetItemTemplate(item.ItemId);
		return itemTemplate?.GetClientName() ?? item.ItemId.ToString(CultureInfo.InvariantCulture);
	}

	private static string GetTitleName(PlayerExpirableRegistration registration, int titleId)
	{
		var titleTemplate = registration.TitleTemplates?.GetTitleTemplate(titleId);
		return titleTemplate == null
			? titleId.ToString(CultureInfo.InvariantCulture)
			: ChatUtil.L10n(titleTemplate.NameId);
	}

	private static bool TryFindHouseObject(
		Player player,
		int houseObjectId,
		out PlayerHouse? house,
		out RegisteredHouseObjectSummary houseObject)
	{
		foreach (var candidateHouse in player.Houses)
		{
			var candidateObject = candidateHouse.Registry?.GetObject(houseObjectId);
			if (candidateObject == null)
				continue;

			house = candidateHouse;
			houseObject = candidateObject;
			return true;
		}

		house = null;
		houseObject = default!;
		return false;
	}

	private sealed class PlayerExpirableRegistration
	{
		public PlayerExpirableRegistration(
			Player player,
			Func<AionServerPacket, Task> sendPacketAsync,
			Func<AionServerPacket, Task>? broadcastVisibleAsync,
			TitleTemplateTable? titleTemplates,
			ItemTemplateTable? itemTemplates,
			HousingObjectTemplateTable? housingObjectTemplates,
			Func<PlayerHouse, RegisteredHouseObjectSummary, HousingObjectTemplateSummary?, Task>? expireHouseObjectAsync,
			Func<RegisteredHouseObjectSummary, HousingObjectTemplateSummary?, bool>? canHouseObjectExpireNow)
		{
			Player = player;
			SendPacketAsync = sendPacketAsync;
			BroadcastVisibleAsync = broadcastVisibleAsync;
			TitleTemplates = titleTemplates;
			ItemTemplates = itemTemplates;
			HousingObjectTemplates = housingObjectTemplates;
			ExpireHouseObjectAsync = expireHouseObjectAsync;
			CanHouseObjectExpireNow = canHouseObjectExpireNow;
		}

		public Player Player { get; }

		public Func<AionServerPacket, Task> SendPacketAsync { get; }

		public Func<AionServerPacket, Task>? BroadcastVisibleAsync { get; }

		public TitleTemplateTable? TitleTemplates { get; }

		public ItemTemplateTable? ItemTemplates { get; }

		public HousingObjectTemplateTable? HousingObjectTemplates { get; }

		public Func<PlayerHouse, RegisteredHouseObjectSummary, HousingObjectTemplateSummary?, Task>? ExpireHouseObjectAsync { get; }

		public Func<RegisteredHouseObjectSummary, HousingObjectTemplateSummary?, bool>? CanHouseObjectExpireNow { get; }

		public object SyncRoot { get; } = new();

		public ConcurrentDictionary<ExpirableKey, byte> Expirables { get; } = new();
	}

	private readonly record struct ExpirableKey(ExpirableKind Kind, int Id, int ExpireTimeSeconds);

	private enum ExpirableKind
	{
		Emotion,
		Title,
		Motion,
		Item,
		HouseObject,
	}
}
