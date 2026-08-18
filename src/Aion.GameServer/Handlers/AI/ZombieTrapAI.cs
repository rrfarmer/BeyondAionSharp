using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Adma Stronghold's zombie traps (281027). Retail pattern <c>ND2_Trap_IDDF2A</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A trap that goes off when a player walks past</b>:
/// it puts suspicious zombies on the player, three metres apart, and is gone in the same branch. On our
/// server it has been a harmless prop standing in the corridor since the instance was ported.
/// <para>
/// <b>The unlucky roll gives you fewer zombies, not more.</b> Retail writes two branches, and the one
/// carrying <c>test_probability 50</c> spawns <b>two</b> while the fall-through spawns <b>three</b> —
/// so half the time a player is caught by the smaller burst. Reading the priorities the other way round
/// would have made the coin flip a punishment instead of a reprieve, which is the opposite fight, and
/// it is the kind of inversion that is invisible unless the two counts are pinned separately.
/// </para>
/// <para>
/// <b>It fires on seeing a <em>player</em>.</b> Retail's handler is <c>on_see_user</c> rather than
/// <c>on_see_npc</c>, which is why <see cref="Ai.Pattern.AiPattern.OnSeeUser"/> exists as its own slot:
/// a trap that went off when the guard beside it wandered into view would be spent before anyone
/// arrived.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. This pattern is complete.
/// </para>
/// </remarks>
[AIName("zombie_trap")]
public class ZombieTrapAI : PatternAi
{
	/// <summary>Retail's <c>IDDF2A_ZombieTrapSum_50_An</c> — the suspicious zombie.</summary>
	private const int Zombie = 281028;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, <c>spawn_range</c> and <c>live_time</c>.</summary>
	private const int Burst = 1;
	private const float ThreeMetres = 3f;
	private const int FiveMinutes = 300;

	/// <summary>Retail's <c>test_probability</c> on the branch that spawns the smaller burst.</summary>
	private const int HalfTheTime = 50;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnSeeUser = Of(
			Branch(2, "half the time, two", [When.Enemy, When.Chance(HalfTheTime)],
				Do.SpawnOnSeen(Zombie, Burst, count: 2, range: ThreeMetres, liveSeconds: FiveMinutes),
				Do.DespawnSelf()),

			Branch(1, "otherwise three", [When.Enemy],
				Do.SpawnOnSeen(Zombie, Burst, count: 3, range: ThreeMetres, liveSeconds: FiveMinutes),
				Do.DespawnSelf())),
	};

	public ZombieTrapAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
