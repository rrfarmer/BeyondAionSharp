using System.Xml.Linq;

namespace Aion.GameServer.Dataholders;

public sealed class RiftScheduleTable
{
	public RiftScheduleTable(IReadOnlyList<RiftScheduleEntry> entries)
	{
		Entries = entries;
	}

	public IReadOnlyList<RiftScheduleEntry> Entries { get; }

	public int Count => Entries.Count;

	public static RiftScheduleTable LoadFromFile(string path)
	{
		// Java parity: configs/schedule/RiftSchedule.load deserializes config/schedule/rift_schedule.xml.
		var document = XDocument.Load(path);
		var entries = document.Root?
			.Elements("rift")
			.SelectMany(
				rift =>
				{
					var worldId = (int?)rift.Attribute("id") ?? 0;
					return rift.Elements("open")
						.Select(open => new RiftScheduleEntry(
							worldId,
							(string?)open.Attribute("schedule") ?? string.Empty,
							(bool?)open.Attribute("spawn") ?? false));
				})
			.Where(entry => entry.WorldId > 0 && !string.IsNullOrWhiteSpace(entry.ScheduleExpression))
			.ToArray()
			?? Array.Empty<RiftScheduleEntry>();
		return new RiftScheduleTable(entries);
	}
}

public sealed record RiftScheduleEntry(
	int WorldId,
	string ScheduleExpression,
	bool SpawnGuards);
