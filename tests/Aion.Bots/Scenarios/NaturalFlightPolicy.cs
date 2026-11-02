using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>A FLY or NO_FLY zone: a polygon with a floor and a ceiling (<c>zones_&lt;map&gt;.xml</c>).</summary>
public sealed record NaturalFlyZone(string Name, bool Forbids, IReadOnlyList<(float X, float Y)> Polygon, float Bottom, float Top)
{
	public bool Contains(float x, float y, float z)
	{
		if (z < Bottom || z > Top) return false;
		bool inside = false;
		for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
		{
			(float xi, float yi) = Polygon[i];
			(float xj, float yj) = Polygon[j];
			if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
				inside = !inside;
		}
		return inside;
	}

	/// <summary>Every FLY and NO_FLY zone of one map's zone file.</summary>
	public static IReadOnlyList<NaturalFlyZone> Load(string zonesXml) =>
		XDocument.Load(zonesXml).Root!.Elements("zone")
			.Where(zone => (string?)zone.Attribute("zone_type") is "FLY" or "NO_FLY")
			.Select(zone =>
			{
				XElement points = zone.Element("points")!;
				return new NaturalFlyZone((string)zone.Attribute("name")!, (string?)zone.Attribute("zone_type") == "NO_FLY",
					points.Elements("point").Select(point => (Float(point, "x"), Float(point, "y"))).ToArray(),
					Float(points, "bottom"), Float(points, "top"));
			}).ToArray();

	/// <summary>
	/// NL-3: the zones a player flies in on one map: its zone file's, and on a map whose world flags hold FLY (Reshanta,
	/// whose zone file has no FLY zone) the whole map from the world floor up. The server resolves the map FLY
	/// attribute through Creature.isInsideFlyZone; this planning overlay keeps the same effective permission.
	/// <para>
	/// Upstream 82a5e0cf9 removed the per-map death levels this used to start from: the floor is now the one height
	/// PlayerMoveController kills below, two meters under zero.
	/// </para>
	/// </summary>
	public static IReadOnlyList<NaturalFlyZone> ForMap(string repoRoot, Aion.GameServer.Model.Templates.World.WorldMapTemplate? map, int mapId)
	{
		string file = Path.Combine(repoRoot, $"game-server/data/static_data/zones/zones_{mapId}.xml");
		IReadOnlyList<NaturalFlyZone> zones = File.Exists(file) ? Load(file) : [];
		if (map == null || !map.HasAttribute(Aion.GameServer.World.Zone.ZoneAttributes.Fly) || map.GetWorldSize() <= 0) return zones;
		float size = map.GetWorldSize();
		return [.. zones, new NaturalFlyZone($"WHOLE_MAP_{mapId}", false, [(0, 0), (size, 0), (size, size), (0, size)],
			WorldFloorZ, WorldFloorZ + size)];
	}

	/// <summary>The lowest Z a player survives: PlayerMoveController kills below -2.</summary>
	private const float WorldFloorZ = -2;

	private static float Float(XElement node, string name) =>
		float.Parse((string)node.Attribute(name)!, CultureInfo.InvariantCulture);
}

/// <summary>What the client knows when it considers taking off.</summary>
/// <param name="OnWaterArea">The bot stands in a navmesh water area.</param>
/// <param name="LastTakeoffMillis">Game time of the last takeoff this session, if any.</param>
/// <param name="NoFlyEffect">An effect with the NOFLY abnormal state is observed.</param>
public sealed record NaturalTakeoffObservation(bool Daeva, BotPosition Position, bool OnWaterArea, float WaterLevel, long NowMillis,
	long? LastTakeoffMillis, bool NoFlyEffect, bool Transformed, bool PrivateStore);

public sealed record NaturalFlightDecision(bool Allowed, string Reason);

/// <summary>A place the bot may land: the ground, or a platform such as Borender's rock.</summary>
public sealed record NaturalLandingTarget(string Kind, BotPosition Position);

/// <summary>One planned flight: fly the legs, spend <paramref name="AirborneWorkSeconds"/> in the air, then land.</summary>
/// <param name="LegMeters">Air distance of each leg, takeoff to landing.</param>
/// <param name="AirborneWorkSeconds">Time spent airborne at the far end (an air fight). Zero when the bot lands there.</param>
public sealed record NaturalFlightPlan(IReadOnlyList<BotPosition> Waypoints, IReadOnlyList<float> LegMeters, float SpeedMetersPerSecond,
	int AirborneWorkSeconds);

