using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Brigade General Terath, whose jump event was placing two hostile drakan.
/// </summary>
/// <remarks>
/// Retail's <c>IDTiamat_Sardha</c> names every npc this fight places. Java named one of them with a
/// <c>TODO find Right ID</c> and guessed 283558 — <c>3rd vituperators assassin</c>, a real aggressive
/// monster one digit away from the effect npc retail actually uses. These pins assert the ids, the posts
/// and the lifetimes, which are the parts the pattern states outright.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BrigadeGeneralTerathAiTests
{
	private const int TiamatStronghold = 300510000;

	private const int Terath = 219354;

	/// <summary>Retail's <c>IDTiamat_Sadha_JumpBoxFX</c>, and the drakan Java put in its place.</summary>
	private const int JumpBoxFx = 283158;
	private const int VituperatorsAssassin = 283558;

	/// <summary>The aetheric field, and the two gravity npcs the jump drops between the posts.</summary>
	private const int AethericField = 730692;
	private const int GravityUp = 283109;
	private const int GravityDown = 283110;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			// The gravity pair carries an AI of its own and the harness validates every name it is asked
			// to place, so omitting it makes the spawn throw. Fifth pin this session to fail that way.
			.WithAi(typeof(BrigadeGeneralTerathAI), typeof(DistortedSpaceAI), typeof(GravityAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc terath = harness.Spawn(Terath, 1030f, 300f, 409f);
		Player player = harness.SpawnPlayer(1035f, 300f, 409f);
		harness.Engage(terath, player);
		return terath;
	}

	/// <summary>
	/// <b>Engaging Terath raises his aetheric field inside the room.</b>
	/// </summary>
	/// <remarks>
	/// Java spawned it at <c>(1030.08, 1030.08, 1030.08)</c> — the x repeated into y and z — which puts it
	/// seven hundred units up the map from the fight. Retail's post is <c>(1030.08, 297.31, 407.04)</c>.
	/// </remarks>
	[Fact]
	public void HisFieldStandsWhereRetailPutsIt()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		Npc field = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == AethericField);
		Assert.Equal(1030.08f, field.GetX(), 2);
		Assert.Equal(297.31f, field.GetY(), 2);
		Assert.Equal(407.04f, field.GetZ(), 2);
	}

	/// <summary>
	/// <b>The jump event places effect npcs and no monsters.</b>
	/// </summary>
	/// <remarks>
	/// This is the whole point of the correction: 283558 is a monster and would fight the party, 283158
	/// is an effect npc on <c>general</c> and does not.
	/// </remarks>
	[Fact]
	public void TheJumpPlacesEffectNpcsAndNoDrakan()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		BossAiHarness.SetHpPercent(terath, 89);
		terath.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, terath);

		Assert.Equal(2, Count(harness, JumpBoxFx));
		Assert.Equal(0, Count(harness, VituperatorsAssassin));
	}

	/// <summary>
	/// <b>And they stand on retail's two posts</b>, and are cleared when the event ends.
	/// </summary>
	/// <remarks>
	/// Retail's live time is twenty-nine seconds and this class ends its whole gravity event at thirty,
	/// deleting whatever is left by hand — so the two are <b>one second apart</b>, and only an assertion
	/// inside that second tells them apart. It is made below: present at twenty-eight, gone at
	/// twenty-nine and a half. Without the lifetime the boxes survive to the thirty-second sweep.
	/// <para>
	/// The gravity pair has no such window: retail gives it twenty-four seconds from a spawn ten seconds
	/// in, which lands at thirty-four, and the sweep at thirty always beats it. That lifetime is set
	/// because it matters the moment the cadence is corrected, but nothing here can observe it.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheJumpBoxesStandOnBothPostsAndAreClearedWithTheEvent()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		BossAiHarness.SetHpPercent(terath, 89);
		terath.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, terath);

		float[] posts = harness.LiveNpcs().Where(n => n.GetNpcId() == JumpBoxFx)
			.Select(n => n.GetX()).OrderBy(x => x).ToArray();
		Assert.Equal(1002.07f, posts[0], 2);
		Assert.Equal(1056.8f, posts[1], 2);

		harness.Clock.Advance(TimeSpan.FromSeconds(28));
		Assert.Equal(2, Count(harness, JumpBoxFx));

		// Inside the one second between retail's live time and this class's own sweep.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1500));
		Assert.Equal(0, Count(harness, JumpBoxFx));
	}

	/// <summary>
	/// <b>The two gravity npcs arrive ten seconds in, on the same point.</b>
	/// </summary>
	/// <remarks>
	/// Retail places both at <c>(1029.93, 297.31, 409)</c>; this port had them a few centimetres apart on
	/// two hand-typed rows. They are cleared with the event, as above.
	/// </remarks>
	[Fact]
	public void TheGravityPairArrivesTogetherAndIsClearedWithTheEvent()
	{
		using BossAiHarness harness = NewHarness();
		Npc terath = Engaged(harness);

		BossAiHarness.SetHpPercent(terath, 89);
		terath.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, terath);

		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.Equal(0, Count(harness, GravityUp));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Npc up = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == GravityUp);
		Npc down = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == GravityDown);
		Assert.Equal(up.GetX(), down.GetX(), 2);
		Assert.Equal(up.GetY(), down.GetY(), 2);
		Assert.Equal(1029.93f, up.GetX(), 2);

		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(0, Count(harness, GravityUp));
		Assert.Equal(0, Count(harness, GravityDown));
	}
}
