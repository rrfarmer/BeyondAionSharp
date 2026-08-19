using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Mysterious Crate, which handed out its rarest prize a third of the time.
/// </summary>
/// <remarks>
/// Retail's <c>ND2_CheatboxSu</c> is six <c>on_killed_by_user</c> rungs tried in priority order, each an
/// independent <c>test_probability</c> that ends the chain when it passes: 1% chaos dracus, then 20% six
/// clodworms, then 20% the mosbear family of three, then 20% the mumu pair, then 9% arrogant amurru, and
/// a last rung with no condition at all so the crate always produces something.
/// <para>
/// aionemu rolled <c>Rnd.Get(1, 3)</c> over three npcs. That is chaos dracus at one in three rather than
/// one in a hundred, one clodworm rather than six, and no mosbears, mumus or amurru at any time.
/// </para>
/// <para>
/// Found by <c>audit_summon_ids.py</c>, which compares the npc ids a class names against the ids its
/// retail pattern spawns -- readable at all only because <c>ai_binding.tsv</c> turned out to resolve the
/// <c>npc_nameid</c> devnames that spawn actions use.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DracusBoxAiTests
{
	private const int Eltnen = 210020000;

	private const int MysteriousCrate = 211801;

	private const int Elroco = 211792;
	private const int MumuMon = 211793;
	private const int MumuZoo = 211794;
	private const int CursedCamu = 211795;
	private const int CursedMiku = 211796;
	private const int CursedMuku = 211797;
	private const int ArrogantAmurru = 211798;
	private const int OozingClodworm = 211799;
	private const int ChaosDracus = 211800;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Eltnen).WithWorldSize(2048)
			.WithAi(typeof(DracusBox), typeof(OneDmgNoActionAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>Kills one crate and returns only what that crate produced.</summary>
	private static Dictionary<int, int> OneCrate(BossAiHarness harness, Dictionary<int, int> running)
	{
		Npc crate = harness.Spawn(MysteriousCrate, 500f, 500f, 200f);
		crate.GetAi().OnGeneralEvent(AiEventType.Died);

		Dictionary<int, int> now = harness.LiveNpcs()
			.GroupBy(n => n.GetNpcId())
			.ToDictionary(g => g.Key, g => g.Count());
		Dictionary<int, int> delta = new Dictionary<int, int>();
		foreach ((int id, int count) in now)
		{
			int before = running.TryGetValue(id, out int b) ? b : 0;
			if (count > before && id != MysteriousCrate)
				delta[id] = count - before;
		}
		foreach ((int id, int count) in now)
			running[id] = count;
		return delta;
	}

	/// <summary>
	/// <b>Chaos dracus is one in a hundred, not one in three.</b> This is the defect, and it is worth
	/// three hundred crates to pin: the old roll produced it about a hundred times in this many.
	/// </summary>
	[Fact]
	public void ChaosDracusIsRareRatherThanOneInThree()
	{
		const int Crates = 300;
		using BossAiHarness harness = NewHarness();
		Dictionary<int, int> running = new Dictionary<int, int>();

		int dracus = 0;
		for (int i = 0; i < Crates; i++)
			if (OneCrate(harness, running).ContainsKey(ChaosDracus))
				dracus++;

		// Retail's rate puts the expectation at three. Thirty is ten times that and still nowhere near the
		// hundred the uniform roll produced, so this fails on the defect and not on a run of luck.
		Assert.True(dracus < 30,
			$"chaos dracus came out of {dracus} of {Crates} crates; retail's rung is one per cent");
	}

	/// <summary>
	/// <b>Every crate produces something.</b> Retail's lowest rung carries no <c>test_probability</c> at
	/// all, so there is no outcome where the crate simply vanishes.
	/// </summary>
	[Fact]
	public void EveryCrateProducesSomething()
	{
		using BossAiHarness harness = NewHarness();
		Dictionary<int, int> running = new Dictionary<int, int>();

		for (int i = 0; i < 120; i++)
			Assert.NotEmpty(OneCrate(harness, running));
	}

	/// <summary>
	/// <b>The groups arrive whole.</b> Six clodworms, three mosbears, two mumus -- retail's
	/// <c>num_to_spawn</c> and its three-spawn and two-spawn rungs. The old code could only ever produce
	/// one npc, so every one of these assertions was unreachable.
	/// </summary>
	[Fact]
	public void TheGroupRungsArriveWhole()
	{
		using BossAiHarness harness = NewHarness();
		Dictionary<int, int> running = new Dictionary<int, int>();
		bool sawClodworms = false, sawMosbears = false, sawMumus = false;

		for (int i = 0; i < 300; i++)
		{
			Dictionary<int, int> got = OneCrate(harness, running);

			if (got.TryGetValue(OozingClodworm, out int worms))
			{
				Assert.Equal(6, worms);
				sawClodworms = true;
			}
			if (got.ContainsKey(CursedMuku) || got.ContainsKey(CursedMiku) || got.ContainsKey(CursedCamu))
			{
				Assert.True(got.ContainsKey(CursedMuku) && got.ContainsKey(CursedMiku) && got.ContainsKey(CursedCamu),
					"the mosbear family came out of the crate incomplete");
				sawMosbears = true;
			}
			if (got.ContainsKey(MumuMon) || got.ContainsKey(MumuZoo))
			{
				Assert.True(got.ContainsKey(MumuMon) && got.ContainsKey(MumuZoo),
					"the mumu pair came out of the crate incomplete");
				sawMumus = true;
			}
		}

		// Without these the assertions above are vacuous -- each rung is roughly one crate in six.
		Assert.True(sawClodworms, "no crate in 300 produced clodworms");
		Assert.True(sawMosbears, "no crate in 300 produced the mosbear family");
		Assert.True(sawMumus, "no crate in 300 produced the mumu pair");
	}

	/// <summary>
	/// <b>The amurru and the elroco rungs exist.</b> Neither npc could come out of the old crate at all;
	/// the elroco is the unconditional rung and should be the commonest thing in the box.
	/// </summary>
	[Fact]
	public void TheAmurruAndElrocoRungsBothFire()
	{
		using BossAiHarness harness = NewHarness();
		Dictionary<int, int> running = new Dictionary<int, int>();
		int amurru = 0, elroco = 0;

		for (int i = 0; i < 300; i++)
		{
			Dictionary<int, int> got = OneCrate(harness, running);
			if (got.ContainsKey(ArrogantAmurru))
				amurru++;
			if (got.ContainsKey(Elroco))
				elroco++;
		}

		Assert.True(amurru > 0, "no crate in 300 produced an arrogant amurru");
		Assert.True(elroco > amurru,
			$"the unconditional rung fired {elroco} times against the amurru's {amurru}; it is the fallback "
			+ "and should be the commonest outcome");
	}
}
