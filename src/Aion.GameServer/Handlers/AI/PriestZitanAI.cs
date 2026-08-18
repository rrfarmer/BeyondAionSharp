using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Priest Zitan (216512) of the Fire Temple. Retail pattern <c>IDTP_Fanatic_Boss_EL_ve40</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He was on plain <c>aggressive</c>, and his fight is
/// one thing done three times: <b>seven illusions of melancholy, and where they land is the mechanic.</b>
/// <list type="table">
/// <item><term>on engaging</term><description><b>three</b> at his own feet</description></item>
/// <item><term>the first blow under fifty</term><description><b>two more, on the player he is
/// fighting</b></description></item>
/// <item><term>the first blow under twenty-five</term><description><b>two more</b>, the same
/// way</description></item>
/// </list>
/// <para>
/// <b>The opening wave guards him and the later two chase.</b> Retail changes the placement rather than
/// the count: <c>SPAWN_LOCATION_MY_POINT</c> for the three that come with him, and
/// <c>spawn_on_target</c> for the four that come after. A class that put all seven in one place would
/// pass a head count and lose the fight.
/// </para>
/// <para>
/// <b>Both crossings are written twice and fire once.</b> Retail carries identical branches under
/// <c>on_attacked</c> and <c>on_spelled</c>, and both are behind the same flag var — so whichever kind
/// of blow lands first pays, and the other finds the flag gone. Our runtime raises the first of those
/// two events, and the flag makes the pair equivalent to it.
/// </para>
/// <para>
/// <b>Not translated, and one of them is a broadcast we deliberately do not send.</b> Each crossing
/// also broadcasts <c>6915</c> at fifteen metres naming his target, and the only listener — the
/// illusions themselves — answers with a bare <c>attack_most_hating</c> and no <c>add_hate_point</c>.
/// That cannot redirect anything: an illusion with an empty hate list has nobody to attack most, and
/// one already fighting is already doing it. The message is a kick into combat for NPCs that are
/// <c>aggressive</c> and do not need kicking, in either engine.
/// </para>
/// <para>
/// Also not translated: three skill indices on three cast timers that carry nothing else; seven
/// shouts; the death message; and <c>set_condition_spawn_variable FanaticElNBoss</c>, which is the
/// instance's own bookkeeping.
/// </para>
/// </remarks>
[AIName("priest_zitan")]
public class PriestZitanAI : PatternAi
{
	/// <summary><c>IDTemple_named_summon_basic</c> — an illusion of melancholy.</summary>
	private const int Illusion = 281524;

	/// <summary>Retail's <c>SPAWN_ID_3</c>, cleared on every one of his three exits.</summary>
	private const int Illusions = 3;

	/// <summary>Retail's <c>spawn_range</c>, the same five metres wherever the wave lands.</summary>
	private const float Ring = 5f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> and <c>ALPHA_2</c>.</summary>
	private const int Below50 = 1;
	private const int Below25 = 2;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(13, "three at his feet", When.Always,
				Do.SpawnNear(Illusion, Illusions, count: 3, range: Ring))),

		OnAttacked = Of(
			Branch(9, "and two on his quarry under fifty", [When.HpBelow(50), When.FirstTime(Below50)],
				Do.SpawnOnTarget(Illusion, Illusions, count: 2, range: Ring)),

			Branch(8, "and two more under twenty-five", [When.HpBelow(25), When.FirstTime(Below25)],
				Do.SpawnOnTarget(Illusion, Illusions, count: 2, range: Ring))),

		OnLeaveAttack = Of(
			Branch(15, "", When.Always,
				Do.Despawn(Illusions))),

		OnDie = Of(
			Branch(15, "", When.Always,
				Do.Despawn(Illusions))),
	};

	public PriestZitanAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
