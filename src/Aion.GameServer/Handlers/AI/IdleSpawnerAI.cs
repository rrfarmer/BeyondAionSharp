using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// An npc that wakes, waits, and places something — retail's plainest use of the idle timer.
/// </summary>
/// <remarks>
/// Twenty-one npcs across nineteen retail patterns, every one of which ran a generic class here and so
/// did nothing at all: arena summoners, Tiamat's hard-mode breath markers, world-raid wave pods and the
/// rest. <see cref="IdleSpawns"/> carries the numbers, because none of them is constant — the wait runs
/// two seconds to ten minutes, the placements one to eleven, and the re-arm is absent, zero, or a real
/// period.
/// <para>
/// <b>Bound by shape, not by encounter.</b> These are the patterns whose idle rung is a single
/// unguarded branch carrying nothing but spawns and the timer. The other 113 in the same family need
/// flag guards, several branches, or actions this port has no answer for, and are listed by
/// <c>tools/client-extract/audit_idle_spawns.py</c>.
/// </para>
/// </remarks>
[AIName("idle_spawner")]
public class IdleSpawnerAI : PatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public IdleSpawnerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id =>
		new AiPattern
		{
			OnWakeUp = AiPattern.Of(IdleSpawns.WakeRungFor(id)),
			OnIdleTimer = AiPattern.Of(IdleSpawns.PlaceRungFor(id)),
		});
}
