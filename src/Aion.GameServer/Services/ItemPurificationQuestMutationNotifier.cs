using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public interface IItemPurificationQuestMutationNotifier
{
	ValueTask<ItemPurificationQuestNotificationDispatchResult> NotifyAsync(
		Player player,
		IReadOnlyList<ItemPurificationQuestNotificationCandidate> notifications,
		CancellationToken cancellationToken = default);
}

public sealed class NoOpItemPurificationQuestMutationNotifier : IItemPurificationQuestMutationNotifier
{
	public ValueTask<ItemPurificationQuestNotificationDispatchResult> NotifyAsync(
		Player player,
		IReadOnlyList<ItemPurificationQuestNotificationCandidate> notifications,
		CancellationToken cancellationToken = default)
	{
		// Java parity breadcrumb: Storage.delete and Storage.add call QuestEngine item hooks
		// during storage mutation. This no-op seam records the ordered intent only; it does
		// not invoke quest handlers or nearby-quest refresh.
		return ValueTask.FromResult(
			notifications.Count == 0
				? ItemPurificationQuestNotificationDispatchResult.NoNotifications()
				: ItemPurificationQuestNotificationDispatchResult.NoOp(notifications));
	}
}

public sealed class PlanningItemPurificationQuestMutationNotifier : IItemPurificationQuestMutationNotifier
{
	private readonly QuestUpdateItemTable _questUpdateItems;
	private readonly IItemPurificationNearbyQuestRefreshDispatcher? _nearbyQuestRefreshDispatcher;

	public PlanningItemPurificationQuestMutationNotifier(
		QuestUpdateItemTable questUpdateItems,
		IItemPurificationNearbyQuestRefreshDispatcher? nearbyQuestRefreshDispatcher = null)
	{
		_questUpdateItems = questUpdateItems;
		_nearbyQuestRefreshDispatcher = nearbyQuestRefreshDispatcher;
	}

	public async ValueTask<ItemPurificationQuestNotificationDispatchResult> NotifyAsync(
		Player player,
		IReadOnlyList<ItemPurificationQuestNotificationCandidate> notifications,
		CancellationToken cancellationToken = default)
	{
		// Java parity breadcrumb: QuestEngine.onItemGet and onItemRemoved call
		// updateNearbyQuests only when questUpdateItems contains the item id. This
		// planner exposes that decision but still does not invoke quest handlers or
		// the player controller.
		var refreshPlan = ItemPurificationNearbyQuestRefreshPlan.Create(notifications, _questUpdateItems);
		var refreshDispatch = _nearbyQuestRefreshDispatcher == null
			? null
			: await _nearbyQuestRefreshDispatcher.DispatchAsync(player, refreshPlan, cancellationToken);
		return notifications.Count == 0
			? ItemPurificationQuestNotificationDispatchResult.NoNotifications(refreshPlan, refreshDispatch)
			: ItemPurificationQuestNotificationDispatchResult.NoOp(notifications, refreshPlan, refreshDispatch);
	}
}

public interface IItemPurificationNearbyQuestRefreshDispatcher
{
	ValueTask<ItemPurificationNearbyQuestRefreshDispatchResult> DispatchAsync(
		Player player,
		ItemPurificationNearbyQuestRefreshPlan refreshPlan,
		CancellationToken cancellationToken = default);
}

public sealed class NoOpItemPurificationNearbyQuestRefreshDispatcher : IItemPurificationNearbyQuestRefreshDispatcher
{
	public ValueTask<ItemPurificationNearbyQuestRefreshDispatchResult> DispatchAsync(
		Player player,
		ItemPurificationNearbyQuestRefreshPlan refreshPlan,
		CancellationToken cancellationToken = default)
	{
		// Java parity breadcrumb: QuestEngine calls player.getController().updateNearbyQuests()
		// for matching questUpdateItems. This dispatcher keeps the explicit seam no-op until a
		// real player-controller refresh path exists in C#.
		return ValueTask.FromResult(
			refreshPlan.ShouldRefreshNearbyQuests
				? ItemPurificationNearbyQuestRefreshDispatchResult.NoOp(refreshPlan)
				: ItemPurificationNearbyQuestRefreshDispatchResult.NoRefreshNeeded(refreshPlan));
	}
}

