using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The sand a burrowing thorn throws up, and the damage npc it drops — hard mode included.
/// </summary>
/// <remarks>
/// Retail's <c>IDTiamat_Tiamat_Uplift</c> and <c>IDTiamat_Hard_Earthquake_01</c> say the same thing:
/// on waking, place a damage npc at your own feet for three seconds. Java found that npc by adding one
/// to its own id, which is right for the normal pair and wrong for the hard one — 856041 pairs with
/// 856124, not 856042.
/// <para>
/// <b>Hard mode never reached the class at all.</b> 856041 was bound to <c>useSkillAndDie</c> with no
/// row in our npc skill data, and that AI deletes an npc with an empty skill list the instant it spawns.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatUpliftAiTests
{
	private const int DragonLordsRefuge = 300520000;

	/// <summary>The normal-mode uplift and the damage npc it drops.</summary>
	private const int Uplift = 283135;
	private const int UpliftDamage = 283136;

	/// <summary>The hard-mode pair, whose ids are not one apart.</summary>
	private const int HardUplift = 856041;
	private const int HardUpliftDamage = 856124;

	/// <summary>What adding one to the hard uplift's id would have reached.</summary>
	private const int NotTheDamageNpc = 856042;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatSkillHelperAI), typeof(UseSkillAndDieAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The normal uplift drops its damage npc a second and a half in.</b>
	/// </summary>
	[Fact]
	public void TheNormalUpliftDropsItsDamageNpc()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Uplift, 300f, 300f, 200f);

		Assert.Equal(0, Count(harness, UpliftDamage));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(1, Count(harness, UpliftDamage));
	}

	/// <summary>
	/// <b>And the hard uplift reaches for 856124, not the next id up.</b>
	/// </summary>
	/// <remarks>
	/// <b>What this pin can see is the absence, not the presence.</b> 856124 runs retail's
	/// <c>IDTiamat_Hard_Earthquake_02</c> — cast, then despawn — so it is bound to <c>useSkillAndDie</c>,
	/// and <b>it has no row in our npc skill data</b>, which makes that AI delete it inside its own
	/// spawn. So the damage npc cannot be counted however correctly it is placed.
	/// <para>
	/// What is asserted instead is that the two wrong npcs never appear: 856042, which id arithmetic
	/// would have reached, and 283136, the normal-mode damage npc that a shared table would have thrown
	/// into the hard fight. <c>audit_skilless_casters.py</c> lists 856124 as the outstanding data gap.
	/// </para>
	/// <para>
	/// <b>And that leaves the table itself unpinned on this path.</b> Reverting to <c>GetNpcId() + 1</c>
	/// survives every pin here: it is right for the normal pair, and on the hard one it reaches 856042,
	/// an id nothing answers to — which looks exactly like the correct npc self-deleting. Only a skill
	/// row for 856124 would tell the two apart.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheHardUpliftReachesForNeitherWrongNpc()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(HardUplift, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(0, Count(harness, NotTheDamageNpc));
		Assert.Equal(0, Count(harness, UpliftDamage));
	}

	/// <summary>
	/// <b>The hard uplift survives long enough to place anything.</b>
	/// </summary>
	/// <remarks>
	/// This is the pin for the binding itself. On <c>useSkillAndDie</c> with no skill row it was deleted
	/// inside its own spawn, so nothing it might have done could ever have run.
	/// </remarks>
	[Fact]
	public void TheHardUpliftIsNotDeletedOnArrival()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(HardUplift, 300f, 300f, 200f);

		Assert.Equal(1, Count(harness, HardUplift));
	}

	/// <summary>
	/// <b>The damage npc lasts retail's three seconds.</b>
	/// </summary>
	[Fact]
	public void TheDamageNpcLastsThreeSeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Uplift, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(1, Count(harness, UpliftDamage));

		// Placed at 1.5s with three seconds of life, so it is gone by five.
		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(0, Count(harness, UpliftDamage));
	}
}
