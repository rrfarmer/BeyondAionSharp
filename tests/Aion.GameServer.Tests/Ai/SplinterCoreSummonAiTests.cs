using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The four Abyssal Splinter cores — Rukril and Ebonsoul, stable and unstable — and the summons that
/// used to either stop after one wave or never stop at all.
/// </summary>
/// <remarks>
/// Retail gives all four summons <c>live_time</c> 70 against a branch timer of the same seventy seconds.
/// None of the four had a lifetime here, and the <b>same omission produced opposite failures</b>:
/// <list type="bullet">
/// <item>where a class guarded on "only if none are standing", the guard never passed twice and the
/// mechanic ran <b>once per fight</b>;</item>
/// <item>where it did not — the unstable pair each summon for their partner, unguarded — a fresh pair
/// arrived <b>every seventy seconds for the whole fight</b> and none ever left.</item>
/// </list>
/// <para>
/// The guards are dropped rather than left inert. Pazuzu's equivalent was harmless once its adds expired,
/// because its life (71) is a second under its cycle (72); here both are 70, so a check landing on the
/// same tick as the expiry could still see them standing. Retail spawns unconditionally.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SplinterCoreSummonAiTests
{
	private const int AbyssalSplinter = 300220000;
	private const int UnstableSplinter = 300600000;

	/// <summary>map, boss npc, the summon it calls, and the AI under test.</summary>
	public static TheoryData<int, int, int, string> Cores => new()
	{
		{ AbyssalSplinter, 216948, 281907, "rukril" },
		{ AbyssalSplinter, 216949, 281908, "ebonsoul" },
		{ UnstableSplinter, 219551, 283204, "unstablerukril" },
		{ UnstableSplinter, 219552, 283205, "unstableebonsoul" },
	};

	private static (BossAiHarness, Npc) Engaged(int map, int npcId)
	{
		BossAiHarness harness = BossAiHarness.For(map).WithWorldSize(2048)
			.WithAi(typeof(RukrilAI), typeof(EbonsoulAI), typeof(UnstableRukrilAI),
				typeof(UnstableEbonsoulAI), typeof(HomingNpcAI), typeof(PieceOfSplendorAI),
				typeof(PieceOfMidnightAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(npcId, 450f, 690f, 434f);
		Player player = harness.SpawnPlayer(452f, 692f, 434f);
		harness.Engage(boss, player);

		// Their summon clocks start when the 95 percent phase is crossed, not on entering combat and
		// not on the first blow -- the blow is only what makes the phase check run.
		BossAiHarness.SetExactPercent(boss, 90);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		return (harness, boss);
	}

	private static List<Npc> Summons(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	/// <summary><b>The summons arrive.</b> Five seconds in, which is this class's own opening delay.</summary>
	[Theory]
	[MemberData(nameof(Cores))]
	public void TheSummonsArrive(int map, int npcId, int summon, string _ai)
	{
		var (harness, _) = Engaged(map, npcId);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.NotEmpty(Summons(harness, summon));
	}

	/// <summary>
	/// <b>And they leave at seventy seconds.</b> The pin the change is about — before it, every one of
	/// these four stood in the room until the boss died.
	/// </summary>
	[Theory]
	[MemberData(nameof(Cores))]
	public void TheSummonsLeaveAtSeventySeconds(int map, int npcId, int summon, string _ai)
	{
		var (harness, _) = Engaged(map, npcId);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		var first = Summons(harness, summon).ToHashSet();
		Assert.NotEmpty(first);

		harness.Clock.Advance(TimeSpan.FromSeconds(71));

		Assert.DoesNotContain(Summons(harness, summon), n => first.Contains(n));
	}

	/// <summary>
	/// <b>And they do not pile up.</b> Counted across three full cycles, which is where the unguarded
	/// partner summons used to reach three times their proper number.
	/// </summary>
	[Theory]
	[MemberData(nameof(Cores))]
	public void TheSummonsDoNotAccumulate(int map, int npcId, int summon, string _ai)
	{
		var (harness, _) = Engaged(map, npcId);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		int firstWave = Summons(harness, summon).Count;

		harness.Clock.Advance(TimeSpan.FromSeconds(210));

		Assert.True(Summons(harness, summon).Count <= firstWave,
			$"summons piled up: {Summons(harness, summon).Count} against a wave of {firstWave}");
	}
}
