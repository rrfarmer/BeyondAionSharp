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

public sealed record ItemPurificationQuestNotificationDispatchResult(
	ItemPurificationQuestNotificationDispatchStatus Status,
	IReadOnlyList<ItemPurificationQuestNotificationCandidate> Notifications)
{
	public bool Succeeded => Status
		is ItemPurificationQuestNotificationDispatchStatus.NoOp
		or ItemPurificationQuestNotificationDispatchStatus.NoNotifications;

	public static ItemPurificationQuestNotificationDispatchResult NoOp(
		IReadOnlyList<ItemPurificationQuestNotificationCandidate> notifications)
	{
		return new ItemPurificationQuestNotificationDispatchResult(
			ItemPurificationQuestNotificationDispatchStatus.NoOp,
			notifications);
	}

	public static ItemPurificationQuestNotificationDispatchResult NoNotifications()
	{
		return new ItemPurificationQuestNotificationDispatchResult(
			ItemPurificationQuestNotificationDispatchStatus.NoNotifications,
			Array.Empty<ItemPurificationQuestNotificationCandidate>());
	}
}

public enum ItemPurificationQuestNotificationDispatchStatus
{
	NoOp,
	NoNotifications,
}
