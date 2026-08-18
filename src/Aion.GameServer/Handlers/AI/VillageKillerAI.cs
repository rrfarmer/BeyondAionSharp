using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The stonereach and flamecrest thrashers of Cygnea and Enshar, which hunt a settlement's garrison
/// rather than its visitors. Retail patterns <c>LDF5_Village_Killer01_DR</c>, <c>_01_L</c>,
/// <c>_02_D</c> and <c>_02_DR</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The moment one of these sees a garrison chief it
/// drops whatever it was doing and goes for it, with five million hate points.</b> Five million is not
/// a weight, it is a statement: nothing a player can do will peel a thrasher off the garrison it came
/// for. The same rule applies when a chief attacks or casts on it.
/// <para>
/// <b>The squads hunt different factions.</b> The <c>01</c> patterns watch for <c>gchief_light</c> and
/// <c>gchief_dark</c> — the Elyos and Asmodian garrisons — and the <c>02</c> patterns for
/// <c>gchief_light</c> and <c>gchief_dragon</c>. A single class covering all four with one race list
/// would send flamecrest thrashers after Asmodian chiefs they ignore in retail.
/// </para>
/// <para>
/// <b>This is the guard that was read as unusable.</b> <c>is_race</c> carries a <c>race_type</c> on
/// every one of its 2,879 uses in the 5.8 files, and a comment in <see cref="Ai.Pattern.PatternAi"/>
/// recorded the opposite for months because the summariser dropped the value. Nothing here would have
/// been buildable under that reading.
/// </para>
/// <para>
/// <b>Two halves are not shipped, and the reason is the same for both.</b>
/// <para>
/// <c>AggroList.AddHate</c> refuses hate on a creature the owner is not an enemy of, and our tribe
/// table makes a thrasher and a <b>Balaur</b> garrison friends. So retail's <c>02</c> patterns hunting
/// <c>gchief_dragon</c> translate to a call that lands and a hate that does not — measured as zero
/// against five million for the Elyos and Asmodian garrisons, with the same guard and the same action.
/// It is pinned as zero rather than forced past the aggro list, because the choice is between retail's
/// pattern and our tribe table and someone should make it deliberately.
/// </para>
/// <para>
/// The <c>on_attacked</c> and <c>on_spelled</c> halves are deferred for the same measurement.
/// <c>When.AttackerRace</c> and <c>Do.HateAttacker</c> are built and wired; what they run into is this
/// gate.
/// </para>
/// </para>
/// </remarks>
public abstract class VillageKillerAI : PatternAi
{
	/// <summary>Retail's <c>points_to_add</c> on all six branches of all four patterns.</summary>
	private const int Unpeelable = 5_000_000;

	protected static AiPattern Build(params Race[] hunted) => new AiPattern
	{
		// Six branches in retail, two per handler, one per hunted race. One branch per handler with a
		// race list is the same test: the actions are identical and only the race differs.
		OnSeeNpc = Of(Branch(6, "a garrison chief, there",
			[When.SeenRace(hunted)], Do.HateSeen(Unpeelable))),

	};

	protected VillageKillerAI(Npc owner)
		: base(owner)
	{
	}
}

/// <summary>
/// Stonereach force thrasher (234104) and its occupation-assassin twin. Retail
/// <c>LDF5_Village_Killer01_DR</c>.
/// </summary>
[AIName("village_killer_01")]
public class VillageKiller01AI : VillageKillerAI
{
	private static readonly AiPattern Pattern_ = Build(Race.GCHIEF_LIGHT, Race.GCHIEF_DARK);

	public VillageKiller01AI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Flamecrest thrashers (234107, 234109). Retail <c>LDF5_Village_Killer02_D</c> and <c>_02_DR</c>.
/// </summary>
/// <remarks>The dragon garrison in place of the Asmodian one — the whole difference between the squads.</remarks>
[AIName("village_killer_02")]
public class VillageKiller02AI : VillageKillerAI
{
	private static readonly AiPattern Pattern_ = Build(Race.GCHIEF_LIGHT, Race.GCHIEF_DRAGON);

	public VillageKiller02AI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
