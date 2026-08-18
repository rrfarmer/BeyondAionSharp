using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The coastal and sea lobseks of Beluslan (214215, 214216), which drop something strange when they are
/// hurt. Retail pattern <c>ND2_Xipeto_45</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Below half health, once, a strange object appears
/// beside it and lasts a minute.</b> Two metres away, sixty seconds, gone when the lobsek dies — the
/// third instance in three entries of retail's "sheds a piece when hurt" idiom, after the stoneskin
/// stoffu and the lich.
/// <para>
/// <b>Sixty seconds is short, and that is the whole of it.</b> The stoffu's fragments live six minutes
/// and the lich's servant fifty; a lobsek's object outlives the fight only if the fight is quick. It is
/// a nuisance with a clock rather than an add.
/// </para>
/// <para>
/// <b>Both provocations, one flag, and the asymmetry is retail's</b> — the melee branch has no
/// <c>is_enemy</c> guard and the caster branch does, exactly as the stoffu's do. Three encounters now
/// carry that asymmetry unchanged; it is the idiom rather than an accident of one pattern.
/// </para>
/// <para>
/// <b>Retail clears the group on being killed by a player and on being killed by an npc, and on
/// nothing else</b> — no leave-combat branch, unlike the stoffu. A lobsek that goes home leaves its
/// object standing for the rest of its minute. Translated as written.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. This pattern is complete.
/// </para>
/// </remarks>
[AIName("lobsek")]
public class LobsekAI : PatternAi
{
	/// <summary>Retail's <c>BLDF2A_XipetoSum_45_An</c> — the strange object.</summary>
	private const int StrangeObject = 280934;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, <c>spawn_range</c> and <c>live_time</c>.</summary>
	private const int Dropped = 1;
	private const float BesideIt = 2f;
	private const int OneMinute = 60;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, shared across both provocations.</summary>
	private const int Shed = 1;

	private const int Half = 50;

	private static PatternAction Drop() =>
		Do.SpawnNear(StrangeObject, Dropped, count: 1, range: BesideIt, liveSeconds: OneMinute);

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(7, "hit, below half", [When.HpBelow(Half), When.FirstTime(Shed)],
			Drop())),

		OnSpelled = Of(Branch(7, "cast at, below half",
			[When.HpBelow(Half), When.CasterIsEnemy, When.FirstTime(Shed)],
			Drop())),

		OnDie = Of(Branch(7, "and take it with it", [], Do.Despawn(Dropped))),
	};

	public LobsekAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
