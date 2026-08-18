using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The village killers of Cygnea and Enshar — raiding parties that hunt a settlement's garrison rather
/// than its visitors. Retail patterns <c>LDF5_Village_Killer01_L</c>, <c>_01_D</c>, <c>_01_DR</c> and
/// the identical <c>_02_*</c> set.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The moment one sees a garrison chief of a faction
/// it is at war with, it commits to it with five million hate points.</b> Five million is not a weight,
/// it is a statement: nothing a player can do will peel a raiding party off the garrison it came for.
/// The same rule runs on being attacked.
/// <para>
/// <b>The suffix is the killer's own side, and each hunts the other two.</b> <c>_L</c> hunts
/// <c>gchief_dark</c> and <c>gchief_dragon</c>; <c>_D</c> hunts <c>gchief_dragon</c> and
/// <c>gchief_light</c>; <c>_DR</c> hunts <c>gchief_dark</c> and <c>gchief_light</c>. <b>No killer ever
/// hunts its own faction's garrison</b> — the three lists are exactly "everyone but me", and <c>01</c>
/// and <c>02</c> are two village sets with identical rules.
/// </para>
/// <para>
/// <b>That fact settled an open question from the previous commit.</b> This shipped first as two classes
/// keyed on the village number rather than three keyed on the faction, which handed a Balaur raider a
/// Balaur garrison to hunt. The aggro list refused the hate — correctly, they are friends — and the
/// refusal was written up as a possible disagreement between retail's data and our tribe table. There
/// was no disagreement. <b>The tribe table was right and the class was wrong</b>, and the
/// <c>on_attacked</c> half that was deferred over it was deferred for a bug rather than a gap.
/// </para>
/// <para>
/// <b>Not translated:</b> <c>on_spelled</c>, which retail carries with the same body. Our engine has no
/// pattern handler for it, so a caster garrison that never lands a melee blow is not committed to.
/// Recorded rather than approximated with <c>on_attacked</c>, which fires on a different event.
/// </para>
/// </remarks>
public abstract class VillageKillerAI : PatternAi
{
	/// <summary>Retail's <c>points_to_add</c> on every branch of all six patterns.</summary>
	private const int Unpeelable = 5_000_000;

	/// <summary>Retail's <c>FLAGVARI_EPSILON_5</c>, set on the attacked branches.</summary>
	private const int Committed = 5;

	protected static AiPattern Build(params Race[] hunted) => new AiPattern
	{
		// Two branches per handler in retail, one per hunted race, with identical actions. One branch
		// with a race list is the same test.
		OnSeeNpc = Of(Branch(6, "a garrison chief, there",
			[When.SeenRace(hunted)], Do.HateSeen(Unpeelable))),

		OnAttacked = Of(Branch(6, "and it is the one hitting me",
			[When.AttackerRace(hunted), When.FirstTime(Committed)],
			Do.HateAttacker(Unpeelable))),
	};

	protected VillageKillerAI(Npc owner)
		: base(owner)
	{
	}
}

/// <summary>An Elyos raiding party. Retail <c>LDF5_Village_Killer01_L</c> / <c>_02_L</c>.</summary>
[AIName("village_killer_elyos")]
public class VillageKillerElyosAI : VillageKillerAI
{
	private static readonly AiPattern Pattern_ = Build(Race.GCHIEF_DARK, Race.GCHIEF_DRAGON);

	public VillageKillerElyosAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>An Asmodian raiding party. Retail <c>LDF5_Village_Killer01_D</c> / <c>_02_D</c>.</summary>
[AIName("village_killer_asmodian")]
public class VillageKillerAsmodianAI : VillageKillerAI
{
	private static readonly AiPattern Pattern_ = Build(Race.GCHIEF_DRAGON, Race.GCHIEF_LIGHT);

	public VillageKillerAsmodianAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>A Balaur raiding party. Retail <c>LDF5_Village_Killer01_DR</c> / <c>_02_DR</c>.</summary>
[AIName("village_killer_balaur")]
public class VillageKillerBalaurAI : VillageKillerAI
{
	private static readonly AiPattern Pattern_ = Build(Race.GCHIEF_DARK, Race.GCHIEF_LIGHT);

	public VillageKillerBalaurAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
