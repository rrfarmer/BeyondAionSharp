using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Divine hisen's two stones, which never appeared.
/// </summary>
/// <remarks>
/// See <see cref="DivineHisenAI"/>. The data describing them was already correct — retail's own
/// coordinates, to two decimals — and had never run, because his <c>ai</c> was <c>aggressive</c> and
/// nothing reads <c>spawn_helpers.xml</c> for such an npc.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DivineHisenAiTests
{
	private const int KromedesTrial = 300230000;

	private const int Hisen = 216968;
	private const int RedStone = 282103;
	private const int BlueStone = 282104;

	private static (BossAiHarness, Npc, Player) Fighting()
	{
		BossAiHarness harness = BossAiHarness.For(KromedesTrial).WithWorldSize(2048)
			.WithAi(typeof(DivineHisenAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc hisen = harness.Spawn(Hisen, 360f, 174f, 147.38f);
		Player player = harness.SpawnPlayer(364f, 174f, 147.38f);
		harness.Engage(hisen, player);
		return (harness, hisen, player);
	}

	private static Npc? Of(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Both stones appear when the fight starts.</b>
	/// </summary>
	[Fact]
	public void BothStonesAppearWhenTheFightStarts()
	{
		(BossAiHarness harness, Npc hisen, Player player) = Fighting();
		using BossAiHarness _ = harness;

		Assert.NotNull(Of(harness, RedStone));
		Assert.NotNull(Of(harness, BlueStone));
	}

	/// <summary>
	/// <b>And each one stands on its own mark.</b>
	/// </summary>
	/// <remarks>
	/// Retail places them absolutely, not near him. A translation using <c>SpawnNear</c> would put two
	/// stones at his feet and satisfy a pin that only counted them — and the stones are room furniture
	/// the fight is built around, so where they stand is the mechanic.
	/// </remarks>
	[Fact]
	public void EachStoneStandsOnItsOwnMark()
	{
		(BossAiHarness harness, Npc hisen, Player player) = Fighting();
		using BossAiHarness _ = harness;

		Npc red = Of(harness, RedStone)!;
		Npc blue = Of(harness, BlueStone)!;

		Assert.Equal(DivineHisenAI.RedMark.X, red.GetX(), 1);
		Assert.Equal(DivineHisenAI.RedMark.Y, red.GetY(), 1);
		Assert.Equal(DivineHisenAI.BlueMark.X, blue.GetX(), 1);
		Assert.Equal(DivineHisenAI.BlueMark.Y, blue.GetY(), 1);
	}

	/// <summary>
	/// <b>Both go when he dies.</b> Retail's <c>on_die</c> despawns each group by name.
	/// </summary>
	[Fact]
	public void BothStonesGoWhenHeDies()
	{
		(BossAiHarness harness, Npc hisen, Player player) = Fighting();
		using BossAiHarness _ = harness;

		BossAiHarness.Kill(hisen, player);

		Assert.Null(Of(harness, RedStone));
		Assert.Null(Of(harness, BlueStone));
	}
}
