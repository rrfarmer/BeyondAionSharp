using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class WalkerTemplateTable
{
	private readonly IReadOnlyDictionary<string, WalkerTemplateSummary> _templatesByRouteId;

	public WalkerTemplateTable(IReadOnlyList<WalkerTemplateSummary> templates)
	{
		Templates = templates;
		_templatesByRouteId = new ReadOnlyDictionary<string, WalkerTemplateSummary>(
			templates
				.GroupBy(template => template.RouteId, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
	}

	public IReadOnlyList<WalkerTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public WalkerTemplateSummary? GetWalkerTemplate(string routeId)
	{
		return _templatesByRouteId.GetValueOrDefault(routeId);
	}
}

public sealed record WalkerTemplateSummary(
	string RouteId,
	int Pool,
	string Formation,
	string LoopType,
	IReadOnlyList<int> Rows,
	IReadOnlyList<WalkerRouteStepSummary> RouteSteps);

public sealed record WalkerRouteStepSummary(
	float X,
	float Y,
	float Z,
	int RestTime,
	int StepIndex,
	bool IsLastStep);

public sealed class WalkerVersionTable
{
	private readonly IReadOnlyDictionary<string, string> _parentRouteIdsByVersionId;

	public WalkerVersionTable(IReadOnlyDictionary<string, string> parentRouteIdsByVersionId)
	{
		_parentRouteIdsByVersionId = parentRouteIdsByVersionId;
	}

	public int Count => _parentRouteIdsByVersionId.Count;

	public bool IsRouteVersioned(string? routeId)
	{
		return routeId != null && _parentRouteIdsByVersionId.ContainsKey(routeId);
	}

	public string? GetRouteVersionId(string? routeId)
	{
		return routeId == null ? null : _parentRouteIdsByVersionId.GetValueOrDefault(routeId);
	}
}
