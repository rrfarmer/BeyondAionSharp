using Aion.GameServer.Configuration;

namespace Aion.GameServer.Services;

public sealed record PriceInfluenceRates(float Elyos = 0.5f, float Asmodians = 0.5f);

public sealed record PriceSnapshot(
	int GlobalPrices,
	int GlobalPricesModifier,
	int Taxes,
	int VendorBuyModifier,
	int VendorSellModifier);

public static class PricesService
{
	public static PriceSnapshot CreateSnapshot(
		string playerRace,
		GameServerPriceOptions options,
		PriceInfluenceRates influenceRates)
	{
		return new PriceSnapshot(
			GetGlobalPrices(playerRace, options, influenceRates),
			GetGlobalPricesModifier(options),
			GetTaxes(playerRace, options, influenceRates),
			GetVendorBuyModifier(options),
			GetVendorSellModifier(options));
	}

	public static int GetGlobalPrices(
		string playerRace,
		GameServerPriceOptions options,
		PriceInfluenceRates influenceRates)
	{
		// Java parity: services/trade/PricesService.getGlobalPrices.
		var defaultPrices = options.DefaultPrices;
		var influenceValue = GetPriceInfluenceRate(playerRace, influenceRates);
		if (influenceValue == 0.5f)
			return defaultPrices;
		if (influenceValue > 0.5f)
		{
			var diff = influenceValue - 0.5f;
			return JavaMathRound(defaultPrices - diff / 2f * 100f);
		}

		var lowDiff = 0.5f - influenceValue;
		return JavaMathRound(defaultPrices + lowDiff / 2f * 100f);
	}

	public static int GetGlobalPricesModifier(GameServerPriceOptions options)
	{
		// Java parity: services/trade/PricesService.getGlobalPricesModifier.
		return options.DefaultModifier;
	}

	public static int GetTaxes(
		string playerRace,
		GameServerPriceOptions options,
		PriceInfluenceRates influenceRates)
	{
		// Java parity: services/trade/PricesService.getTaxes.
		var defaultTax = options.DefaultTaxes;
		var influenceValue = GetPriceInfluenceRate(playerRace, influenceRates);
		if (influenceValue >= 0.5f)
			return defaultTax;

		var diff = 0.5f - influenceValue;
		return JavaMathRound(defaultTax + diff / 4f * 100f);
	}

	public static int GetVendorBuyModifier(GameServerPriceOptions options)
	{
		// Java parity: services/trade/PricesService.getVendorBuyModifier.
		return options.VendorBuyModifier;
	}

	public static int GetVendorSellModifier(GameServerPriceOptions options)
	{
		// Java parity: services/trade/PricesService.getVendorSellModifier.
		return options.VendorSellModifier;
	}

	public static long GetPriceForService(
		long basePrice,
		string playerRace,
		GameServerPriceOptions options,
		PriceInfluenceRates influenceRates)
	{
		// Java parity: PricesService.getPriceForService floors after each double percentage step.
		var price = JavaDoubleToLong(basePrice * GetGlobalPrices(playerRace, options, influenceRates) / 100d);
		price = JavaDoubleToLong(price * GetGlobalPricesModifier(options) / 100d);
		return JavaDoubleToLong(price * GetTaxes(playerRace, options, influenceRates) / 100d);
	}

	public static long GetBuyPrice(
		long requiredKinah,
		string playerRace,
		GameServerPriceOptions options,
		PriceInfluenceRates influenceRates)
	{
		// Java parity: PricesService.getBuyPrice applies vendor buy before global prices, modifier, and taxes.
		var price = JavaDoubleToLong(requiredKinah * GetVendorBuyModifier(options) / 100d);
		price = JavaDoubleToLong(price * GetGlobalPrices(playerRace, options, influenceRates) / 100d);
		price = JavaDoubleToLong(price * GetGlobalPricesModifier(options) / 100d);
		return JavaDoubleToLong(price * GetTaxes(playerRace, options, influenceRates) / 100d);
	}

	public static long GetSellReward(long kinahValue, int sellModifier)
	{
		// Java parity: PricesService.getSellReward.
		return JavaDoubleToLong(kinahValue * sellModifier / 100d);
	}

	private static float GetPriceInfluenceRate(string playerRace, PriceInfluenceRates influenceRates)
	{
		return playerRace.ToUpperInvariant() switch
		{
			"ASMODIANS" => influenceRates.Asmodians,
			"ELYOS" => influenceRates.Elyos,
			_ => throw new ArgumentException($"{playerRace} is no valid player race.", nameof(playerRace)),
		};
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

	private static long JavaDoubleToLong(double value)
	{
		if (double.IsNaN(value))
			return 0;
		if (value <= long.MinValue)
			return long.MinValue;
		if (value >= long.MaxValue)
			return long.MaxValue;
		return (long)value;
	}
}
