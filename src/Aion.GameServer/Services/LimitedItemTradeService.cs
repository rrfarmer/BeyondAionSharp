using Aion.GameServer.Dataholders;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class LimitedItemTradeService
{
	private readonly Dictionary<int, List<LimitedItemRuntimeState>> _limitedTradeNpcs;
	private readonly List<ScheduledTask> _scheduledResetTasks = [];
	private readonly object _sync = new();
	private CancellationTokenSource? _resetTokenSource;

	private LimitedItemTradeService(Dictionary<int, List<LimitedItemRuntimeState>> limitedTradeNpcs)
	{
		_limitedTradeNpcs = limitedTradeNpcs;
	}

	public static LimitedItemTradeService Empty { get; } = new(new Dictionary<int, List<LimitedItemRuntimeState>>());

	public static LimitedItemTradeService Create(TradeListTable tradeLists, GoodsListTable goodsLists)
	{
		ArgumentNullException.ThrowIfNull(tradeLists);
		ArgumentNullException.ThrowIfNull(goodsLists);

		// Java parity: services/LimitedItemTradeService.start scans TradeListData trade lists,
		// resolves each tab's GoodsList, then stores GoodsList.getLimitedItems per NPC.
		var limitedTradeNpcs = new Dictionary<int, List<LimitedItemRuntimeState>>();
		foreach (var tradeList in tradeLists.TradeLists)
		{
			foreach (var goodsListId in tradeList.GoodsListIds)
			{
				var goodsList = goodsLists.GetGoodsListById(goodsListId);
				if (goodsList == null)
					continue;

				foreach (var item in goodsList.ItemSummaries)
				{
					if (!item.IsLimitedItem)
						continue;

					if (!limitedTradeNpcs.TryGetValue(tradeList.NpcId, out var limitedItems))
					{
						limitedItems = [];
						limitedTradeNpcs[tradeList.NpcId] = limitedItems;
					}

					limitedItems.Add(
						new LimitedItemRuntimeState(
							item.Id,
							item.SellLimit!.Value,
							item.BuyLimit!.Value,
							goodsList.SalesTime));
				}
			}
		}

		return new LimitedItemTradeService(limitedTradeNpcs);
	}

	public IReadOnlyList<NpcDialogLimitedItemFact> GetLimitedItemFacts(int npcId, int playerObjectId)
	{
		lock (_sync)
		{
			if (!_limitedTradeNpcs.TryGetValue(npcId, out var limitedItems))
				return Array.Empty<NpcDialogLimitedItemFact>();

			return limitedItems
				.Select(item => item.ToFact(playerObjectId))
				.ToArray();
		}
	}

	public bool CanBuy(int npcId, int itemId, int playerObjectId, long count)
	{
		lock (_sync)
		{
			var item = FindLimitedItem(npcId, itemId);
			return item == null || item.CanBuy(playerObjectId, count);
		}
	}

	public LimitedItemBuyMutation? BuyItem(int npcId, int itemId, int playerObjectId, long count)
	{
		lock (_sync)
		{
			var item = FindLimitedItem(npcId, itemId);
			return item?.Buy(playerObjectId, count);
		}
	}

	public LimitedItemResetScheduleResult StartScheduledResets(
		ThreadPoolManager threadPoolManager,
		TimeZoneInfo serverTimeZone,
		Func<DateTimeOffset>? clock = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(threadPoolManager);
		ArgumentNullException.ThrowIfNull(serverTimeZone);

		// Java parity: services/LimitedItemTradeService.start schedules each
		// LimitedItem.setToDefault through CronService with GSConfig.TIME_ZONE_ID.
		ShutdownScheduledResets();
		clock ??= () => DateTimeOffset.Now;
		var skipped = new List<LimitedItemResetScheduleSkip>();
		var scheduled = new List<LimitedItemScheduledReset>();
		lock (_sync)
		{
			foreach (var (npcId, item) in EnumerateLimitedItems())
			{
				if (!JavaQuartzCronExpression.TryParse(item.SalesTime, out var cronExpression))
				{
					skipped.Add(new LimitedItemResetScheduleSkip(npcId, item.ItemId, item.SalesTime, "Unsupported Java Quartz cron expression."));
					continue;
				}

				scheduled.Add(new LimitedItemScheduledReset(npcId, item, cronExpression));
			}

			_resetTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		}

		foreach (var reset in scheduled)
			ScheduleReset(threadPoolManager, serverTimeZone, clock, reset);

		return new LimitedItemResetScheduleResult(scheduled.Count, skipped);
	}

	public void ShutdownScheduledResets()
	{
		CancellationTokenSource? tokenSource;
		lock (_sync)
		{
			tokenSource = _resetTokenSource;
			_resetTokenSource = null;
			foreach (var task in _scheduledResetTasks)
				task.Cancel();
			_scheduledResetTasks.Clear();
		}

		tokenSource?.Cancel();
		tokenSource?.Dispose();
	}

	private LimitedItemRuntimeState? FindLimitedItem(int npcId, int itemId)
	{
		if (!_limitedTradeNpcs.TryGetValue(npcId, out var limitedItems))
			return null;
		return limitedItems.FirstOrDefault(item => item.ItemId == itemId);
	}

	private void ScheduleReset(
		ThreadPoolManager threadPoolManager,
		TimeZoneInfo serverTimeZone,
		Func<DateTimeOffset> clock,
		LimitedItemScheduledReset reset)
	{
		CancellationToken lifetimeToken;
		lock (_sync)
		{
			if (_resetTokenSource == null || _resetTokenSource.IsCancellationRequested)
				return;
			lifetimeToken = _resetTokenSource.Token;
		}

		var now = TimeZoneInfo.ConvertTime(clock(), serverTimeZone);
		var nextRun = reset.CronExpression.GetNextRunAfter(now);
		var delay = nextRun - now;
		if (delay < TimeSpan.Zero)
			delay = TimeSpan.Zero;

		var task = threadPoolManager.Schedule(
			_ =>
			{
				lock (_sync)
					reset.Item.SetToDefault();
				if (!lifetimeToken.IsCancellationRequested)
					ScheduleReset(threadPoolManager, serverTimeZone, clock, reset);
				return ValueTask.CompletedTask;
			},
			delay,
			lifetimeToken);
		TrackScheduledResetTask(task);
	}

	private void TrackScheduledResetTask(ScheduledTask task)
	{
		lock (_sync)
			_scheduledResetTasks.Add(task);
		_ = task.Completion.ContinueWith(
			_ =>
			{
				lock (_sync)
					_scheduledResetTasks.Remove(task);
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private IEnumerable<(int NpcId, LimitedItemRuntimeState Item)> EnumerateLimitedItems()
	{
		foreach (var (npcId, limitedItems) in _limitedTradeNpcs)
		foreach (var item in limitedItems)
			yield return (npcId, item);
	}
}

public sealed record LimitedItemBuyMutation(
	int ItemId,
	int? PlayerBuyCount,
	int? SellLimit);

public sealed record LimitedItemResetScheduleResult(
	int ScheduledCount,
	IReadOnlyList<LimitedItemResetScheduleSkip> SkippedItems);

public sealed record LimitedItemResetScheduleSkip(
	int NpcId,
	int ItemId,
	string? SalesTime,
	string Reason);

internal sealed record LimitedItemScheduledReset(
	int NpcId,
	LimitedItemRuntimeState Item,
	JavaQuartzCronExpression CronExpression);

internal sealed class LimitedItemRuntimeState
{
	private readonly Dictionary<int, int> _buyCounts = new();

	public LimitedItemRuntimeState(int itemId, int sellLimit, int buyLimit, string? salesTime)
	{
		ItemId = itemId;
		SellLimit = sellLimit;
		BuyLimit = buyLimit;
		DefaultSellLimit = sellLimit;
		SalesTime = salesTime;
	}

	public int ItemId { get; }

	public int SellLimit { get; private set; }

	public int BuyLimit { get; }

	public int DefaultSellLimit { get; }

	public string? SalesTime { get; }

	public NpcDialogLimitedItemFact ToFact(int playerObjectId)
	{
		return new NpcDialogLimitedItemFact(
			ItemId,
			SellLimit,
			BuyLimit,
			_buyCounts.GetValueOrDefault(playerObjectId),
			SalesTime);
	}

	public bool CanBuy(int playerObjectId, long count)
	{
		// Java parity: TradeService.canBuyLimitItem checks current sellLimit and
		// LimitedItem.getBuyCount(playerObjectId) against the requested tradeItem count.
		if (SellLimit > 0 && SellLimit - count < 0)
			return false;
		if (BuyLimit > 0 && _buyCounts.GetValueOrDefault(playerObjectId) + count > BuyLimit)
			return false;
		return true;
	}

	public LimitedItemBuyMutation Buy(int playerObjectId, long count)
	{
		// Java parity: TradeService.performBuyTransaction final loop calls
		// LimitedItem.setBuyCount and setSellLimit after ItemService.addItem succeeds.
		var playerBuyCount = _buyCounts.GetValueOrDefault(playerObjectId);
		int? updatedBuyCount = null;
		if (BuyLimit > 0)
		{
			updatedBuyCount = checked(playerBuyCount + (int)count);
			_buyCounts[playerObjectId] = updatedBuyCount.Value;
		}

		int? updatedSellLimit = null;
		if (DefaultSellLimit > 0)
		{
			updatedSellLimit = checked(SellLimit - (int)count);
			SellLimit = updatedSellLimit.Value;
		}

		return new LimitedItemBuyMutation(ItemId, updatedBuyCount, updatedSellLimit);
	}

	public void SetToDefault()
	{
		// Java parity: model/limiteditems/LimitedItem.setToDefault.
		SellLimit = DefaultSellLimit;
		_buyCounts.Clear();
	}
}
