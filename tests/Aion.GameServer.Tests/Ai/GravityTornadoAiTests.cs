using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="GravityTornadoAI"/>, translated from retail patterns
/// <c>IDTiamat_Tiamat_Gravity</c> and <c>IDTiamat_Hard_Gravity</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Found by the shared-<c>ai_name</c> audit. Two things were wrong: the tornado never spawned its
/// crusher, and both modes cast the hard-mode skill because the mode test named an npc that never
/// carries this AI. Both are pinned.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GravityTornadoAiTests
{
	private const int DragonLordsRefuge = 300520000;

	private const int NormalTornado = 283140;
	private const int HardTornado = 856046;
	private const int NormalCrusher = 283142;
	private const int HardCrusher = 856047;

	private const int NormalGravity = 20966;
	private const int HardGravity = 21901;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(GravityTornadoAI), typeof(AggressiveNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>The normal tornado brings its own crusher, on its own mark.</summary>
	[Fact]
	public void TheNormalTornadoBringsItsCrusher()
	{
		using BossAiHarness harness = NewHarness();
		Npc tornado = harness.Spawn(NormalTornado, 500f, 500f, 400f);

		Npc crusher = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == NormalCrusher));
		Assert.Equal(tornado.GetX(), crusher.GetX(), 1);
		Assert.Equal(tornado.GetY(), crusher.GetY(), 1);
		Assert.Equal(0, Count(harness, HardCrusher));
	}

	/// <summary>And the hard one brings the hard crusher, which nothing reached before this.</summary>
	[Fact]
	public void TheHardTornadoBringsTheHardCrusher()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(HardTornado, 500f, 500f, 400f);

		Assert.Equal(1, Count(harness, HardCrusher));
		Assert.Equal(0, Count(harness, NormalCrusher));
	}

	/// <summary>
	/// The normal tornado casts the <b>normal</b> gravity skill and the hard one the hard skill. It
	/// was the hard one for both before: the mode test named 283142, the crusher, which never carries
	/// this AI, so no tornado could ever match it.
	/// </summary>
	/// <remarks>
	/// The choice is pinned rather than the cast. This class casts through
	/// <c>NpcController.UseSkill</c>, which fires immediately instead of going through the skill queue
	/// the harness reads, so there is nothing to drain — and the two skills are told apart by stack
	/// name (<c>IDTIAMAT_TIAMAT_GRAVITY_SKILL</c> against <c>IDTIAMAT_HARD_...</c>), which is what
	/// makes the mapping a fact rather than a reading.
	/// </remarks>
	[Fact]
	public void EachModeCastsItsOwnGravitySkill()
	{
		Assert.Equal(NormalGravity, GravityTornadoAI.GravitySkillFor(NormalTornado));
		Assert.Equal(HardGravity, GravityTornadoAI.GravitySkillFor(HardTornado));
	}

	/// <summary>And each brings its own crusher, by the same mapping.</summary>
	[Fact]
	public void EachModeBringsItsOwnCrusher()
	{
		Assert.Equal(NormalCrusher, GravityTornadoAI.CrusherFor(NormalTornado));
		Assert.Equal(HardCrusher, GravityTornadoAI.CrusherFor(HardTornado));
	}

	/// <summary>
	/// <b>The twin beats every three seconds, after a first beat at one.</b>
	/// </summary>
	/// <remarks>
	/// Retail's damage twin does nothing but <c>broadcast_message 204</c> at one metre, a second after
	/// it appears and every three seconds after. Both twins ran plain <c>aggressive</c> here, so nothing
	/// sent 204 — exactly what this class's remark said — and the cast ran off a timer of its own at
	/// <b>half</b> retail's rate.
	/// <para>
	/// Counted with a probe listener rather than by watching the tornado cast: the cast goes straight
	/// through <c>NpcController.UseSkill</c> and never reaches a queue a pin can read, which is the same
	/// reason <see cref="GravityTornadoAI.GravitySkillFor"/> is exposed. The first version of this pin
	/// asserted a constant against itself and threw its own count away; a probe is what makes the beat
	/// observable.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheTwinBeatsEveryThreeSeconds()
	{
		using BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(GravityTornadoAI), typeof(GravityBombDamageAI), typeof(BeatProbeAI),
				typeof(AggressiveNpcAI))
			.Build();
		BeatProbeAI.Beats = 0;
		Npc tornado = harness.Spawn(NormalTornado, 500f, 500f, 200f);
		Npc twin = harness.LiveNpcs().Single(n => n.GetNpcId() == NormalCrusher);
		Npc probe = harness.SpawnWithAi(NormalTornado, "beat_probe", twin.GetX(), twin.GetY(), twin.GetZ());
		BossAiHarness.MakeMutuallyKnown(twin, probe);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(1, BeatProbeAI.Beats);

		// Nine more seconds carries beats at four, seven and ten.
		harness.Clock.Advance(TimeSpan.FromSeconds(9));

		Assert.Equal(4, BeatProbeAI.Beats);
	}

	/// <summary>
	/// <b>The twin stands where the tornado does.</b> Its broadcast reaches one metre, so anything
	/// further apart than that is a tornado that never casts again.
	/// </summary>
	[Fact]
	public void TheTwinStandsOnTheTornado()
	{
		using BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(GravityTornadoAI), typeof(GravityBombDamageAI), typeof(AggressiveNpcAI))
			.Build();
		Npc tornado = harness.Spawn(NormalTornado, 500f, 500f, 200f);
		Npc twin = harness.LiveNpcs().Single(n => n.GetNpcId() == NormalCrusher);

		double apart = Math.Sqrt(Math.Pow(twin.GetX() - tornado.GetX(), 2)
			+ Math.Pow(twin.GetY() - tornado.GetY(), 2));

		// Retail's own number, written out. Comparing against GravityBombDamageAI.Reach would have made
		// this pin agree with whatever the constant said -- a mutation widening the beat to a hundred
		// metres passed it, because the pin was using the value under test as its own expectation.
		Assert.True(apart <= 1f, $"the twin stands {apart:F2}m away and retail's beat reaches 1m");
		Assert.Equal(1f, GravityBombDamageAI.Reach);
	}

}

/// <summary>Counts <c>broadcast_message 204</c> arrivals, so the twin's beat is observable.</summary>
/// <remarks>
/// A throwaway listener, the same shape as the one in <c>ResearcherTeselikAiTests</c>. The tornado's own
/// answer to the beat is a cast that never reaches a readable queue, so the message is counted where it
/// lands instead.
/// </remarks>
[Aion.GameServer.Ai.AIName("beat_probe")]
public class BeatProbeAI : Aion.GameServer.Handlers.AI.GeneralNpcAI, Aion.GameServer.Ai.INpcMessageListener
{
	public static int Beats;

	public BeatProbeAI(Npc owner) : base(owner)
	{
	}

	public void OnNpcMessage(Npc sender, int messageType, Aion.GameServer.Model.GameObjects.VisibleObject? param)
	{
		if (messageType == GravityBombDamageAI.CastNow)
			Beats++;
	}
}
