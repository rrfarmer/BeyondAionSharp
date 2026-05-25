namespace Aion.GameServer.Services;

public sealed class TradeApFormulaService
{
	public static int CalculateAbyssBuyRequiredAp(
		IEnumerable<TradeApCostComponent> apCostComponents,
		int sellPriceRate,
		int vendorBuyModifier)
	{
		// Java parity: model/trade/TradeList.calculateAbyssRewardBuyList accumulates each AP/ABYSS acquisition
		// component with `(int) ((requiredAp * count * modifier / 100.0D) * vendorBuyModifier) / 100`.
		var requiredAp = 0;
		foreach (var component in apCostComponents)
		{
			requiredAp += CalculateAbyssBuyRequiredAp(
				component.RequiredApPerItem,
				component.Count,
				sellPriceRate,
				vendorBuyModifier);
		}

		return requiredAp;
	}

	public static int CalculateAbyssBuyRequiredAp(
		int requiredApPerItem,
		long count,
		int sellPriceRate,
		int vendorBuyModifier)
	{
		// Java narrows the double result to int before the final `/ 100` integer division.
		var narrowed = JavaDoubleToInt(unchecked(requiredApPerItem * count * sellPriceRate) / 100.0d * vendorBuyModifier);
		return narrowed / 100;
	}

	public static int CalculateApResaleReward(
		int requiredApPerItem,
		float buyPriceRate,
		long count)
	{
		// Java parity: TradeService.performSellForAPToShop:
		// `Math.round((requiredAp * purchaseTemplate.getBuyPriceRate()) / 100F) * (int) count`.
		var apPerItem = JavaMathRound(requiredApPerItem * buyPriceRate / 100f);
		return apPerItem * unchecked((int)count);
	}

	public static int CalculateTradeInApDelta(
		int targetRequiredAp,
		IEnumerable<int> tradeInItemRequiredAp,
		int count,
		int sellPriceRate,
		int vendorBuyModifier)
	{
		// Java parity: TradeService.performBuyFromTradeInTrade subtracts the AP value of required trade-in item
		// templates from the target item AP value, and spends only a positive difference.
		var requiredAp = CalculateAbyssBuyRequiredAp(targetRequiredAp, count, sellPriceRate, vendorBuyModifier);
		var differenceAp = 0;
		foreach (var itemRequiredAp in tradeInItemRequiredAp)
			differenceAp += CalculateAbyssBuyRequiredAp(itemRequiredAp, count, sellPriceRate, vendorBuyModifier);

		return Math.Max(0, requiredAp - differenceAp);
	}

	private static int JavaDoubleToInt(double value)
	{
		if (double.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)value;
	}

	private static int JavaMathRound(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)MathF.Floor(value + 0.5f);
	}
}

public sealed record TradeApCostComponent(int RequiredApPerItem, long Count);
