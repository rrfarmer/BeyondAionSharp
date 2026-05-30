using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ExchangeTradeValidationPlanStatus
{
	Valid,
	BlockedActivePlayerTooHeavy,
	BlockedPartnerTooHeavy,
	BlockedBothTooHeavy,
}

public sealed record ExchangeTradeValidationPlan(
	ExchangeTradeValidationPlanStatus Status,
	SmSystemMessage? ActivePlayerMessage,
	SmSystemMessage? PartnerMessage,
	bool IsValid,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ExchangeTradeValidationPlanService
{
	public static ExchangeTradeValidationPlan CreatePlan(
		bool activePlayerInventoryFull,
		bool partnerInventoryFull)
	{
		// Java parity: services/ExchangeService.addItemsToInventory (called from performTrade validation).
		// If partner too heavy → STR_EXCHANGE_CANT_EXCHANGE_HEAVY_TO_ADD_EXCHANGE_ITEM to activePlayer,
		//                        STR_PARTNER_TOO_HEAVY_TO_EXCHANGE to partner (this is the partner's perspective).
		// If activePlayer too heavy → STR_PARTNER_TOO_HEAVY_TO_EXCHANGE to activePlayer (active sees partner message),
		//                              STR_EXCHANGE_CANT_EXCHANGE_HEAVY_TO_ADD_EXCHANGE_ITEM to partner.
		// If both: send both.
		// If neither: trade can proceed.
		if (!activePlayerInventoryFull && !partnerInventoryFull)
		{
			return new ExchangeTradeValidationPlan(
				ExchangeTradeValidationPlanStatus.Valid,
				ActivePlayerMessage: null,
				PartnerMessage: null,
				IsValid: true,
				"ExchangeService.validateInventorySize -> both inventories have room -> trade can proceed"
			);
		}

		SmSystemMessage? activeMessage = null;
		SmSystemMessage? partnerMessage = null;

		if (partnerInventoryFull)
		{
			// activePlayer is adding to full partner → activePlayer gets HEAVY message, partner gets PARTNER_TOO_HEAVY
			activeMessage = SmSystemMessage.ExchangeCantExchangeHeavyToAddExchangeItem();
			partnerMessage = SmSystemMessage.ExchangePartnerTooHeavyToExchange();
		}

		if (activePlayerInventoryFull)
		{
			// partner is adding to full activePlayer → activePlayer gets PARTNER_TOO_HEAVY, partner gets HEAVY message
			activeMessage = SmSystemMessage.ExchangePartnerTooHeavyToExchange();
			partnerMessage = SmSystemMessage.ExchangeCantExchangeHeavyToAddExchangeItem();
		}

		return new ExchangeTradeValidationPlan(
			activePlayerInventoryFull && partnerInventoryFull
				? ExchangeTradeValidationPlanStatus.BlockedBothTooHeavy
				: partnerInventoryFull
					? ExchangeTradeValidationPlanStatus.BlockedPartnerTooHeavy
					: ExchangeTradeValidationPlanStatus.BlockedActivePlayerTooHeavy,
			activeMessage,
			partnerMessage,
			IsValid: false,
			"ExchangeService.addItemsToInventory -> inventory full -> STR_EXCHANGE/STR_PARTNER message dispatch"
		);
	}
}