/// <summary>
/// AF-04 (docs/natural-altgard-leveling.md): when the bot may fly, and how far its flight time takes it. The server rules
/// are Java's (4.8 <c>FlyController</c>, <c>PlayerLifeStats</c>, <c>LifeStatsRestoreService</c>); refusing to take off from
/// water is the client's rule, so the bot copies it. Pure: no clock, no network.
/// </summary>
public static class NaturalFlightPolicy
{
	/// <summary>Java sets the next takeoff to now + FLY_REUSE_TIME (10 s) - 100 ms.</summary>
	public const int TakeoffReuseMillis = 10_000 - 100;
	/// <summary>Flying inside a FLY zone costs 1 FP per second; outside it 2 (Java <c>triggerFpReduce</c>).</summary>
	public const int FpPerSecondInZone = 1;
	/// <summary>On the ground FP comes back 3 at a time every 6 s, the first 3 s after landing (<c>restoreFp</c>).</summary>
	public const int FpRestoreAmount = 3, FpRestoreFirstMillis = 3_000, FpRestorePeriodMillis = 6_000;
	/// <summary>FP kept in hand at the moment of landing, for a missed approach or a slow descent.</summary>
	public const int LandingReserveFp = 10;

	public static NaturalFlightDecision CanTakeOff(NaturalTakeoffObservation state, IReadOnlyList<NaturalFlyZone> zones)
	{
		if (!state.Daeva) return new(false, "Only a Daeva can fly.");
		BotPosition at = state.Position;
		if (!zones.Any(zone => !zone.Forbids && zone.Contains(at.X, at.Y, at.Z)) || zones.Any(zone => zone.Forbids && zone.Contains(at.X, at.Y, at.Z)))
			return new(false, "Flight is forbidden here: not inside a FLY zone, or inside a NO_FLY zone.");
		if (state.OnWaterArea || at.Z < state.WaterLevel)
			return new(false, "The client refuses to take off from water.");
		if (state.NoFlyEffect) return new(false, "A NOFLY effect is active.");
		if (state.Transformed) return new(false, "A transformation forbids flight.");
		if (state.PrivateStore) return new(false, "A private store is open.");
		if (state.LastTakeoffMillis is { } last && state.NowMillis < last + TakeoffReuseMillis)
			return new(false, $"Takeoff reuse: {last + TakeoffReuseMillis - state.NowMillis} ms left.");
		return new(true, "Ready to take off.");
	}

	/// <summary>FP a flight costs: one per started second in the zone, the first taken one second after takeoff.</summary>
	public static int FlightCost(NaturalFlightPlan plan) =>
		(int)MathF.Ceiling(plan.LegMeters.Sum() / plan.SpeedMetersPerSecond) + plan.AirborneWorkSeconds;

	/// <summary>
	/// A flight is taken only when every waypoint stays inside a FLY zone (leaving one ends the flight and the character
	/// falls) and the FP left at landing is at least <see cref="LandingReserveFp"/>.
	/// </summary>
	public static NaturalFlightDecision CanFly(NaturalFlightPlan plan, int currentFp, IReadOnlyList<NaturalFlyZone> zones)
	{
		foreach (BotPosition point in plan.Waypoints)
			if (!zones.Any(zone => !zone.Forbids && zone.Contains(point.X, point.Y, point.Z)) ||
				zones.Any(zone => zone.Forbids && zone.Contains(point.X, point.Y, point.Z)))
				return new(false, $"Waypoint ({point.X:F0}, {point.Y:F0}, {point.Z:F0}) leaves the FLY zone.");
		int cost = FlightCost(plan);
		int left = currentFp - cost;
		return left >= LandingReserveFp
			? new(true, $"Costs {cost} FP of {currentFp}; lands with {left}.")
			: new(false, $"Costs {cost} FP of {currentFp}; would land with {left}, under the {LandingReserveFp} FP reserve.");
	}

	/// <summary>While airborne: land now when the FP left would not cover the way to the landing plus the reserve.</summary>
	public static NaturalFlightDecision MustLand(int currentFp, float metersToLanding, float speedMetersPerSecond) =>
		currentFp - (int)MathF.Ceiling(metersToLanding / speedMetersPerSecond) < LandingReserveFp
			? new(true, $"{currentFp} FP left and {metersToLanding:F0} m to the landing: land now.")
			: new(false, "Enough flight time to continue.");

	/// <summary>Game time on the ground until FP reaches <paramref name="neededFp"/> (0 when it already has).</summary>
	public static long RestoreMillis(int currentFp, int neededFp, int maxFp)
	{
		int target = Math.Min(neededFp, maxFp);
		if (currentFp >= target) return 0;
		int ticks = (target - currentFp + FpRestoreAmount - 1) / FpRestoreAmount;
		return FpRestoreFirstMillis + (long)(ticks - 1) * FpRestorePeriodMillis;
	}

	/// <summary>The nearest landing target the FP left still reaches, or null when none does (the character will fall).</summary>
	public static NaturalLandingTarget? ChooseLanding(BotPosition from, IReadOnlyList<NaturalLandingTarget> targets, int currentFp,
		float speedMetersPerSecond) =>
		targets.Select(target => (Target: target, Meters: Distance(from, target.Position)))
			.Where(candidate => currentFp >= (int)MathF.Ceiling(candidate.Meters / speedMetersPerSecond))
			.OrderBy(candidate => candidate.Meters).Select(candidate => candidate.Target).FirstOrDefault();

	public static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
