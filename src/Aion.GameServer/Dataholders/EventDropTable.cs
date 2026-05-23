namespace Aion.GameServer.Dataholders;

public sealed class EventDropTable
{
	public EventDropTable(IReadOnlyList<EventTemplateSummary> events)
	{
		Events = events;
	}

	public IReadOnlyList<EventTemplateSummary> Events { get; }

	public int Count => Events.Count;

	public IReadOnlyList<GlobalDropRuleSummary> GetActiveDropRules(
		DateTime now,
		IReadOnlySet<string>? disabledEventNames = null)
	{
		// Java parity: services/event/EventService.collectActiveEvents + collectDropRules.
		if (disabledEventNames is { Count: 1 } && disabledEventNames.Contains("*"))
			return Array.Empty<GlobalDropRuleSummary>();

		var disabled = disabledEventNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		return Events
			.Where(template => !disabled.Contains(template.Name) && template.IsActive(now))
			.SelectMany(template => template.DropRules)
			.ToArray();
	}
}

public sealed record EventTemplateSummary(
	string Name,
	DateTime? StartDate,
	DateTime? EndDate,
	string Theme,
	IReadOnlyList<GlobalDropRuleSummary> DropRules)
{
	public bool IsActive(DateTime now)
	{
		// Java parity: model/templates/event/EventTemplate.isInEventPeriod.
		return (StartDate == null || now >= StartDate.Value)
			&& (EndDate == null || now < EndDate.Value);
	}
}
