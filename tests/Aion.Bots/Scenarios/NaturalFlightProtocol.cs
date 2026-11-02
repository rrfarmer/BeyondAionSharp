using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>A planned flight route and why it was or was not accepted.</summary>
public sealed record NaturalFlightRoute(IReadOnlyList<BotPosition> Waypoints, float Meters, string? Refusal)
{
	public bool IsUsable => Refusal == null && Waypoints.Count > 0;
}

/// <summary>
/// AF-05 (docs/natural-altgard-leveling.md): the client side of free flight. Take off with <c>CM_EMOTION</c> FLY, fly with
/// <c>CM_MOVE_IN_AIR</c> samples (the E2E plan's flight packet), land with <c>CM_EMOTION</c> LAND on the ground or a platform,
/// and read the flight time from <c>SM_FLY_TIME</c>. Routes climb straight up, cross at a cruise height and come straight
/// down, each leg in line of sight on the server's geometry; <see cref="NaturalFlightPolicy"/> decides whether to go.
/// </summary>
public static class NaturalFlightProtocol
{
	/// <summary>Frames per second of flight movement, as the flight-path scenarios sample it.</summary>
	public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);

	/// <summary>Grid spacing and reach of the climb-column search, and the heights tried for the low leg.</summary>
	public const float ColumnSpacing = 10f, ColumnReach = 100f;
	private static readonly float[] LowHeights = [3f, 10f, 20f, 35f];

	/// <summary>
	/// Climb, cross and descend from a takeoff point to a landing point, cruising at <paramref name="cruiseZ"/>. When the
	/// straight climb or descent is blocked (the floating island over Altgard Fortress), the route flies low to a clear
	/// column beside the low end, climbs or descends there, and crosses at cruise height. The shortest clear route wins.
	/// </summary>
	public static NaturalFlightRoute Plan(BotNavigationGeometry geometry, int mapId, BotPosition start, BotPosition end, float cruiseZ)
	{
		float cruise = MathF.Max(cruiseZ, MathF.Max(start.Z, end.Z));
		// Air to air (or any clear line): fly straight.
		NaturalFlightRoute straight = Check(geometry, mapId, start, [end]);
		if (straight.IsUsable) return straight;
		NaturalFlightRoute direct = Check(geometry, mapId, start, [start with { Z = cruise }, end with { Z = cruise }, end]);
		if (direct.IsUsable) return direct;
		bool startIsLow = start.Z <= end.Z;
		NaturalFlightRoute? best = Columns(startIsLow);
		// M6: a high start under rock (a ledge in Morheim's crater, the climb out shut all the way to the zone's ceiling) flies low
		// to a clear column beside it as well; only when the usual columns find nothing, so a route found before is unchanged.
		best ??= Columns(!startIsLow);
		return best ?? direct;

		// Columns beside the start (fly low to it, climb, cross) or beside the end (cross, descend, fly low to the end).
		NaturalFlightRoute? Columns(bool besideStart)
		{
			BotPosition low = besideStart ? start : end;
			NaturalFlightRoute? found = null;
			for (float dx = -ColumnReach; dx <= ColumnReach; dx += ColumnSpacing)
				for (float dy = -ColumnReach; dy <= ColumnReach; dy += ColumnSpacing)
					foreach (float height in LowHeights)
					{
						var column = new BotPosition(low.X + dx, low.Y + dy, low.Z + height, 0);
						BotPosition[] waypoints = besideStart
							? [start with { Z = column.Z }, column, column with { Z = cruise }, end with { Z = cruise }, end]
							: [start with { Z = cruise }, column with { Z = cruise }, column, end with { Z = column.Z }, end];
						float meters = Length(start, waypoints);
						if (found != null && meters >= found.Meters) continue;
						NaturalFlightRoute candidate = Check(geometry, mapId, start, waypoints);
						if (candidate.IsUsable) found = candidate;
					}
			return found;
		}
	}

	/// <summary>NL-3: the cheap part of <see cref="Plan"/>: a straight flight, or the climb, the crossing at cruise height and the
	/// descent, is clear. For choosing among many takeoffs; the chosen one is planned in full.</summary>
	public static bool DirectlyClear(BotNavigationGeometry geometry, int mapId, BotPosition start, BotPosition end, float cruiseZ)
	{
		float cruise = MathF.Max(cruiseZ, MathF.Max(start.Z, end.Z));
		return IsClear(geometry, mapId, start, end) || IsClear(geometry, mapId, start, start with { Z = cruise }) &&
			IsClear(geometry, mapId, start with { Z = cruise }, end with { Z = cruise }) && IsClear(geometry, mapId, end with { Z = cruise }, end);
	}

	private static NaturalFlightRoute Check(BotNavigationGeometry geometry, int mapId, BotPosition start, BotPosition[] waypoints)
	{
		BotPosition previous = start;
		foreach (BotPosition point in waypoints)
		{
			if (!IsClear(geometry, mapId, previous, point))
				return new([], 0, $"Leg ({previous.X:F0}, {previous.Y:F0}, {previous.Z:F0}) -> ({point.X:F0}, {point.Y:F0}, {point.Z:F0}) is blocked.");
			previous = point;
		}
		return new(waypoints, Length(start, waypoints), null);
	}

	private static float Length(BotPosition start, IReadOnlyList<BotPosition> waypoints)
	{
		float meters = 0;
		BotPosition previous = start;
		foreach (BotPosition point in waypoints)
		{
			meters += NaturalFlightPolicy.Distance(previous, point);
			previous = point;
		}
		return meters;
	}

	/// <summary>The server's sight check refuses rays over 80 m, so a leg is checked in pieces of at most this length.</summary>
	public const float SightPiece = 40f;

	/// <summary>No collision along the leg, checked piece by piece.</summary>
	public static bool IsClear(BotNavigationGeometry geometry, int mapId, BotPosition a, BotPosition b)
	{
		float meters = NaturalFlightPolicy.Distance(a, b);
		int pieces = Math.Max(1, (int)MathF.Ceiling(meters / SightPiece));
		BotPosition previous = a;
		for (int i = 1; i <= pieces; i++)
		{
			float t = (float)i / pieces;
			var next = new BotPosition(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, b.Heading);
			if (NaturalFlightPolicy.Distance(previous, next) > 0.01f && !geometry.HasLineOfSight(mapId, previous, next))
				return false;
			previous = next;
		}
		return true;
	}

	public static NaturalFlightPlan ToPlan(NaturalFlightRoute route, BotPosition start, float speedMetersPerSecond, int airborneWorkSeconds = 0)
	{
		var legs = new List<float>();
		BotPosition previous = start;
		foreach (BotPosition point in route.Waypoints)
		{
			legs.Add(NaturalFlightPolicy.Distance(previous, point));
			previous = point;
		}
		return new(route.Waypoints, legs, speedMetersPerSecond, airborneWorkSeconds);
	}

	/// <summary>Take off; returns the flight speed the server announced with the FLY emotion.</summary>
	public static async Task<float> TakeOffAsync(INaturalJourneySession session, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.Fly(), token);
		// The server's refusals end the wait instead of the watchdog (M6: STR_FLYING_FORBIDDEN_HERE at Slag Bulwark).
		DecodedBotServerPacket flying = await session.WaitForPacketAsync(packet =>
			packet.PacketType == typeof(SM_EMOTION) && packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.FLY ||
			packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<object>("name") is "STR_FLYING_FORBIDDEN_HERE" or "STR_FLYING_FORBIDDEN_ZONE" or
				"STR_FLYING_TIME_NOT_READY" or "STR_CANT_FLY_NOW_DUE_TO_NOFLY" or "STR_FLYING_DISABLE_1" or "STR_FLYING_DISABLE_2", token);
		if (flying.PacketType == typeof(SM_SYSTEM_MESSAGE)) throw new IOException($"Takeoff refused: {flying.Get<object>("name")}.");
		float speed = flying.Get<float>("movementSpeed");
		return speed > 0 ? speed : session.Api.World.MovementSpeed ?? throw new InvalidDataException("The FLY emotion carried no speed.");
	}

	/// <summary>Fly the waypoints at <paramref name="speed"/>, one <c>CM_MOVE_IN_AIR</c> sample every half second.</summary>
	/// <param name="betweenPieces">NL-3: when given, each leg is flown in pieces of at most four seconds and this runs between
	/// them (a Wind Serum drunk on the move once its full restore fits).</param>
	public static async Task FlyAsync(INaturalJourneySession session, int mapId, BotPosition start, IReadOnlyList<BotPosition> waypoints,
		float speed, CancellationToken token, Func<Task>? betweenPieces = null)
	{
		BotPosition current = start;
		foreach (BotPosition point in waypoints)
		{
			float meters = NaturalFlightPolicy.Distance(current, point);
			if (meters <= 0.01f) continue;
			int pieces = betweenPieces == null ? 1 : Math.Max(1, (int)MathF.Ceiling(meters / speed / 4f));
			for (int piece = 1; piece <= pieces; piece++)
			{
				float share = (float)piece / pieces;
				BotPosition next = piece == pieces ? point : new BotPosition(current.X + (point.X - current.X) / (pieces - piece + 1),
					current.Y + (point.Y - current.Y) / (pieces - piece + 1), current.Z + (point.Z - current.Z) / (pieces - piece + 1), point.Heading);
				TimeSpan duration = TimeSpan.FromSeconds(NaturalFlightPolicy.Distance(current, next) / speed);
				if (duration < SampleInterval) duration = SampleInterval;
				await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(current, next, mapId, duration), token);
				current = next;
				if (betweenPieces != null && share < 1) await betweenPieces();
			}
		}
	}

	/// <summary>Fall from where the bot hangs, the flight ended, to <paramref name="groundZ"/>: the client reports it with
	/// CM_MOVE's FALL flag, a sample every 250 ms under gravity, and then the stop on the ground. Java's PlayerMoveController
	/// measures from where the fall started to where it stops and deals the damage there: none under 10 m, 1% of max HP per
	/// meter plus 4 above it, and death from 50 m (upstream 82a5e0cf9; a sprinting player takes none). A fall that begins
	/// right after a teleport or a flight path deals nothing, because those forget where the fall started.</summary>
	public static async Task FallAsync(INaturalJourneySession session, BotPosition top, float groundZ, CancellationToken token)
	{
		if (groundZ >= top.Z) throw new ArgumentOutOfRangeException(nameof(groundZ));
		const float gravity = 9.8f, terminal = 40f;
		TimeSpan step = TimeSpan.FromMilliseconds(250);
		byte falling = (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE | MovementMask.FALL);
		var frames = new List<BotMovementFrame>
		{
			new(TimeSpan.Zero, GameClientPackets.Move(new MovementPacketData(top.X, top.Y, top.Z, top.Heading, falling)), top),
		};
		float z = top.Z, speed = 0;
		TimeSpan total = TimeSpan.Zero;
		while (z > groundZ)
		{
			speed = Math.Min(terminal, speed + gravity * (float)step.TotalSeconds);
			z = Math.Max(groundZ, z - speed * (float)step.TotalSeconds);
			BotPosition at = top with { Z = z };
			total += step;
			frames.Add(new(step, GameClientPackets.Move(new MovementPacketData(at.X, at.Y, at.Z, at.Heading, falling)), at));
		}
		BotPosition ground = top with { Z = groundZ };
		frames.Add(new(TimeSpan.Zero, GameClientPackets.Move(new MovementPacketData(ground.X, ground.Y, ground.Z, ground.Heading, 0)), ground));
		await session.ExecuteMovementAsync(new BotMovementPlan(frames, total, top.Z - groundZ), token);
	}

	/// <summary>Land where the bot hovers; the server ends the flight and starts restoring flight time.</summary>
	public static async Task LandAsync(INaturalJourneySession session, CancellationToken token)
	{
		// NL-3: a bot killed in the air (the Templar and the Chanter, by upper Reshanta's monsters on a hop) never lands;
		// its death ends the wait, and the revive takes over.
		if (session.Api.World.IsDead) return;
		await session.SendPacketAsync(session.Api.Land(), token);
		await session.WaitForPacketAsync(packet =>
			packet.PacketType == typeof(SM_EMOTION) && packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.LAND ||
			packet.PacketType == typeof(SM_DIE), token);
	}
}
