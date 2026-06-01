using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreCreatePlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_ZeroItemsCreatesClosePlanBeforeOpenGuard()
	{
		var packet = CreatePacket();
		var context = CreateContext(isFlying: true, storeIsOpen: true);

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(packet, context, EmptyItems);

		Assert.Equal(PrivateStoreCreatePlanStatus.ClosePlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.NotNull(plan.ClosePlan);
		Assert.Equal(PrivateStoreClosePlanStatus.PlanCreated, plan.ClosePlan!.Status);
		Assert.Null(plan.OpenGuardPlan);
		Assert.Empty(plan.ItemValidationSteps);
		Assert.False(plan.WouldSetStore);
		Assert.Contains("tradePSItems.length <= 0", plan.JavaSource);
	}

	[Fact]
	public void CreateDisabledPlan_ZeroItemsWithNoOpenStoreCarriesSkippedClosePlan()
	{
		var packet = CreatePacket();
		var context = CreateContext(storeIsOpen: false);

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(packet, context, EmptyItems);

		Assert.Equal(PrivateStoreCreatePlanStatus.ClosePlanCreated, plan.Status);
		Assert.NotNull(plan.ClosePlan);
		Assert.Equal(PrivateStoreClosePlanStatus.SkippedNoStoreOpen, plan.ClosePlan!.Status);
	}

	[Fact]
	public void CreateDisabledPlan_OpenGuardBlocksBeforeItemValidation()
	{
		var packet = CreatePacket(new CmPrivateStoreEntry(3001, 100000001, 1, 100));
		var context = CreateContext(isInCombatMode: true);

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(packet, context, ValidItems(3001, 100000001));

		Assert.Equal(PrivateStoreCreatePlanStatus.OpenGuardBlocked, plan.Status);
		Assert.NotNull(plan.OpenGuardPlan);
		Assert.Equal(PrivateStoreOpenGuardPlanStatus.BlockedInCombatMode, plan.OpenGuardPlan!.Status);
		Assert.Empty(plan.ItemValidationSteps);
		Assert.False(plan.WouldSetStore);
		Assert.False(plan.WouldBroadcastOpenEmotion);
	}

	[Fact]
	public void CreateDisabledPlan_ValidatesItemsInReadOrderAndRecordsOpenBroadcastIntent()
	{
		var packet = CreatePacket(
			new CmPrivateStoreEntry(3001, 100000001, 1, 10_000),
			new CmPrivateStoreEntry(3002, 182003001, 5, 300));

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(
			packet,
			CreateContext(),
			ValidItems((3001, 100000001, 10), (3002, 182003001, 10)));

		Assert.Equal(PrivateStoreCreatePlanStatus.DisabledNoSideEffects, plan.Status);
		Assert.False(plan.IsLive);
		Assert.NotNull(plan.OpenGuardPlan);
		Assert.Equal(PrivateStoreOpenGuardPlanStatus.CanOpen, plan.OpenGuardPlan!.Status);
		Assert.Equal(2, plan.ItemValidationSteps.Count);
		Assert.All(plan.ItemValidationSteps, step => Assert.Equal(PrivateStoreItemValidationPlanStatus.Valid, step.ValidationPlan.Status));
		Assert.Equal([3001, 3002], plan.StoredItemIntents.Select(item => item.ItemObjectId).ToArray());
		Assert.True(plan.WouldSetStore);
		Assert.True(plan.WouldSetPrivateShopState);
		Assert.True(plan.WouldBroadcastOpenEmotion);
		Assert.IsType<SmEmotion>(plan.OpenPrivateShopEmotionPacket);
		Assert.Contains(PrivateStoreCreateStepKind.BroadcastOpenPrivateShopEmotion, plan.Steps.Select(step => step.Kind));
	}

	[Fact]
	public void CreateDisabledPlan_MissingInventoryItemStopsBeforeStoreAssignment()
	{
		var packet = CreatePacket(new CmPrivateStoreEntry(3001, 100000001, 1, 10_000));

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(packet, CreateContext(), EmptyItems);

		Assert.Equal(PrivateStoreCreatePlanStatus.ItemValidationBlocked, plan.Status);
		var step = Assert.Single(plan.ItemValidationSteps);
		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedNullOrMismatchedItem, step.ValidationPlan.Status);
		Assert.Empty(plan.StoredItemIntents);
		Assert.False(plan.WouldSetStore);
		Assert.Null(plan.OpenPrivateShopEmotionPacket);
		Assert.Contains("returns before store assignment", plan.JavaSource);
	}

	[Fact]
	public void CreateDisabledPlan_InvalidSecondItemKeepsPriorStoredIntentAndStops()
	{
		var packet = CreatePacket(
			new CmPrivateStoreEntry(3001, 100000001, 1, 10_000),
			new CmPrivateStoreEntry(3002, 182003001, 11, 300));

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(
			packet,
			CreateContext(),
			ValidItems((3001, 100000001, 10), (3002, 182003001, 10)));

		Assert.Equal(PrivateStoreCreatePlanStatus.ItemValidationBlocked, plan.Status);
		Assert.Equal(2, plan.ItemValidationSteps.Count);
		Assert.Equal(PrivateStoreItemValidationPlanStatus.Valid, plan.ItemValidationSteps[0].ValidationPlan.Status);
		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedInvalidCount, plan.ItemValidationSteps[1].ValidationPlan.Status);
		var stored = Assert.Single(plan.StoredItemIntents);
		Assert.Equal(3001, stored.ItemObjectId);
		Assert.False(plan.WouldSetStore);
	}

	[Fact]
	public void CreateDisabledPlan_DuplicateObjectIdBlocksAsAlreadyRegisteredOnSecondItem()
	{
		var packet = CreatePacket(
			new CmPrivateStoreEntry(3001, 100000001, 1, 10_000),
			new CmPrivateStoreEntry(3001, 100000001, 1, 20_000));

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(
			packet,
			CreateContext(),
			ValidItems(3001, 100000001));

		Assert.Equal(PrivateStoreCreatePlanStatus.ItemValidationBlocked, plan.Status);
		Assert.Equal(2, plan.ItemValidationSteps.Count);
		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedAlreadyRegistered, plan.ItemValidationSteps[1].ValidationPlan.Status);
		Assert.NotNull(plan.ItemValidationSteps[1].ValidationPlan.DenialMessage);
		Assert.Equal(1300942, plan.ItemValidationSteps[1].ValidationPlan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateDisabledPlan_EleventhItemBlocksWithStoreFullMessage()
	{
		var entries = Enumerable.Range(1, 11)
			.Select(index => new CmPrivateStoreEntry(3000 + index, 100000000 + index, 1, 100))
			.ToArray();
		var itemContexts = entries.ToDictionary(
			entry => entry.ItemObjectId,
			entry => ValidItem(entry.ItemObjectId, entry.ItemId));

		var plan = PrivateStoreCreatePlanService.CreateDisabledPlan(CreatePacket(entries), CreateContext(), itemContexts);

		Assert.Equal(PrivateStoreCreatePlanStatus.ItemValidationBlocked, plan.Status);
		Assert.Equal(11, plan.ItemValidationSteps.Count);
		Assert.Equal(10, plan.StoredItemIntents.Count);
		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedStoreIsFull, plan.ItemValidationSteps[10].ValidationPlan.Status);
		Assert.NotNull(plan.ItemValidationSteps[10].ValidationPlan.DenialMessage);
		Assert.Equal(1300666, plan.ItemValidationSteps[10].ValidationPlan.DenialMessage!.MessageId);
	}

	private static readonly IReadOnlyDictionary<int, PrivateStoreCreateItemContext> EmptyItems =
		new Dictionary<int, PrivateStoreCreateItemContext>();

	private static CmPrivateStore CreatePacket(params CmPrivateStoreEntry[] entries)
	{
		var packet = new CmPrivateStore(119, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteH(entries.Length);
		foreach (var entry in entries)
		{
			buffer.WriteD(entry.ItemObjectId);
			buffer.WriteD(entry.ItemId);
			buffer.WriteH(entry.Count);
			buffer.WriteQ(entry.Price);
		}

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static PrivateStoreCreatePlayerContext CreateContext(
		bool storeIsOpen = false,
		bool isFlying = false,
		bool isInMove = false,
		bool isInCombatMode = false,
		bool isTrading = false,
		bool isInRideOrRobotMode = false,
		bool isHidden = false,
		bool isDead = false,
		bool isInChairState = false)
	{
		return new PrivateStoreCreatePlayerContext(
			PlayerObjectId: 9001,
			CreatureState: 0,
			storeIsOpen,
			isFlying,
			isInMove,
			isInCombatMode,
			isTrading,
			isInRideOrRobotMode,
			isHidden,
			isDead,
			isInChairState);
	}

	private static IReadOnlyDictionary<int, PrivateStoreCreateItemContext> ValidItems(int objectId, int itemId) =>
		new Dictionary<int, PrivateStoreCreateItemContext>
		{
			[objectId] = ValidItem(objectId, itemId)
		};

	private static IReadOnlyDictionary<int, PrivateStoreCreateItemContext> ValidItems(params (int ObjectId, int ItemId, long AvailableCount)[] items) =>
		items.ToDictionary(item => item.ObjectId, item => ValidItem(item.ObjectId, item.ItemId, item.AvailableCount));

	private static PrivateStoreCreateItemContext ValidItem(int objectId, int itemId, long availableCount = 10) =>
		new(
			objectId,
			itemId,
			availableCount,
			ItemExistsAndIdMatches: true,
			ItemIsPackCountAboveZeroOrTradeable: true,
			ItemIsEquipped: false);
}
