using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Panesterra's soul anchor barriers: ten minutes after they wake, each places a faction-balance npc
/// on itself for ten seconds.
/// </summary>
/// <remarks>
/// Twenty of these run <c>Gab1_0*_Guard_Noshow_0*</c> in retail and every one of them was on plain
/// <c>aggressive</c> here, which does nothing with a timer.
/// <para>
/// <b>It is not a chain, which is what the log thought.</b> All five spawning patterns place the
/// <em>same</em> npc — 702412 — and that npc's own pattern has no idle timer, so nothing follows it.
/// One wake, one wait, one spawn.
/// </para>
/// <para>
/// The rung ends <c>set_idle_timer delay=0</c>, and this class is the first in the port to depend on
/// what that means. It is <b>stop</b>: the barrier places its npc once and disarms. See
/// <see cref="PatternAi.SetIdleTimer"/> for the evidence, and <c>IdleTimerSemanticsTests</c> for the
/// pins. Read the other way, every barrier in Panesterra would spawn one of these per tick forever.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>use_skill</c> on the same wake-up rung, which is a skill index, and
/// the <c>on_attacked</c> / <c>on_most_hating_updated</c> rungs, which display a system message when a
/// rebirth door is struck — a string id this port has no table for.
/// </para>
/// </remarks>
[AIName("soul_anchor_barrier")]
public class SoulAnchorBarrierAI : PatternAi
{
	/// <summary>Retail's <c>delay</c> on the wake-up rung: ten minutes.</summary>
	private const int TenMinutes = 600_000;

	/// <summary>Retail's <c>live_time</c> on the spawn.</summary>
	private const int TenSeconds = 10;

	/// <summary>The faction-balance npc every one of the five patterns places.</summary>
	private const int FactionBalance = 702412;

	/// <summary>Retail's <c>SPAWN_ID_NONE</c>: the barrier does not track what it placed.</summary>
	private const int Untracked = 0;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = AiPattern.Of(
			AiPattern.Branch(7, "start the ten minutes", When.Always,
				Do.SetIdleTimer(TenMinutes))),

		OnIdleTimer = AiPattern.Of(
			AiPattern.Branch(7, "place the balance npc, and stop", When.Always,
				// SPAWN_LOCATION_MY_POINT with spawn_range=5.
				Do.SpawnNear(FactionBalance, Untracked, count: 1, range: 5f, liveSeconds: TenSeconds),
				Do.SetIdleTimer(0))),
	};

	public SoulAnchorBarrierAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
