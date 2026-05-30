using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreItemValidationPlanServiceTests
{
	[Fact]
	public void CreatePlan_AllClearItemIsValid()
	{
		var plan = Valid();

		Assert.Equal(PrivateStoreItemValidationPlanStatus.Valid, plan.Status);
		Assert.True(plan.IsValid);
		Assert.False(plan.IsLive);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_NullOrMismatchedItemBlocksSilently()
	{
		var plan = CreatePlan(itemExistsAndIdMatches: false);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedNullOrMismatchedItem, plan.Status);
		Assert.False(plan.IsValid);
		Assert.Null(plan.DenialMessage);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreatePlan_CountBelowOneBlocksSilently(long count)
	{
		var plan = CreatePlan(requestedCount: count);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedInvalidCount, plan.Status);
		Assert.False(plan.IsValid);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_CountExceedsAvailableBlocksSilently()
	{
		var plan = CreatePlan(requestedCount: 11, availableCount: 10);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedInvalidCount, plan.Status);
		Assert.Null(plan.DenialMessage);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-100)]
	public void CreatePlan_NegativePriceBlocksSilently(long price)
	{
		var plan = CreatePlan(requestedPrice: price);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedNegativePrice, plan.Status);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_ZeroPriceIsAllowed()
	{
		var plan = CreatePlan(requestedPrice: 0);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.Valid, plan.Status);
	}

	[Fact]
	public void CreatePlan_StoreFullBlocksWithJavaFullBasketMessage()
	{
		var plan = CreatePlan(currentStoreItemCount: 10);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedStoreIsFull, plan.Status);
		Assert.False(plan.IsValid);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300666, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_StoreAtNineItemsIsNotFull()
	{
		var plan = CreatePlan(currentStoreItemCount: 9);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.Valid, plan.Status);
	}

	[Fact]
	public void CreatePlan_NotTradeableBlocksWithJavaExchangeMessage()
	{
		var plan = CreatePlan(itemIsPackCountAboveZeroOrTradeable: false);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedNotTradeable, plan.Status);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300661, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_EquippedItemBlocksWithJavaEquippedMessage()
	{
		var plan = CreatePlan(itemIsEquipped: true);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedItemIsEquipped, plan.Status);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300660, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_AlreadyRegisteredBlocksWithJavaAlreadyRegisteredMessage()
	{
		var plan = CreatePlan(itemAlreadyRegistered: true);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedAlreadyRegistered, plan.Status);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300942, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_NullMismatchBlocksBeforeAllOtherGuards()
	{
		var plan = CreatePlan(itemExistsAndIdMatches: false, currentStoreItemCount: 10, itemIsEquipped: true);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedNullOrMismatchedItem, plan.Status);
	}

	[Fact]
	public void CreatePlan_StoreFullBlocksBeforeTradeableAndEquipGuards()
	{
		var plan = CreatePlan(currentStoreItemCount: 10, itemIsPackCountAboveZeroOrTradeable: false, itemIsEquipped: true);

		Assert.Equal(PrivateStoreItemValidationPlanStatus.BlockedStoreIsFull, plan.Status);
	}

	private static PrivateStoreItemValidationPlan Valid()
	{
		return CreatePlan();
	}

	private static PrivateStoreItemValidationPlan CreatePlan(
		bool itemExistsAndIdMatches = true,
		long requestedCount = 1,
		long availableCount = 10,
		long requestedPrice = 100,
		int currentStoreItemCount = 0,
		bool itemIsPackCountAboveZeroOrTradeable = true,
		bool itemIsEquipped = false,
		bool itemAlreadyRegistered = false)
	{
		return PrivateStoreItemValidationPlanService.CreatePlan(
			itemExistsAndIdMatches, requestedCount, availableCount, requestedPrice,
			currentStoreItemCount, itemIsPackCountAboveZeroOrTradeable, itemIsEquipped, itemAlreadyRegistered);
	}
}
