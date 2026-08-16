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
}
