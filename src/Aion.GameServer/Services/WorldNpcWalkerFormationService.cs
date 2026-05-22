using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerFormationService
{
	private const float Distance = 2f;
	private const float TriangleRowDistanceFactor = 0.86602540378443864676372317075294f;

	public WorldNpcWalkerFormationResult FormSquareGroup(
		IReadOnlyList<WorldNpc> npcs,
		WorldNpcWalkerRoutePlan routePlan)
	{
		// Java parity: spawnengine/WalkerGroup.form handles SQUARE clusters after InstanceWalkerFormations groups same-position candidates.
		if (npcs.Count == 0)
			return WorldNpcWalkerFormationResult.Empty(routePlan.RouteId, routePlan.VersionRouteId);
		if (routePlan.Status != WorldNpcWalkerRouteStatus.Ready)
			return WorldNpcWalkerFormationResult.Unchanged(routePlan.RouteId, routePlan.VersionRouteId, WorldNpcWalkerFormationStatus.MissingRoute, npcs);
		if (!string.Equals(routePlan.Formation, "SQUARE", StringComparison.Ordinal))
			return WorldNpcWalkerFormationResult.Unchanged(routePlan.RouteId, routePlan.VersionRouteId, WorldNpcWalkerFormationStatus.PointFormation, npcs);
		if (routePlan.RouteSteps.Count < 2)
			return WorldNpcWalkerFormationResult.Unchanged(routePlan.RouteId, routePlan.VersionRouteId, WorldNpcWalkerFormationStatus.InsufficientRoute, npcs);
		if (routePlan.Rows.Count == 0)
			return WorldNpcWalkerFormationResult.Unchanged(routePlan.RouteId, routePlan.VersionRouteId, WorldNpcWalkerFormationStatus.PointFormation, npcs);

		var members = npcs
			.OrderByDescending(npc => npc.WalkerIndex)
			.ToArray();
		var origin = new WalkerPoint(members[0].Position.X, members[0].Position.Y);
		var destination = new WalkerPoint(routePlan.RouteSteps[1].X, routePlan.RouteSteps[1].Y);
		var formedMembers = routePlan.Rows.Count == 1
			? FormLine(members, origin, destination)
			: FormRows(members, routePlan.Rows, origin, destination);
		return new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Ready,
			routePlan.RouteId,
			routePlan.VersionRouteId,
			formedMembers);
	}

	public static WalkerPoint GetLinePoint(WalkerPoint origin, WalkerPoint destination, WorldNpcWalkerShift shift)
	{
		// Java parity: spawnengine/WalkerGroup.getLinePoint keeps this projection math, including its TODO around angle shift.
		var direction = GetShiftSigns(origin, destination);
		if (origin.Y - destination.Y == 0)
		{
			return new WalkerPoint(
				origin.X + direction.CoronalShift * shift.CoronalShift,
				origin.Y - direction.SagittalShift * shift.SagittalShift);
		}

		if (origin.X - destination.X == 0)
		{
			return new WalkerPoint(
				origin.X + direction.CoronalShift * shift.SagittalShift,
				origin.Y + direction.CoronalShift * shift.CoronalShift);
		}

		var slope = (origin.X - destination.X) / (double)(origin.Y - destination.Y);
		var projectedX = Math.Abs(shift.SagittalShift) / Math.Sqrt(1 + slope * slope);
		var result = shift.SagittalShift * direction.CoronalShift < 0
			? new WalkerPoint((float)(origin.X - projectedX), (float)(origin.Y + projectedX * slope))
			: new WalkerPoint((float)(origin.X + projectedX), (float)(origin.Y - projectedX * slope));

		if (shift.CoronalShift == 0)
			return result;

		var rotatedShift = shift.SagittalShift != 0
			? GetLinePoint(
				origin,
				destination,
				new WorldNpcWalkerShift(MathF.Sign(shift.SagittalShift) * Math.Abs(shift.CoronalShift), 0))
			: GetLinePoint(origin, destination, new WorldNpcWalkerShift(Math.Abs(shift.CoronalShift), 0));
		var dx = Math.Abs(origin.X - rotatedShift.X);
		var dy = Math.Abs(origin.Y - rotatedShift.Y);
		if (shift.CoronalShift < 0)
		{
			if (direction.SagittalShift < 0 && direction.CoronalShift < 0)
				return new WalkerPoint(result.X + dy, result.Y + dx);
			if (direction.SagittalShift > 0 && direction.CoronalShift > 0)
				return new WalkerPoint(result.X - dy, result.Y - dx);
			if (direction.SagittalShift < 0 && direction.CoronalShift > 0)
				return new WalkerPoint(result.X + dy, result.Y - dx);
			if (direction.SagittalShift > 0 && direction.CoronalShift < 0)
				return new WalkerPoint(result.X - dy, result.Y + dx);
		}
		else
		{
			if (direction.SagittalShift < 0 && direction.CoronalShift < 0)
				return new WalkerPoint(result.X - dy, result.Y - dx);
			if (direction.SagittalShift > 0 && direction.CoronalShift > 0)
				return new WalkerPoint(result.X + dy, result.Y + dx);
			if (direction.SagittalShift < 0 && direction.CoronalShift > 0)
				return new WalkerPoint(result.X - dy, result.Y + dx);
			if (direction.SagittalShift > 0 && direction.CoronalShift < 0)
				return new WalkerPoint(result.X + dy, result.Y - dx);
		}

		return result;
	}

	private static IReadOnlyList<WorldNpcWalkerFormationMember> FormLine(
		IReadOnlyList<WorldNpc> members,
		WalkerPoint origin,
		WalkerPoint destination)
	{
		var result = new List<WorldNpcWalkerFormationMember>(members.Count);
		var bounds = members.Sum(npc => npc.Template.BoundRadius);
		var distance = (1 - members.Count) / 2f * (Distance + bounds);
		for (var i = 0; i < members.Count; i++, distance += Distance)
		{
			var shift = new WorldNpcWalkerShift(distance, 0);
			var location = GetLinePoint(origin, destination, shift);
			result.Add(WorldNpcWalkerFormationMember.FromNpc(members[i], shift, location));
		}

		return result;
	}

	private static IReadOnlyList<WorldNpcWalkerFormationMember> FormRows(
		IReadOnlyList<WorldNpc> members,
		IReadOnlyList<int> rows,
		WalkerPoint origin,
		WalkerPoint destination)
	{
		var result = new List<WorldNpcWalkerFormationMember>(members.Count);
		var rowDistances = new float[Math.Max(0, rows.Count - 1)];
		var coronalDistance = 0f;
		for (var i = 0; i < rows.Count - 1; i++)
		{
			rowDistances[i] = rows[i] % 2 != rows[i + 1] % 2
				? TriangleRowDistanceFactor * Distance
				: Distance;
			coronalDistance -= rowDistances[i];
		}

		var index = 0;
		for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
		{
			var sagittalDistance = (1 - rows[rowIndex]) / 2f * Distance;
			for (var column = 0; column < rows[rowIndex]; column++, sagittalDistance += Distance)
			{
				if (index > members.Count - 1)
					break;

				var shift = new WorldNpcWalkerShift(sagittalDistance, coronalDistance);
				var location = GetLinePoint(origin, destination, shift);
				result.Add(WorldNpcWalkerFormationMember.FromNpc(members[index++], shift, location));
			}

			if (rowIndex < rows.Count - 1)
				coronalDistance += rowDistances[rowIndex];
		}

		return result;
	}

	private static WorldNpcWalkerShift GetShiftSigns(WalkerPoint origin, WalkerPoint destination)
	{
		return new WorldNpcWalkerShift(
			MathF.Sign(destination.X - origin.X),
			MathF.Sign(destination.Y - origin.Y));
	}
}

