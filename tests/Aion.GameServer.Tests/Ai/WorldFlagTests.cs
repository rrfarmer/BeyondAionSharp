using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The two properties that make a world flag different from a per-npc one: <b>every npc in an instance
/// shares it, and no two instances share it.</b>
/// </summary>
/// <remarks>
/// Retail uses these to let one npc arm a step a different npc later takes — the sealed akaimum sets a
/// flag when it stands a fallen guard back up, and the silikor consumes that flag when a neutral caster
/// spells it. <b>Neither property is testable through an encounter yet</b>, because nothing in this port
/// sends that pair, so they are pinned directly against the store.
/// <para>
/// The isolation pin is the one worth having. A world flag scoped to the server rather than the instance
/// would pass every sharing test and still be wrong in the only way that matters: <b>one group's progress
/// arming another group's mechanic</b>, which no encounter pin written inside a single instance can see.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class WorldFlagTests
{
	private const int Flag = 3;

	private const int Lab = 310110000;

	/// <summary>
	/// A real instance, taken from a harness rather than constructed. Building one directly needs the
	/// data manager, and an instance that came from the world is the thing the guards will actually be
	/// handed at runtime.
	/// </summary>
	private static WorldMapInstance Instance(List<BossAiHarness> keep)
	{
		BossAiHarness harness = BossAiHarness.For(Lab).WithWorldSize(1024)
			.WithAi(typeof(Aion.GameServer.Handlers.AI.SilikorGuardAI),
				typeof(Aion.GameServer.Handlers.AI.AggressiveNpcAI)).Build();
		keep.Add(harness);
		return harness.Spawn(280971, 300f, 300f, 200f).GetWorldMapInstance();
	}

	/// <summary><b>The first caller wins and the rest lose</b> — a test-and-set, shared.</summary>
	[Fact]
	public void OnlyTheFirstCallerInAnInstanceSetsIt()
	{
		List<BossAiHarness> keep = [];
		WorldMapInstance instance = Instance(keep);

		Assert.True(WorldFlags.TestAndSet(instance, Flag));
		Assert.False(WorldFlags.TestAndSet(instance, Flag));
		Assert.False(WorldFlags.TestAndSet(instance, Flag));
	}

	/// <summary>
	/// <b>And a second instance is not affected.</b> The property a server-wide store would break.
	/// </summary>
	[Fact]
	public void TwoInstancesDoNotShareAFlag()
	{
		List<BossAiHarness> keep = [];
		WorldMapInstance one = Instance(keep);
		WorldMapInstance other = Instance(keep);

		Assert.True(WorldFlags.TestAndSet(one, Flag));

		Assert.False(WorldFlags.IsSet(other, Flag));
		Assert.True(WorldFlags.TestAndSet(other, Flag));
	}

	/// <summary>
	/// <b>What one npc sets, another consumes.</b> Written as two calls against one instance because that
	/// is exactly what the akaimum and the silikor will do: neither reads its own flag.
	/// </summary>
	[Fact]
	public void OneSetterArmsADifferentConsumer()
	{
		List<BossAiHarness> keep = [];
		WorldMapInstance instance = Instance(keep);

		Assert.False(WorldFlags.TestAndUnset(instance, Flag));

		WorldFlags.TestAndSet(instance, Flag);

		Assert.True(WorldFlags.TestAndUnset(instance, Flag));
		Assert.False(WorldFlags.TestAndUnset(instance, Flag));
	}

	/// <summary><c>is_world_flag_var</c> reads without touching, unlike every other way in.</summary>
	[Fact]
	public void ReadingDoesNotChangeTheAnswer()
	{
		List<BossAiHarness> keep = [];
		WorldMapInstance instance = Instance(keep);
		WorldFlags.TestAndSet(instance, Flag);

		Assert.True(WorldFlags.IsSet(instance, Flag));
		Assert.True(WorldFlags.IsSet(instance, Flag));
		Assert.True(WorldFlags.TestAndUnset(instance, Flag));
	}

	/// <summary>Slots are independent, so a pattern using several does not smear them together.</summary>
	[Fact]
	public void SlotsAreIndependent()
	{
		List<BossAiHarness> keep = [];
		WorldMapInstance instance = Instance(keep);

		WorldFlags.TestAndSet(instance, 0);

		Assert.True(WorldFlags.IsSet(instance, 0));
		Assert.False(WorldFlags.IsSet(instance, 1));
		Assert.False(WorldFlags.IsSet(instance, WorldFlags.Slots - 1));
	}
}
