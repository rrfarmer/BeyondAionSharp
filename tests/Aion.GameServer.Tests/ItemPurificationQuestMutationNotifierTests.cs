using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationQuestMutationNotifierTests
{
	[Fact]
	public async Task PlanningNotifier_FiltersNearbyRefreshCandidatesThroughQuestUpdateItems()
	{
		var notifier = new PlanningItemPurificationQuestMutationNotifier(
			new QuestUpdateItemTable([186000002, 100000002]));
		var notifications = new[]
		{
			CreateNotification(ItemPurificationQuestNotificationType.ItemRemoved, ItemPurificationApplicationOperationType.DeleteMaterialItem, 20, 186000001),
			CreateNotification(ItemPurificationQuestNotificationType.ItemRemoved, ItemPurificationApplicationOperationType.DeleteBaseItem, 10, 100000001),
			CreateNotification(ItemPurificationQuestNotificationType.ItemGet, ItemPurificationApplicationOperationType.AddTargetItem, 9001, 100000002),
			CreateNotification(ItemPurificationQuestNotificationType.ItemRemoved, ItemPurificationApplicationOperationType.DeleteMaterialItem, 21, 186000002),
		};

		var result = await notifier.NotifyAsync(new Player { ObjectId = 1 }, notifications);

		Assert.Equal(ItemPurificationQuestNotificationDispatchStatus.NoOp, result.Status);
		Assert.Same(notifications, result.Notifications);
		var refreshPlan = Assert.IsType<ItemPurificationNearbyQuestRefreshPlan>(result.NearbyQuestRefreshPlan);
		Assert.True(refreshPlan.ShouldRefreshNearbyQuests);
		Assert.Equal(ItemPurificationNearbyQuestRefreshPlanStatus.Ready, refreshPlan.Status);
		Assert.Equal(
			[
				ItemPurificationQuestNotificationType.ItemGet,
				ItemPurificationQuestNotificationType.ItemRemoved,
			],
			refreshPlan.Candidates.Select(candidate => candidate.Type).ToArray());
		Assert.Equal([100000002, 186000002], refreshPlan.Candidates.Select(candidate => candidate.ItemId).ToArray());
		Assert.Equal([9001, 21], refreshPlan.Candidates.Select(candidate => candidate.ObjectId).ToArray());
		Assert.Null(result.NearbyQuestRefreshDispatch);
	}

	[Fact]
	public async Task PlanningNotifier_UsesOptInNoOpDispatcherForRefreshCandidates()
	{
		var notifier = new PlanningItemPurificationQuestMutationNotifier(
			new QuestUpdateItemTable([100000002]),
			new NoOpItemPurificationNearbyQuestRefreshDispatcher());
		var notifications = new[]
		{
			CreateNotification(ItemPurificationQuestNotificationType.ItemGet, ItemPurificationApplicationOperationType.AddTargetItem, 9001, 100000002),
		};

		var result = await notifier.NotifyAsync(new Player { ObjectId = 1 }, notifications);

		var refreshDispatch = Assert.IsType<ItemPurificationNearbyQuestRefreshDispatchResult>(result.NearbyQuestRefreshDispatch);
		Assert.True(refreshDispatch.Succeeded);
		Assert.Equal(ItemPurificationNearbyQuestRefreshDispatchStatus.NoOp, refreshDispatch.Status);
		var candidate = Assert.Single(refreshDispatch.Candidates);
		Assert.Equal(100000002, candidate.ItemId);
	}

	[Fact]
	public async Task PlanningNotifier_ReportsNoRefreshCandidatesWhenItemsAreNotQuestUpdateItems()
	{
		var notifier = new PlanningItemPurificationQuestMutationNotifier(
			new QuestUpdateItemTable([182200001]),
			new NoOpItemPurificationNearbyQuestRefreshDispatcher());
		var notifications = new[]
		{
			CreateNotification(ItemPurificationQuestNotificationType.ItemRemoved, ItemPurificationApplicationOperationType.DeleteMaterialItem, 20, 186000001),
			CreateNotification(ItemPurificationQuestNotificationType.ItemGet, ItemPurificationApplicationOperationType.AddTargetItem, 9001, 100000002),
		};

		var result = await notifier.NotifyAsync(new Player { ObjectId = 1 }, notifications);

		var refreshPlan = Assert.IsType<ItemPurificationNearbyQuestRefreshPlan>(result.NearbyQuestRefreshPlan);
		Assert.False(refreshPlan.ShouldRefreshNearbyQuests);
		Assert.Equal(ItemPurificationNearbyQuestRefreshPlanStatus.NoRefreshCandidates, refreshPlan.Status);
		Assert.Empty(refreshPlan.Candidates);
		var refreshDispatch = Assert.IsType<ItemPurificationNearbyQuestRefreshDispatchResult>(result.NearbyQuestRefreshDispatch);
		Assert.True(refreshDispatch.Succeeded);
		Assert.Equal(ItemPurificationNearbyQuestRefreshDispatchStatus.NoRefreshNeeded, refreshDispatch.Status);
		Assert.Empty(refreshDispatch.Candidates);
	}

	[Fact]
	public async Task PlanningNotifier_ReportsNoNotificationsWithoutRefreshing()
	{
		var notifier = new PlanningItemPurificationQuestMutationNotifier(new QuestUpdateItemTable([182200001]));

		var result = await notifier.NotifyAsync(
			new Player { ObjectId = 1 },
			Array.Empty<ItemPurificationQuestNotificationCandidate>());

		Assert.Equal(ItemPurificationQuestNotificationDispatchStatus.NoNotifications, result.Status);
		var refreshPlan = Assert.IsType<ItemPurificationNearbyQuestRefreshPlan>(result.NearbyQuestRefreshPlan);
		Assert.False(refreshPlan.ShouldRefreshNearbyQuests);
		Assert.Equal(ItemPurificationNearbyQuestRefreshPlanStatus.NoNotifications, refreshPlan.Status);
		Assert.Empty(refreshPlan.Candidates);
	}

	private static ItemPurificationQuestNotificationCandidate CreateNotification(
		ItemPurificationQuestNotificationType type,
		ItemPurificationApplicationOperationType sourceOperation,
		int objectId,
		int itemId)
	{
		return new ItemPurificationQuestNotificationCandidate(type, sourceOperation, objectId, itemId);
	}
}
