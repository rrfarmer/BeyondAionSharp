namespace Aion.GameServer.Services;

public sealed record ItemChargePricePlan(
	int Price1,
	int Price2,
	int ChargeLevel,
	int CurrentChargePoints,
	int NextChargeLevel,
	double FirstLevel,
	double UpdateLevel,
	long PayAmount,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ItemChargePricePlanService
{
	// Java parity: model/items/ChargeInfo constants.
	public const int ChargeLevel1Threshold = 500_000;
	public const int ChargeLevel2Threshold = 1_000_000;

	public static ItemChargePricePlan CreatePlan(
		int price1,
		int price2,
		int chargeLevel,
		int currentChargePoints)
	{
		// Java parity: services/item/ItemChargeService.getPayAmountForService.
		if (price1 == 0 && price2 == 0)
		{
			return new ItemChargePricePlan(price1, price2, chargeLevel, currentChargePoints,
				NextChargeLevel: 0, FirstLevel: 0, UpdateLevel: 0, PayAmount: 0,
				"ItemChargeService.getPayAmountForService -> improvement == null or no price -> 0");
		}

		double firstLevel = price1 / 2.0;
		double updateLevel = Math.Round(firstLevel + (price2 - price1) / 2.0);

		var nextLevel = GetNextChargeLevel(currentChargePoints);
		double money = 0;
		float currentChargeRatio = 1f;

		switch (chargeLevel)
		{
			case 1:
				currentChargeRatio -= (float)currentChargePoints / ChargeLevel1Threshold;
				money = Math.Ceiling(firstLevel * currentChargeRatio);
				break;
			case 2:
				switch (nextLevel)
				{
					case 1: // needs to go through level 1 first
						currentChargeRatio -= (float)currentChargePoints / ChargeLevel1Threshold;
						money = Math.Ceiling(firstLevel * currentChargeRatio) + updateLevel;
						break;
					case 2: // already at level 1, just topping up to level 2
						currentChargeRatio -= (float)(currentChargePoints - ChargeLevel1Threshold)
							/ (ChargeLevel2Threshold - ChargeLevel1Threshold);
						money = Math.Ceiling(updateLevel * currentChargeRatio);
						break;
				}
				break;
		}

		var payAmount = Math.Max(0L, (long)money);
		return new ItemChargePricePlan(
			price1, price2, chargeLevel, currentChargePoints,
			nextLevel, firstLevel, updateLevel, payAmount,
			$"ItemChargeService.getPayAmountForService -> level={chargeLevel} charge={currentChargePoints} firstLevel={firstLevel} updateLevel={updateLevel} -> {payAmount}"
		);
	}

	public static int GetNextChargeLevel(int chargePoints)
	{
		// Java parity: ItemChargeService.getNextChargeLevel(Item).
		if (chargePoints < ChargeLevel1Threshold)
			return 1;
		if (chargePoints < ChargeLevel2Threshold)
			return 2;
		return 0; // already at max (Java throws; C# returns 0)
	}
}
