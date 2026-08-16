using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="EmpyreanLordAI"/>'s arrival spawns, translated from retail patterns
/// <c>Kaisinel_Avatar1</c>/<c>2</c>, <c>Markutan_Avatar1</c>/<c>2</c> and their
/// <c>IDTiamat_Hard_God*</c> twins (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Eight npc ids share this class across two difficulties and four roles. The class already split the
/// four roles for its casts; neither of the two NPCs an avatar places was placed at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class EmpyreanLordAiTests
{
	/// <summary>The Dragon Lord's Refuge, which is the map the class checks for Tiamat's id.</summary>
	private const int DragonLordsRefuge = 300520000;

	private const int KaisinelAvatarOne = 219488;
	private const int KaisinelAvatarTwo = 219489;
	private const int MarchutanAvatarOne = 219491;
	private const int MarchutanAvatarTwo = 219492;

	private const int HardKaisinelAvatarOne = 856020;
	private const int HardKaisinelAvatarTwo = 856021;
	private const int HardMarchutanAvatarOne = 856023;
	private const int HardMarchutanAvatarTwo = 856024;

	private const int KaisinelSpawnHeal = 283159;
	private const int MarchutanSpawnHeal = 283160;
	private const int KaisinelTeleport = 283175;
	private const int MarchutanTeleport = 283176;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(EmpyreanLordAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// The table, role by role and difficulty by difficulty. The eight retail patterns agree pair for
	/// pair, so a hard-mode avatar places exactly what its normal-mode twin does.
	/// </summary>
	[Theory]
	[InlineData(KaisinelAvatarOne, KaisinelSpawnHeal, 20, 7000)]
	[InlineData(HardKaisinelAvatarOne, KaisinelSpawnHeal, 20, 7000)]
	[InlineData(MarchutanAvatarOne, MarchutanSpawnHeal, 20, 7000)]
	[InlineData(HardMarchutanAvatarOne, MarchutanSpawnHeal, 20, 7000)]
	[InlineData(KaisinelAvatarTwo, KaisinelTeleport, 6, 0)]
	[InlineData(HardKaisinelAvatarTwo, KaisinelTeleport, 6, 0)]
	[InlineData(MarchutanAvatarTwo, MarchutanTeleport, 6, 0)]
	[InlineData(HardMarchutanAvatarTwo, MarchutanTeleport, 6, 0)]
	public void EachRolePlacesItsOwn(int avatar, int expected, int life, int delayMillis)
	{
		Assert.Equal(expected, EmpyreanLordAI.ArrivalSpawnFor(avatar, out int liveSeconds, out int delay));
		Assert.Equal(life, liveSeconds);
		Assert.Equal(delayMillis, delay);
	}

	/// <summary>An NPC that is not one of the eight places nothing rather than somebody else's.</summary>
	[Fact]
	public void AnUnlistedAvatarPlacesNothing()
	{
		Assert.Equal(0, EmpyreanLordAI.ArrivalSpawnFor(123456, out _, out _));
	}

	/// <summary>
	/// <b>The god does not appear immediately.</b> Retail hangs the spawn off a seven-second
	/// <c>set_idle_timer</c>, so the first avatar arrives alone and the god follows — a threshold-free
	/// beat that a port reading only the spawn action would have collapsed to nothing.
	/// </summary>
	[Fact]
	public void TheGodFollowsSevenSecondsBehindTheFirstAvatar()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(KaisinelAvatarOne, 500f, 500f, 400f);

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(0, Count(harness, KaisinelSpawnHeal));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(1, Count(harness, KaisinelSpawnHeal));
	}

	/// <summary>And it stands for twenty seconds, which is retail's <c>live_time</c>.</summary>
	[Fact]
	public void TheGodStandsForTwentySeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(MarchutanAvatarOne, 500f, 500f, 400f);

		// Placed at seven seconds, so its twenty run out at twenty-seven.
		harness.Clock.Advance(TimeSpan.FromSeconds(8));
		Assert.Equal(1, Count(harness, MarchutanSpawnHeal));

		harness.Clock.Advance(TimeSpan.FromSeconds(17));
		Assert.Equal(1, Count(harness, MarchutanSpawnHeal));

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(0, Count(harness, MarchutanSpawnHeal));
	}

	/// <summary>
	/// The second avatar arrives inside its effect rather than seven seconds later, and the effect
	/// lasts six seconds rather than twenty. Both halves of that differ from the first avatar's.
	/// </summary>
	[Fact]
	public void TheSecondAvatarArrivesInsideItsTeleport()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(KaisinelAvatarTwo, 500f, 500f, 400f);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(1, Count(harness, KaisinelTeleport));

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(1, Count(harness, KaisinelTeleport));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(0, Count(harness, KaisinelTeleport));
	}

	/// <summary>
	/// <b>Each god's own, and never the other's.</b> Four roles across two gods is four chances for a
	/// hardcoded id, which is how the twin protectors' hellfire wave went wrong.
	/// </summary>
	/// <param name="lookAfter">
	/// Past the role's own delay and inside its own lifetime — the first avatar's god arrives at seven
	/// seconds and the second's effect is gone by six, so one window cannot serve both.
	/// </param>
	[Theory]
	[InlineData(KaisinelAvatarOne, KaisinelSpawnHeal, MarchutanSpawnHeal, 8)]
	[InlineData(MarchutanAvatarOne, MarchutanSpawnHeal, KaisinelSpawnHeal, 8)]
	[InlineData(KaisinelAvatarTwo, KaisinelTeleport, MarchutanTeleport, 1)]
	[InlineData(MarchutanAvatarTwo, MarchutanTeleport, KaisinelTeleport, 1)]
	public void NeitherGodPlacesTheOthers(int avatar, int expected, int theOtherGods, int lookAfter)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(avatar, 500f, 500f, 400f);

		harness.Clock.Advance(TimeSpan.FromSeconds(lookAfter));

		Assert.Equal(1, Count(harness, expected));
		Assert.Equal(0, Count(harness, theOtherGods));
	}

	/// <summary>
	/// Placed where the avatar stands, which is retail's <c>SPAWN_LOCATION_MY_POINT</c> — the god
	/// appears beside the avatar that called it rather than on a mark.
	/// </summary>
	[Fact]
	public void TheGodAppearsWhereItsAvatarStands()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(KaisinelAvatarOne, 640f, 480f, 400f);

		harness.Clock.Advance(TimeSpan.FromSeconds(8));

		Npc god = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == KaisinelSpawnHeal));
		Assert.Equal(640f, god.GetX(), 1);
		Assert.Equal(480f, god.GetY(), 1);
	}
}