public sealed record ItemPurificationQuestNotificationDispatchResult(
	ItemPurificationQuestNotificationDispatchStatus Status,
	IReadOnlyList<ItemPurificationQuestNotificationCandidate> Notifications,
	ItemPurificationNearbyQuestRefreshPlan? NearbyQuestRefreshPlan = null,
	ItemPurificationNearbyQuestRefreshDispatchResult? NearbyQuestRefreshDispatch = null)
{
	public bool Succeeded => Status
		is ItemPurificationQuestNotificationDispatchStatus.NoOp
		or ItemPurificationQuestNotificationDispatchStatus.NoNotifications;

	public static ItemPurificationQuestNotificationDispatchResult NoOp(
		IReadOnlyList<ItemPurificationQuestNotificationCandidate> notifications,
		ItemPurificationNearbyQuestRefreshPlan? nearbyQuestRefreshPlan = null,
		ItemPurificationNearbyQuestRefreshDispatchResult? nearbyQuestRefreshDispatch = null)
	{
		return new ItemPurificationQuestNotificationDispatchResult(
			ItemPurificationQuestNotificationDispatchStatus.NoOp,
			notifications,
			nearbyQuestRefreshPlan,
			nearbyQuestRefreshDispatch);
	}

	public static ItemPurificationQuestNotificationDispatchResult NoNotifications(
		ItemPurificationNearbyQuestRefreshPlan? nearbyQuestRefreshPlan = null,
		ItemPurificationNearbyQuestRefreshDispatchResult? nearbyQuestRefreshDispatch = null)
	{
		return new ItemPurificationQuestNotificationDispatchResult(
			ItemPurificationQuestNotificationDispatchStatus.NoNotifications,
			Array.Empty<ItemPurificationQuestNotificationCandidate>(),
			nearbyQuestRefreshPlan,
			nearbyQuestRefreshDispatch);
	}
}

public sealed record ItemPurificationNearbyQuestRefreshPlan(
	ItemPurificationNearbyQuestRefreshPlanStatus Status,
	IReadOnlyList<ItemPurificationNearbyQuestRefreshCandidate> Candidates)
{
	public bool ShouldRefreshNearbyQuests => Status == ItemPurificationNearbyQuestRefreshPlanStatus.Ready;

	public static ItemPurificationNearbyQuestRefreshPlan Create(
		IReadOnlyList<ItemPurificationQuestNotificationCandidate> notifications,
		QuestUpdateItemTable questUpdateItems)
	{
		if (notifications.Count == 0)
		{
			return new ItemPurificationNearbyQuestRefreshPlan(
				ItemPurificationNearbyQuestRefreshPlanStatus.NoNotifications,
				Array.Empty<ItemPurificationNearbyQuestRefreshCandidate>());
		}

		if (questUpdateItems.Count == 0)
		{
			return new ItemPurificationNearbyQuestRefreshPlan(
				ItemPurificationNearbyQuestRefreshPlanStatus.NoQuestUpdateItems,
				Array.Empty<ItemPurificationNearbyQuestRefreshCandidate>());
		}

		var candidates = notifications
			.Where(notification => questUpdateItems.ContainsItemId(notification.ItemId))
			.Select(
				notification => new ItemPurificationNearbyQuestRefreshCandidate(
					notification.Type,
					notification.SourceOperation,
					notification.ObjectId,
					notification.ItemId))
			.ToArray();
		return new ItemPurificationNearbyQuestRefreshPlan(
			candidates.Length == 0
				? ItemPurificationNearbyQuestRefreshPlanStatus.NoRefreshCandidates
				: ItemPurificationNearbyQuestRefreshPlanStatus.Ready,
			candidates);
	}
}

public sealed record ItemPurificationNearbyQuestRefreshCandidate(
	ItemPurificationQuestNotificationType Type,
	ItemPurificationApplicationOperationType SourceOperation,
	int ObjectId,
	int ItemId);

public sealed record ItemPurificationNearbyQuestRefreshDispatchResult(
	ItemPurificationNearbyQuestRefreshDispatchStatus Status,
	IReadOnlyList<ItemPurificationNearbyQuestRefreshCandidate> Candidates)
{
	public bool Succeeded => Status
		is ItemPurificationNearbyQuestRefreshDispatchStatus.NoOp
		or ItemPurificationNearbyQuestRefreshDispatchStatus.NoRefreshNeeded;

	public static ItemPurificationNearbyQuestRefreshDispatchResult NoOp(
		ItemPurificationNearbyQuestRefreshPlan refreshPlan)
	{
		return new ItemPurificationNearbyQuestRefreshDispatchResult(
			ItemPurificationNearbyQuestRefreshDispatchStatus.NoOp,
			refreshPlan.Candidates);
	}

	public static ItemPurificationNearbyQuestRefreshDispatchResult NoRefreshNeeded(
		ItemPurificationNearbyQuestRefreshPlan refreshPlan)
	{
		return new ItemPurificationNearbyQuestRefreshDispatchResult(
			ItemPurificationNearbyQuestRefreshDispatchStatus.NoRefreshNeeded,
			refreshPlan.Candidates);
	}
}

public enum ItemPurificationNearbyQuestRefreshPlanStatus
{
	Ready,
	NoNotifications,
	NoQuestUpdateItems,
	NoRefreshCandidates,
}

public enum ItemPurificationNearbyQuestRefreshDispatchStatus
{
	NoOp,
	NoRefreshNeeded,
}

public enum ItemPurificationQuestNotificationDispatchStatus
{
	NoOp,
	NoNotifications,
}