public sealed record WorldNpcWalkerFormationResult(
	WorldNpcWalkerFormationStatus Status,
	string RouteId,
	string VersionRouteId,
	IReadOnlyList<WorldNpcWalkerFormationMember> Members)
{
	public static WorldNpcWalkerFormationResult Empty(string routeId, string versionRouteId = "")
	{
		return new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Empty,
			routeId,
			versionRouteId,
			Array.Empty<WorldNpcWalkerFormationMember>());
	}

	public static WorldNpcWalkerFormationResult Unchanged(
		string routeId,
		string versionRouteId,
		WorldNpcWalkerFormationStatus status,
		IReadOnlyList<WorldNpc> npcs)
	{
		return new WorldNpcWalkerFormationResult(
			status,
			routeId,
			versionRouteId,
			npcs.Select(npc => WorldNpcWalkerFormationMember.FromNpc(npc, new WorldNpcWalkerShift(0, 0), new WalkerPoint(npc.Position.X, npc.Position.Y))).ToArray());
	}
}

public sealed record WorldNpcWalkerFormationMember(
	int ObjectId,
	int TemplateId,
	int WalkerIndex,
	float X,
	float Y,
	float SagittalShift,
	float CoronalShift)
{
	public static WorldNpcWalkerFormationMember FromNpc(WorldNpc npc, WorldNpcWalkerShift shift, WalkerPoint location)
	{
		return new WorldNpcWalkerFormationMember(
			npc.ObjectId,
			npc.TemplateId,
			npc.WalkerIndex,
			location.X,
			location.Y,
			shift.SagittalShift,
			shift.CoronalShift);
	}
}

public readonly record struct WalkerPoint(float X, float Y);

public readonly record struct WorldNpcWalkerShift(float SagittalShift, float CoronalShift);

public enum WorldNpcWalkerFormationStatus
{
	Empty,
	MissingRoute,
	PointFormation,
	InsufficientRoute,
	Ready,
}
