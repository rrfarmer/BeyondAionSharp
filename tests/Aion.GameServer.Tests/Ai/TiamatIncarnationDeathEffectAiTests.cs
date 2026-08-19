using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Killing an incarnation is what removes its drakan mage — and it removed none.
/// </summary>
/// <remarks>
/// Each incarnation leaves a mark when it dies. All four retail patterns are three lines and identical
/// in shape: cast a dispel, <b>broadcast at ninety-nine metres</b>, despawn. Each message is answered by
/// exactly one balaur spiritualist, which despawns on hearing it.
/// <para>
/// This port placed all four marks on the right deaths and for retail's six seconds, but bound them to
/// <c>general</c> — so they were scenery, and <b>every mage stayed in the room however many
/// incarnations the raid killed</b>.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatIncarnationDeathEffectAiTests
{
	private const int DragonLordsRefuge = 300520000;

	/// <summary>The four marks, and the mage each one dismisses.</summary>
	private const int FissurefangMark = 283063;
	private const int PetriscaleMark = 283064;
	private const int GraviwingMark = 283065;
	private const int WrathclawMark = 283066;

	private const int MageOne = 283163;
	private const int MageTwo = 283164;
	private const int MageThree = 283165;
	private const int MageFour = 283166;

	/// <summary>The hard-mode twin of the first mage, which shares its mark.</summary>
	private const int HardMageOne = 856483;

	private static readonly int[] AllMages = [MageOne, MageTwo, MageThree, MageFour];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatIncarnationDeathEffectAI), typeof(AggressiveNoLootNpcAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Each mark dismisses its own mage and leaves the other three standing.</b>
	/// </summary>
	/// <remarks>
	/// The pairing is the whole mechanic and it is not derivable: the marks run 283063-283066 and the
	/// mages 283163-283166, but 283064 answers to mage three and 283065 to mage four — the order is
	/// retail's message numbers, not the id order.
	/// </remarks>
	[Theory]
	[InlineData(FissurefangMark, MageOne)]
	[InlineData(PetriscaleMark, MageThree)]
	[InlineData(GraviwingMark, MageFour)]
	[InlineData(WrathclawMark, MageTwo)]
	public void AMarkDismissesItsOwnMageAndNoOther(int mark, int mage)
	{
		using BossAiHarness harness = NewHarness();
		foreach (int each in AllMages)
			harness.Spawn(each, 500f, 500f, 417f);

		harness.Spawn(mark, 505f, 500f, 417f);

		Assert.Equal(0, Count(harness, mage));
		foreach (int other in AllMages)
			if (other != mage)
				Assert.Equal(1, Count(harness, other));
	}

	/// <summary>
	/// <b>A mark reaches the hard-mode mage too</b>, which shares its message.
	/// </summary>
	[Fact]
	public void AMarkAlsoDismissesTheHardModeMage()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(HardMageOne, 500f, 500f, 417f);

		harness.Spawn(FissurefangMark, 505f, 500f, 417f);

		Assert.Equal(0, Count(harness, HardMageOne));
	}

	/// <summary>
	/// <b>And a mage past retail's ninety-nine metres does not hear it.</b>
	/// </summary>
	[Fact]
	public void AMageBeyondEarshotStays()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(MageOne, 500f, 500f, 417f);

		// A hundred and fifty metres away, which is past the broadcast.
		harness.Spawn(FissurefangMark, 650f, 500f, 417f);

		Assert.Equal(1, Count(harness, MageOne));
	}
}
