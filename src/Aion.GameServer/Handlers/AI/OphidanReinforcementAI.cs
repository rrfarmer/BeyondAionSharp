using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Ophidan Bridge's four reinforcement posts (284708–284711). Retail patterns
/// <c>BIDF5_U1_SummonSupport_1</c> through <c>_4</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Four invisible NPCs, one at each corner of the
/// bridge, and between them the reason the instance is a race: <b>every sixty seconds another pair of
/// beritran walks in, five times over, and then the post is spent.</b>
/// <list type="table">
/// <item><term>on waking</term><description>an idle timer, thirty seconds</description></item>
/// <item><term>every other tick</term><description>two more beritran at two fixed points</description></item>
/// <item><term>after the fifth pair</term><description>nothing more — the counter stops at ten</description></item>
/// <item><term>on despawning</term><description>everything it called takes its leave</description></item>
/// </list>
/// <para>
/// <b>The counter is retail's, and the way it is expressed is not.</b> Retail guards each wave with
/// <c>increase_intvar be_true_only_when_hit_the_bound="TRUE"</c> over bounds 0–2, 2–4, 4–6, 6–8 and
/// 8–10 — an element written as a <em>condition</em>, so all five would advance the counter as the
/// event tried them in turn. No reading of that produces five evenly spaced waves, and the designer's
/// own comments say exactly what it should be: 1차 through 5차 스폰, each "60s 후". Split here into a
/// read-only <see cref="When.CountEquals"/> on the branches and a <see cref="Do.Increment"/> inside
/// whichever one runs, which gives one step per thirty-second tick and a wave every second step. The
/// outcome is retail's; the mechanism is ours, and it is the first time this log has had to say that
/// about a guard rather than an action.
/// </para>
/// <para>
/// <b>They arrive but they do not march.</b> Every one of retail's spawns carries a
/// <c>pathname</c> — twenty-four distinct routes across the four posts, <c>NPCPathSupport_Path01</c>
/// through <c>_Path24</c> — so in retail each pair walks its own line into the bridge. We have no
/// mapping from those names to walker ids, so ours appear at the post and hold it. Half the mechanic,
/// and the half that matters for pacing.
/// </para>
/// <para>
/// <b>Not translated.</b> Retail's <c>spawn_range</c> of three metres around each named point, which
/// <see cref="Do.SpawnAt"/> has no room for and which nothing depends on; and the <c>pathname</c> on
/// every spawn, above.
/// </para>
/// </remarks>
[AIName("ophidan_reinforcement")]
public class OphidanReinforcementAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c>: one group, cleared when the post goes.</summary>
	private const int Wave = 1;

	/// <summary>Retail's <c>set_idle_timer</c>, and it is the tick rather than the cadence.</summary>
	private const int Tick = 30000;

	/// <summary>Retail's <c>INTVARI_FIRST</c>, and the bounds its five branches divide.</summary>
	private const int Counter = 0;
	private const int Spent = 10;

	/// <summary>Where each post puts its pair, and which two kinds it calls.</summary>
	private static readonly Dictionary<int, (int First, SpawnSpot At, int Second, SpawnSpot Then)> Posts = new()
	{
		[284708] = (231184, new SpawnSpot(720f, 457f, 600f), 231185, new SpawnSpot(720f, 463f, 600f)),
		[284709] = (231184, new SpawnSpot(560f, 422f, 623f), 231185, new SpawnSpot(560f, 427f, 623f)),
		[284710] = (231186, new SpawnSpot(645f, 541f, 594f), 231184, new SpawnSpot(641f, 537f, 594f)),
		[284711] = (231187, new SpawnSpot(449f, 498f, 604f), 231187, new SpawnSpot(453f, 494f, 602f)),
	};

	private static readonly Dictionary<int, AiPattern> Patterns =
		Posts.ToDictionary(e => e.Key, e => Build(e.Value.First, e.Value.At, e.Value.Second, e.Value.Then));

	private static AiPattern Build(int first, SpawnSpot at, int second, SpawnSpot then) => new AiPattern
	{
		// Seeded at one so the bounds below can be retail's own numbers. Retail's guard advances the
		// counter as a side effect of being evaluated, which puts it a step ahead of a counter that
		// only moves when an action says so; this restores the offset. Waves then land at sixty,
		// a hundred and twenty, a hundred and eighty, two hundred and forty and three hundred seconds,
		// which is what the designer's five comments say.
		OnWakeUp = Of(
			Branch(1, "", When.Always,
				Do.Increment(Counter, 0, Spent),
				Do.SetIdleTimer(Tick))),

		OnIdleTimer = Of(
			Wave5(first, at, second, then),
			WaveAt(5, 8, first, at, second, then),
			WaveAt(4, 6, first, at, second, then),
			WaveAt(3, 4, first, at, second, then),
			WaveAt(2, 2, first, at, second, then),

			// The tick between two waves: step the counter and come round again.
			Branch(1, "", When.Always,
				Do.Increment(Counter, 0, Spent),
				Do.SetIdleTimer(Tick))),

		OnDespawn = Of(
			Branch(7, "", When.Always,
				Do.Despawn(Wave))),
	};

	/// <summary>One wave: step the counter, place the pair, and set the next tick going.</summary>
	private static PatternBranch WaveAt(int priority, int hits, int first, SpawnSpot at, int second,
		SpawnSpot then)
		=> Branch(priority, $"wave at {hits}", [When.CountEquals(Counter, hits)],
			Do.Increment(Counter, 0, Spent),
			Do.SpawnAt(first, Wave, 0, at),
			Do.SpawnAt(second, Wave, 0, then),
			Do.SetIdleTimer(Tick));

	/// <summary>
	/// The last wave, which does not set another tick going: retail's counter stops at ten and the
	/// post has nothing left to send.
	/// </summary>
	private static PatternBranch Wave5(int first, SpawnSpot at, int second, SpawnSpot then)
		=> Branch(6, "the last wave", [When.CountEquals(Counter, Spent)],
			Do.SpawnAt(first, Wave, 0, at),
			Do.SpawnAt(second, Wave, 0, then));

	private readonly AiPattern pattern;

	public OphidanReinforcementAI(Npc owner)
		: base(owner)
	{
		pattern = Patterns[owner.GetNpcId()];
	}

	protected override AiPattern Pattern => pattern;
}
