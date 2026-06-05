using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class PetDopingTable
{
	private readonly IReadOnlyDictionary<int, PetDopingEntrySummary> _dopingsById;

	public PetDopingTable(IReadOnlyList<PetDopingEntrySummary> dopings)
	{
		Dopings = dopings;
		_dopingsById = new ReadOnlyDictionary<int, PetDopingEntrySummary>(
			dopings.ToDictionary(doping => doping.Id));
	}

	public IReadOnlyList<PetDopingEntrySummary> Dopings { get; }

	public int Count => Dopings.Count;

	public PetDopingEntrySummary? GetDopingTemplate(int id)
	{
		// Java parity: dataholders/PetDopingData.getDopingTemplate.
		return _dopingsById.TryGetValue(id, out var doping) ? doping : null;
	}
}

public sealed record PetDopingEntrySummary(
	int Id,
	bool UseDrink,
	bool UseFood,
	int ScrollsUsed);
