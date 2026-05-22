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
