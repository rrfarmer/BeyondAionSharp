using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The corasks and gnarls of Cygnea and Enshar that burst a swarm of clodworms onto whoever is
/// killing them. Retail patterns <c>LDF5_D2_Xipeto_Clodworm</c>, <c>_63</c>, <c>_65</c> and
/// <c>LDF5_D2_Xipeto_Sufur_Clodworm_65</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Once, below half health, three clodworms appear
/// on the attacker</b> — not on the corask — three metres apart, already hating them for a hundred and
/// already swinging. They go when it dies, when it leaves the fight, when it goes idle and when it
/// returns to its spawn point, so they are the corask's problem to clean up and never outlive it.
/// <para>
/// <b>This is the first family off the "no blockers at all" list</b>, and the point of that list is
/// that it is small: eighteen patterns out of two hundred and ninety-nine carry no skill index, no
/// path, no shout and no script. Everything here is a spawn and four despawns, which is why it could
/// be finished rather than half-built.
/// </para>
/// <para>
/// <b>Four patterns rather than one, because the swarm is level-matched.</b> Retail gives each band its
/// own summon — 284155 at sixty-one, 284157 at sixty-three, 283903 at sixty-five, and the sulphur
/// gnarl its own 283904 — and one shared id would have put a level-61 swarm on a level-65 fight. The
/// four classes exist only to carry those four numbers.
/// </para>
/// <para>
/// <b>Retail arms the same branch from two events.</b> <c>on_attacked</c> and <c>on_spelled</c> carry
/// identical bodies, so a caster who never lands a melee blow gets the swarm too; the flag var makes
/// it once either way.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. These four patterns are complete.
/// </para>
/// </remarks>
public abstract class XipetoClodwormAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c> — the group every despawn branch clears.</summary>
	private const int Swarm = 1;

	/// <summary>Retail's <c>num_to_spawn</c> and <c>spawn_range</c>.</summary>
	private const int Three = 3;
	private const float ThreeMetres = 3f;

	/// <summary>Retail's <c>hatepoints_to_add</c> on the <c>attack_target_after_spawn</c>.</summary>
	private const int OnArrival = 100;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>: once a fight.</summary>
	private const int Burst = 1;

	private const int Half = 50;

	/// <summary>
	/// The one table, with the band's own swarm in it.
	/// </summary>
	protected static AiPattern Build(int swarmNpcId)
	{
		PatternBranch OnHurt(int priority) => Branch(priority, "burst, once, below half",
			[When.HpBelow(Half), When.FirstTime(Burst)],
			Do.SpawnOnTarget(swarmNpcId, Swarm, count: Three, range: ThreeMetres,
				attackHate: OnArrival));

		PatternBranch[] Clear(int priority) =>
			Of(Branch(priority, "and take them with it", [], Do.Despawn(Swarm)));

		return new AiPattern
		{
			// Retail carries the same body on on_attacked and on_spelled, so a caster who never lands
			// a melee blow gets it too. The flag var makes it once either way.
			OnAttacked = Of(OnHurt(1)),
			OnDie = Clear(4),
			OnLeaveAttack = Clear(3),
			OnEnterIdle = Clear(3),
		};
	}

	protected XipetoClodwormAI(Npc owner)
		: base(owner)
	{
	}
}

/// <summary>Ebon and black corask (219754, 219755). Retail <c>LDF5_D2_Xipeto_Clodworm</c>.</summary>
[AIName("xipeto_clodworm_61")]
public class XipetoClodworm61AI : XipetoClodwormAI
{
	private static readonly AiPattern Pattern_ = Build(284155);

	public XipetoClodworm61AI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>Wily gnarl (230494). Retail <c>LDF5_D2_Xipeto_Clodworm_63</c>.</summary>
[AIName("xipeto_clodworm_63")]
public class XipetoClodworm63AI : XipetoClodwormAI
{
	private static readonly AiPattern Pattern_ = Build(284157);

	public XipetoClodworm63AI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>Lurking and burrowing corask (235878, 235879). Retail <c>LDF5_D2_Xipeto_Clodworm_65</c>.</summary>
[AIName("xipeto_clodworm_65")]
public class XipetoClodworm65AI : XipetoClodwormAI
{
	private static readonly AiPattern Pattern_ = Build(283903);

	public XipetoClodworm65AI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>Swamp gnarl (230586). Retail <c>LDF5_D2_Xipeto_Sufur_Clodworm_65</c>.</summary>
/// <remarks>Its swarm is a different npc from the other sixty-five's — 283904 rather than 283903.</remarks>
[AIName("xipeto_clodworm_sulphur")]
public class XipetoClodwormSulphurAI : XipetoClodwormAI
{
	private static readonly AiPattern Pattern_ = Build(283904);

	public XipetoClodwormSulphurAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
