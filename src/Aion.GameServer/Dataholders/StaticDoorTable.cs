using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class StaticDoorTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<StaticDoorSummary>> _doorsByWorldId;
	private readonly IReadOnlyDictionary<StaticDoorKey, StaticDoorSummary> _doorsByWorldAndStaticId;

	public StaticDoorTable(IReadOnlyList<StaticDoorSummary> doors)
	{
		Doors = doors;
		_doorsByWorldId = new ReadOnlyDictionary<int, IReadOnlyList<StaticDoorSummary>>(
			doors
				.GroupBy(door => door.WorldId)
				.ToDictionary(
					group => group.Key,
					group => (IReadOnlyList<StaticDoorSummary>)group.ToArray()));
		_doorsByWorldAndStaticId = new ReadOnlyDictionary<StaticDoorKey, StaticDoorSummary>(
			doors.ToDictionary(door => new StaticDoorKey(door.WorldId, door.StaticId)));
	}

	public IReadOnlyList<StaticDoorSummary> Doors { get; }

	public int Count => Doors.Count;

	public IReadOnlyList<StaticDoorSummary> GetStaticDoors(int worldId)
	{
		return _doorsByWorldId.GetValueOrDefault(worldId) ?? Array.Empty<StaticDoorSummary>();
	}

	public StaticDoorSummary? GetStaticDoor(int worldId, int staticId)
	{
		return _doorsByWorldAndStaticId.GetValueOrDefault(new StaticDoorKey(worldId, staticId));
	}

	private readonly record struct StaticDoorKey(int WorldId, int StaticId);
}

public sealed record StaticDoorSummary(
	int WorldId,
	int StaticId,
	int KeyId,
	float X,
	float Y,
	float Z,
	int State)
{
	public const int TemplateId = 300001;
	public const int OpenedFlag = 1 << 0;

	public bool IsOpen => (State & OpenedFlag) == OpenedFlag;
}
