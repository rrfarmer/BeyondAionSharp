using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Calindi's two ground hazards, which this port ran as one shape when retail has two.
/// </summary>
/// <remarks>
/// Retail's fire crown re-arms its idle timer, so it pulses every second for the ten seconds it stands.
/// Retail's shadow fire does not re-arm: it burns once, a second after it appears, and then stands for
/// fifteen. Both ran here as fixed-rate loops for fifteen seconds — the crown every two seconds and
/// <b>the shadow fire every half second</b>.
/// <para>
/// The pulses are casts rather than spawns in this port, because it holds retail's hazard/damage pair
/// the other way up. What these pins can see is the hazard's own lifetime and the texture npc beside it,
/// so those are what they assert; the cast counts are named in the doc and are not pinned.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CalindiSummonsAiTests
{
	private const int DragonLordsRefuge = 300520000;

	/// <summary>The fire crown pair: the persistent npc here, and its texture.</summary>
	private const int FireCrown = 283131;
	private const int FireCrownTexture = 283130;

	/// <summary>The shadow fire pair, and the hard-mode twin of each.</summary>
	private const int ShadowFire = 283133;
	private const int ShadowFireTexture = 283132;
	private const int HardFireCrown = 856299;
	private const int HardShadowFire = 856298;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			// The texture npcs are on noaction, and the harness validates every AI name it is asked to
			// place -- sixth pin this session to fail first for a missing WithAi entry.
			.WithAi(typeof(CalindiSummonsAI), typeof(NoActionAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The fire crown stands ten seconds</b>, which is the lifetime the worm that drops it gives it.
	/// </summary>
	/// <remarks>
	/// It stood fifteen. Retail's <c>IDTiamat_BurrowingWorm_BurrowDispel</c> spawns the crown with
	/// <c>live_time=10</c>, and the crown pulses for exactly as long as it is there.
	/// </remarks>
	[Theory]
	[InlineData(FireCrown)]
	[InlineData(HardFireCrown)]
	public void TheFireCrownStandsTenSeconds(int crown)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(crown, 480f, 500f, 427f);

		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.Equal(1, Count(harness, crown));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, crown));
	}

	/// <summary>
	/// <b>The shadow fire stands fifteen</b>, which is the lifetime Calindi gives it.
	/// </summary>
	/// <remarks>
	/// Both of retail's Calindi patterns, normal and hard, spawn it with <c>live_time=15</c>. That one
	/// was already right; it is pinned because the crown's was changed beside it and the two are easy to
	/// conflate — this class treated them as the same npc with different numbers.
	/// </remarks>
	[Theory]
	[InlineData(ShadowFire)]
	[InlineData(HardShadowFire)]
	public void TheShadowFireStandsFifteenSeconds(int fire)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(fire, 480f, 500f, 427f);

		harness.Clock.Advance(TimeSpan.FromSeconds(14));
		Assert.Equal(1, Count(harness, fire));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, fire));
	}

	/// <summary>
	/// <b>Each hazard puts its own texture npc beside it, and takes it away again.</b>
	/// </summary>
	/// <remarks>
	/// The texture is retail's actual hazard npc — 283130 is <c>IDTiamat_Kalyndi_FireCrown</c> — standing
	/// in as scenery because this port drives the pair from the other end. If it outlived its owner the
	/// room would fill with crowns.
	/// </remarks>
	[Theory]
	[InlineData(FireCrown, FireCrownTexture, 10)]
	[InlineData(ShadowFire, ShadowFireTexture, 15)]
	[InlineData(HardFireCrown, FireCrownTexture, 10)]
	[InlineData(HardShadowFire, ShadowFireTexture, 15)]
	public void EachHazardTakesItsTextureWithIt(int hazard, int texture, int lifeSeconds)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(hazard, 480f, 500f, 427f);

		Assert.Equal(1, Count(harness, texture));

		harness.Clock.Advance(TimeSpan.FromSeconds(lifeSeconds + 1));
		Assert.Equal(0, Count(harness, texture));
	}
}
