using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Queen Serusia (231003), the Idian Depths field named. Retail pattern <c>NeutQueen_N_65_Ah</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Her egg-laying was already right — 75%, 50% and 25%
/// for one, two and three eggs, in <c>ai/spawn_helpers.xml</c> against the correct npc — and **the
/// eggs never hatched**. Retail arms a battle timer alongside every one of those three branches and
/// broadcasts a hatch call fifteen seconds later; every egg in fifty metres becomes a larva and is
/// gone.
/// <para>
/// <b>Fifteen seconds is the mechanic.</b> An egg that lives out its timer is a larva; an egg killed
/// first is nothing. Without the timer the eggs are scenery that the queen tidies away on dying, which
/// is what this fight has been doing — the adds existed and could not arrive.
/// </para>
/// <para>
/// <b>Retail writes three timers and three numbers and one listener that answers all three.</b> The
/// numbers are therefore decoration: whichever call lands first hatches every egg standing, including
/// eggs laid at a later threshold whose own timer has not run out. A raid that pushes her from 75 to
/// 50 quickly gets all three eggs at once. That is retail's arithmetic, not an approximation of it,
/// and it is pinned.
/// </para>
/// <para>
/// <b>Not translated:</b> her two combat skills on their alternating fifteen-second loop
/// (<c>SKILLI_INDEX_0</c> and <c>_1</c>) and the self-buff she casts on waking and on leaving combat
/// (<c>_2</c>) — skill indices, as everywhere. The egg despawn on death, on despawn and on going home
/// is already <see cref="SummonerAI"/>'s <c>RemoveAndResetHelperSpawns</c>, which is retail's three
/// <c>despawn SPAWN_ID_1</c> branches by a different route.
/// </para>
/// </remarks>
[AIName("queen_serusia")]
public class QueenSerusiaAI : SummonerAI
{
	/// <summary>
	/// Retail's <c>402000</c>, <c>402001</c> and <c>402002</c> — one per threshold, and the egg
	/// answers all three identically.
	/// </summary>
	public const int HatchAt75 = 402000;
	public const int HatchAt50 = 402001;
	public const int HatchAt25 = 402002;

	/// <summary>Retail's <c>delay</c> on all three of <c>BTIMERI_INDEX_10</c>–<c>_12</c>.</summary>
	private const long Incubation = 15000L;

	/// <summary>Retail's <c>range_as_meter</c> on all three broadcasts.</summary>
	private const float HatchReach = 50f;

	public QueenSerusiaAI(Npc owner)
		: base(owner)
	{
	}

	/// <summary>
	/// Retail arms the timer in the same branch that lays the eggs, before the spawn — so this rides
	/// the hook that runs before <see cref="SummonerAI"/>'s scheduled spawn rather than after it.
	/// </summary>
	protected override void HandleBeforeSpawn(Percentage percent)
	{
		base.HandleBeforeSpawn(percent);

		int message = percent.GetPercent() switch
		{
			75 => HatchAt75,
			50 => HatchAt50,
			25 => HatchAt25,
			_ => 0,
		};
		if (message == 0)
			return;

		ThreadPoolManager.GetInstance().Schedule(_ =>
		{
			if (!IsDead())
				NpcMessageBus.Broadcast(GetOwner(), message, GetOwner(), HatchReach);
			return ValueTask.CompletedTask;
		}, Incubation);
	}
}

/// <summary>
/// Queen Serusia's egg (284273). Retail pattern <c>NeutQueenSumEgg_N_65_e</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its entire pattern is three branches that are the
/// same branch three times: on any of the queen's hatch calls, put a larva on its own spot and go.
/// </remarks>
[AIName("serusia_egg")]
public class SerusiaEggAI : PatternAi
{
	/// <summary>Retail's <c>BLDF5_SumNeuth2_Abyss_As_65_An</c>.</summary>
	private const int Larva = 284278;

	/// <summary>
	/// Retail's <c>SPAWN_ID_NONE</c>: the egg does not keep hold of what it hatched. Nothing could —
	/// it despawns in the same branch.
	/// </summary>
	private const int Untracked = 0;

	/// <summary>Retail's <c>spawn_range</c>.</summary>
	private const float NextToTheShell = 5f;

	private static PatternBranch Hatch(int priority, int message) =>
		Branch(priority, "hatch", [When.Message(message)],
			Do.SpawnNear(Larva, Untracked, count: 1, range: NextToTheShell), Do.DespawnSelf());

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Hatch(9, QueenSerusiaAI.HatchAt75),
			Hatch(8, QueenSerusiaAI.HatchAt50),
			Hatch(7, QueenSerusiaAI.HatchAt25)),
	};

	public SerusiaEggAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Serusia's larvae (284278). Retail pattern <c>GhostRun_Sum_As_N_65_Ae</c>, whose whole content is
/// one branch: when the fight is over, leave.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Worth its own class rather than being left on
/// <c>aggressive</c>: a hatched larva whose target walks away would otherwise stand in the Idian
/// Depths until it decayed, and the queen cannot tidy it — <see cref="SummonerAI"/> only tracks what
/// <em>it</em> spawned, and the larva was spawned by an egg.
/// </remarks>
[AIName("serusia_larva")]
public class SerusiaLarvaAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterIdle = Of(Branch(7, "the fight is over", [], Do.DespawnSelf())),
	};

	public SerusiaLarvaAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
