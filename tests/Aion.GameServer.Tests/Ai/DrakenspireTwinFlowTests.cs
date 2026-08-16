using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Handlers.Instance;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the twin-protector flow in <see cref="DrakenspireDepthsInstance"/>, translated from the
/// retail <c>IDSeal_Twin_*_Source</c> patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The first test of an instance handler in this suite, and it exists because of a specific
/// miss.</b> A hook was added to this handler and removed a commit later — it could not have worked,
/// and every pin passed anyway, because the pins drove the AI class directly and nothing exercised
/// the handler. The twin flow is where the encounter's decisions actually live, so this drives
/// <c>OnDie</c> and looks at what the room contains afterwards.
/// <para>
/// The handler is constructed against the harness's own map instance and told about deaths by hand;
/// no packets go anywhere, because the harness's players have no connection.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DrakenspireTwinFlowTests
{
	private const int Drakenspire = 301390000;

	private const int LavaProtector = 236227;
	private const int HeatventProtector = 236228;
	private const int FountlessLava = 236225;
	private const int FountlessHeatvent = 236226;

	private const int LavaFont = 855708;
	private const int HeatventFont = 855709;
	private const int OminousDarkness = 702769;

	private static (BossAiHarness, DrakenspireDepthsInstance) Room()
	{
		BossAiHarness harness = BossAiHarness.For(Drakenspire).WithWorldSize(2048)
			.WithAi(typeof(TwinProtectorAI), typeof(TwinFontAI), typeof(TwinFailureDisplayAI),
				typeof(AggressiveNpcAI), typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI),
				typeof(NoActionAI))
			.Build();
		var handler = new DrakenspireDepthsInstance(
			harness.World.GetWorldMap(Drakenspire).GetMainWorldMapInstance());
		return (harness, handler);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// Tells the handler a protector died, and takes the body away.
	/// </summary>
	/// <remarks>
	/// The handler's <c>OnDie</c> is the decision under test, and it does not remove the corpse —
	/// production's death path does that separately. Leaving it in the world made a pin that counts
	/// protectors read the dead one as still standing, which is a fact about the harness rather than
	/// about the encounter.
	/// </remarks>
	private static void Kills(DrakenspireDepthsInstance handler, Npc protector)
	{
		handler.OnDie(protector);
		protector.GetController().DeleteIfAliveOrCancelRespawn();
	}

	/// <summary>The first twin down leaves its font where it fell.</summary>
	[Fact]
	public void TheFirstTwinDownLeavesAFont()
	{
		var (harness, handler) = Room();
		using BossAiHarness _h = harness;

		Npc lava = harness.Spawn(LavaProtector, 531f, 212f, 1683f);
		Kills(handler, lava);

		Assert.Equal(1, Count(harness, LavaFont));
		Assert.Equal(0, Count(harness, HeatventFont));
	}

	/// <summary>
	/// <b>Miss the window and the fountless one comes back, where it fell.</b> Java parity respawned
	/// the font-leaving version at a fixed mark, so a raid that missed once could miss forever.
	/// </summary>
	[Fact]
	public void MissingTheWindowBringsBackTheFountlessProtector()
	{
		var (harness, handler) = Room();
		using BossAiHarness _h = harness;

		Npc lava = harness.Spawn(LavaProtector, 620f, 240f, 1683f);
		Kills(handler, lava);
		Assert.Equal(1, Count(harness, LavaFont));

		harness.Clock.Advance(TimeSpan.FromSeconds(16));

		Assert.Equal(0, Count(harness, LavaFont));
		Assert.Equal(1, Count(harness, FountlessLava));
		Assert.Equal(0, Count(harness, LavaProtector));

		// Where it fell, not at the opening mark.
		Npc back = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == FountlessLava));
		Assert.Equal(620f, back.GetX(), 1);
		Assert.Equal(240f, back.GetY(), 1);
	}

	/// <summary>
	/// <b>And a fountless protector leaves no font</b>, which is the whole point of it — the loop
	/// closes after one failure.
	/// </summary>
	[Fact]
	public void AFountlessProtectorLeavesNoFont()
	{
		var (harness, handler) = Room();
		using BossAiHarness _h = harness;

		Npc fountless = harness.Spawn(FountlessLava, 531f, 212f, 1683f);
		Kills(handler, fountless);

		Assert.Equal(0, Count(harness, LavaFont));
		Assert.Equal(0, Count(harness, HeatventFont));
	}

	/// <summary>
	/// <b>Both down inside the window leaves the ominous darkness behind.</b> Retail's success message
	/// turns the standing font into the quest object rather than deleting it; Java parity deleted it,
	/// so a raid that won saw the same empty floor as one that never engaged.
	/// </summary>
	[Fact]
	public void KillingBothInsideTheWindowLeavesTheQuestObject()
	{
		var (harness, handler) = Room();
		using BossAiHarness _h = harness;

		Npc lava = harness.Spawn(LavaProtector, 531f, 212f, 1683f);
		Kills(handler, lava);
		Assert.Equal(1, Count(harness, LavaFont));

		Npc heatvent = harness.Spawn(HeatventProtector, 530f, 151f, 1683f);
		Kills(handler, heatvent);

		Assert.Equal(0, Count(harness, LavaFont));
		Assert.Equal(1, Count(harness, OminousDarkness));

		// On the font's mark, which is where the first twin fell.
		Npc left = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == OminousDarkness));
		Assert.Equal(531f, left.GetX(), 1);
	}

	/// <summary>
	/// A completed encounter never spawns a protector, however long the room is left running.
	/// </summary>
	/// <remarks>
	/// This holds for two reasons and the pin cannot separate them: winning cancels the fifteen-second
	/// task <em>and</em> takes the font away, and the respawn needs a font. Removing the cancel alone
	/// survives a mutation sweep for exactly that reason. It is kept as an outcome pin rather than
	/// rewritten, because the outcome is what a raid sees — but the cancel is belt-and-braces, and
	/// anything that later leaves a font standing on a win would make it load-bearing.
	/// </remarks>
	[Fact]
	public void AWonEncounterNeverBringsAProtectorBack()
	{
		var (harness, handler) = Room();
		using BossAiHarness _h = harness;

		Kills(handler, harness.Spawn(LavaProtector, 531f, 212f, 1683f));
		Kills(handler, harness.Spawn(HeatventProtector, 530f, 151f, 1683f));

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(0, Count(harness, FountlessLava));
		Assert.Equal(0, Count(harness, FountlessHeatvent));
	}
}
