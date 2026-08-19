using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The gravity bomb's damage twin (283142 normal, 856047 hard), which is a metronome.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its entire pattern is three lines:
/// <code>
/// on_wake_up:    set_idle_timer 1000
/// on_idle_timer: broadcast_message 204 range_as_meter=1 param_obj=OBJI_SELF
///                set_idle_timer 3000
/// </code>
/// <para>
/// <b>It is not an add and it does no damage of its own.</b> It exists to beat time: one second after
/// it appears, and every three seconds after that, it tells whatever is standing on it to cast. The
/// range is <b>one metre</b>, so the only thing that can hear it is the tornado that spawned it on its
/// own mark.
/// </para>
/// <para>
/// <b>Both twins ran plain <c>aggressive</c></b>, so nothing sent 204 — which is what
/// <see cref="GravityTornadoAI"/>'s remark said, and why that class drove its cast from a timer of its
/// own instead. The timer ran at six seconds where retail's beat is three.
/// </para>
/// <para>
/// Not to be confused with <c>gravity_crusher</c> (283141), the visible bomb that walks at a player.
/// Retail names them <c>IDTiamat_Tiamat_GravityBomb</c> and <c>..._Dmg</c>; only the second is here.
/// </para>
/// </remarks>
[AIName("gravity_bomb_damage")]
public class GravityBombDamageAI : PatternAi
{
	/// <summary>Retail's <c>message_type=204</c>: "cast now".</summary>
	public const int CastNow = 204;

	/// <summary>Retail's <c>range_as_meter=1</c>. It is meant to reach one thing.</summary>
	public const float Reach = 1f;

	private const int FirstBeat = 1000;
	private const int Interval = 3000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(
			Branch(2, "start the beat", When.Always,
				Do.SetIdleTimer(FirstBeat))),

		OnIdleTimer = Of(
			Branch(1, "beat, and keep beating", When.Always,
				Do.Broadcast(CastNow, Reach),
				Do.SetIdleTimer(Interval))),
	};

	public GravityBombDamageAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
