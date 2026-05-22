using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerRouteService
{
	public WorldNpcWalkerRoutePlan ResolveRoute(
		WorldNpc npc,
		WalkerTemplateTable walkerTemplates,
		WalkerVersionTable walkerVersions)
	{
		// Java parity: model/gameobjects/Npc.isWalker gates WalkManager route setup from SpawnTemplate.walkerId.
		if (string.IsNullOrWhiteSpace(npc.WalkerId))
			return WorldNpcWalkerRoutePlan.None();

		var template = walkerTemplates.GetWalkerTemplate(npc.WalkerId);
		if (template == null)
			return WorldNpcWalkerRoutePlan.Missing(npc.WalkerId);

		// Java parity: model/templates/walker/WalkerTemplate.getVersionId delegates to WalkerVersionsData.
		return WorldNpcWalkerRoutePlan.Ready(
			template.RouteId,
			walkerVersions.GetRouteVersionId(template.RouteId) ?? string.Empty,
			template.Formation,
			template.Rows,
			template.RouteSteps);
	}
}

public sealed record WorldNpcWalkerRoutePlan(
	WorldNpcWalkerRouteStatus Status,
	string RouteId,
	string VersionRouteId,
	string Formation,
	IReadOnlyList<int> Rows,
	IReadOnlyList<WalkerRouteStepSummary> RouteSteps)
{
	public static WorldNpcWalkerRoutePlan None()
	{
		return new WorldNpcWalkerRoutePlan(
			WorldNpcWalkerRouteStatus.None,
			string.Empty,
			string.Empty,
			string.Empty,
			Array.Empty<int>(),
			Array.Empty<WalkerRouteStepSummary>());
	}

	public static WorldNpcWalkerRoutePlan Missing(string routeId)
	{
		return new WorldNpcWalkerRoutePlan(
			WorldNpcWalkerRouteStatus.MissingRoute,
			routeId,
			string.Empty,
			string.Empty,
			Array.Empty<int>(),
			Array.Empty<WalkerRouteStepSummary>());
	}

	public static WorldNpcWalkerRoutePlan Ready(
		string routeId,
		string versionRouteId,
		string formation,
		IReadOnlyList<int> rows,
		IReadOnlyList<WalkerRouteStepSummary> routeSteps)
	{
		return new WorldNpcWalkerRoutePlan(
			WorldNpcWalkerRouteStatus.Ready,
			routeId,
			versionRouteId,
			formation,
			rows,
			routeSteps);
	}
}

public enum WorldNpcWalkerRouteStatus
{
	None,
	MissingRoute,
	Ready,
}
