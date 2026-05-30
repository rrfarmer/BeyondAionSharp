using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ExchangeTradeValidationPlanServiceTests
{
	[Fact]
	public void CreatePlan_BothInventoriesHaveRoomIsValid()
	{
		var plan = ExchangeTradeValidationPlanService.CreatePlan(
			activePlayerInventoryFull: false, partnerInventoryFull: false);

		Assert.Equal(ExchangeTradeValidationPlanStatus.Valid, plan.Status);
		Assert.True(plan.IsValid);
		Assert.Null(plan.ActivePlayerMessage);
		Assert.Null(plan.PartnerMessage);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_PartnerInventoryFullSendsHeavyMessageToActive()
	{
		var plan = ExchangeTradeValidationPlanService.CreatePlan(
			activePlayerInventoryFull: false, partnerInventoryFull: true);

		Assert.Equal(ExchangeTradeValidationPlanStatus.BlockedPartnerTooHeavy, plan.Status);
		Assert.False(plan.IsValid);
		Assert.NotNull(plan.ActivePlayerMessage);
		Assert.NotNull(plan.PartnerMessage);
		// Active gets "you are too heavy to add item" (their items can't go to partner)
		Assert.Equal(1300359, plan.ActivePlayerMessage!.MessageId); // ExchangeCantExchangeHeavyToAddExchangeItem
		// Partner gets "partner is too heavy"
		Assert.Equal(1300357, plan.PartnerMessage!.MessageId); // ExchangePartnerTooHeavyToExchange
	}

	[Fact]
	public void CreatePlan_ActivePlayerInventoryFullSendsPartnerHeavyMessage()
	{
		var plan = ExchangeTradeValidationPlanService.CreatePlan(
			activePlayerInventoryFull: true, partnerInventoryFull: false);

		Assert.Equal(ExchangeTradeValidationPlanStatus.BlockedActivePlayerTooHeavy, plan.Status);
		Assert.False(plan.IsValid);
		// Active gets "partner is too heavy" (partner's items can't come to them)
		Assert.Equal(1300357, plan.ActivePlayerMessage!.MessageId); // ExchangePartnerTooHeavyToExchange
		// Partner gets "you are too heavy to add item"
		Assert.Equal(1300359, plan.PartnerMessage!.MessageId); // ExchangeCantExchangeHeavyToAddExchangeItem
	}

	[Fact]
	public void CreatePlan_BothFullIsBlockedBothTooHeavy()
	{
		var plan = ExchangeTradeValidationPlanService.CreatePlan(
			activePlayerInventoryFull: true, partnerInventoryFull: true);

		Assert.Equal(ExchangeTradeValidationPlanStatus.BlockedBothTooHeavy, plan.Status);
		Assert.False(plan.IsValid);
	}
}
