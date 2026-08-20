using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// A Tiamat breath beacon: the marker a raid runs out of, and the damage it lays two seconds later.
/// </summary>
/// <remarks>
/// <b>This port had the warning and not the breath.</b> Tiamat's rotation places the beacons — that half
/// has worked since the rotation was ported — and in retail each beacon then arms a 2000ms idle timer
/// and spawns its own <c>_dmg</c> twin along the line it marked. Fifteen beacons exist here and twelve
/// were on plain <c>aggressive</c>, which does nothing at all; the other three had a class that casts a
/// skill and never spawns. So every breath in the encounter landed harmlessly.
/// <para>
/// The rungs come from <see cref="TiamatBeacons"/> because the placement is per beacon: a middle beacon
/// lays <b>eleven</b> hits in an absolute line and lives two seconds, while the left and right ones lay
/// a single hit <c>SPAWN_LOCATION_MY_POINT</c> — on the marker itself — and live three.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>use_skill</c> that four of these patterns fire on the same wake-up rung,
/// which is a skill index. <see cref="UltimateAtrocityAI"/> already casts for the two beacons it owns,
/// and those two keep that class; this one is for the twelve that had nothing.
/// </para>
/// </remarks>
[AIName("tiamat_beacon")]
public class TiamatBeaconAI : PatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public TiamatBeaconAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id =>
		new AiPattern
		{
			OnWakeUp = AiPattern.Of(TiamatBeacons.WakeRungFor(id)),
			OnIdleTimer = AiPattern.Of(TiamatBeacons.BreathRungFor(id)),
		});
}
