namespace Aion.GameServer.Services;

public sealed record BindPointTeleportPricePlan(
	int HotspotId,
	long BasePrice,
	double Distance,
	long DistanceCost,
	long ComputedPrice,
	long ClientPrice,
	long PriceDifference,
	bool ShouldWarnPriceMismatch,
	long FinalPrice,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportPricePlanService
{
	public static BindPointTeleportPricePlan CreatePlan(
		int hotspotId,
		float playerX,
		float playerY,
		float playerZ,
		float hotspotX,
		float hotspotY,
		float hotspotZ,
		long hotspotBasePrice,
		long priceSentByGameClient)
	{
		// Java parity: services/teleport/BindPointTeleportService.calculateTeleportationPrice.
		// This is only the distance/client-price reconciliation slice; checkRequirements and live teleport scheduling are separate.
		var distance = GetDistance(playerX, playerY, playerZ, hotspotX, hotspotY, hotspotZ);
		var distanceCost = (long)(hotspotBasePrice * distance / 1000d);
		var computedPrice = Math.Max(1, hotspotBasePrice + distanceCost);
		var priceDifference = JavaLongAbs(computedPrice - priceSentByGameClient);
		var shouldWarn = priceDifference > 1;
		var finalPrice = Math.Max(computedPrice, priceSentByGameClient);

		return new BindPointTeleportPricePlan(
			hotspotId,
			hotspotBasePrice,
			distance,
			distanceCost,
			computedPrice,
			priceSentByGameClient,
			priceDifference,
			shouldWarn,
			finalPrice,
			"BindPointTeleportService.calculateTeleportationPrice -> PositionUtil.getDistance -> Math.max(computedPrice, clientPrice)",
			IsLive: false);
	}

	private static double GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
	{
		// Java PositionUtil stores the coordinate deltas as float before Math.sqrt.
		float dx = x1 - x2;
		float dy = y1 - y2;
		float dz = z1 - z2;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}

	private static long JavaLongAbs(long value)
	{
		return value == long.MinValue ? long.MinValue : Math.Abs(value);
	}
}
