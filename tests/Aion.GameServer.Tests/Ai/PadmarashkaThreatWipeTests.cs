using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Padmarashka's threat wipes, which she never did.
/// </summary>
/// <remarks>
/// Retail's <c>reset_hatepoints</c> sits on three of her health-guarded rungs — the death vortex at
/// fifty-one and again at thirty-one, and the enrage at nineteen. She held her tank from the pull to
/// the floor instead, which removes the only thing those rungs are for.
/// <para>
/// Found by <c>audit_hp_phases.py</c>: <c>ours [95, 50, 25]</c> against
/// <c>retail [71, 51, 41, 31, 21, 19]</c>, no threshold in common.
/// </para>
/// <para>
/// <b>Pinned as a table, and that is weaker than this file would like.</b> The behaviour cannot be
/// observed in the harness at all: the stand-in player deals no real damage, so she leaves combat
/// within a few seconds of engaging and her aggro list is emptied — hate added by hand does not survive
/// to be measured, let alone across a seven-second wipe. What is fixed in place is which thresholds
/// carry the wipe, how long each waits, and that all three are thresholds she really crosses. That the
/// wipe then zeroes hate rather than emptying the list is held by review.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PadmarashkaThreatWipeTests
{
	private const int PadmarashkasCave = 300230000;
	private const int Padmarashka = 218756;

	/// <summary>
	/// <b>Three thresholds wipe threat, and they are retail's three.</b>
	/// </summary>
	/// <remarks>
	/// 51 and 31 are the death-vortex rungs, 19 the enrage. Nothing else does — 95, 50 and 25 are this
	/// port's own staging, and a wipe written on every threshold would look the same as retail's three
	/// without the negative half below.
	/// </remarks>
	[Theory]
	[InlineData(PadmarashkaCaveAI.FirstWipePercent, true)]
	[InlineData(PadmarashkaCaveAI.SecondWipePercent, true)]
	[InlineData(PadmarashkaCaveAI.EnrageWipePercent, true)]
	[InlineData(95, false)]
	[InlineData(50, false)]
	[InlineData(25, false)]
	public void ThreeThresholdsWipeThreatAndTheyAreRetailsThree(int phase, bool wipes)
	{
		Assert.Equal(wipes, PadmarashkaCaveAI.WipesThreatAt(phase));
	}

	/// <summary>
	/// <b>The vortex wipes trail their threshold by seven seconds; the enrage wipe does not.</b>
	/// </summary>
	/// <remarks>
	/// Retail's primal-fear rung arms <c>BTIMERI_INDEX_2</c> at 7000 and the vortex rung carrying the
	/// wipe fires on that, so it lands after the threshold rather than on it. The enrage rung carries
	/// the wipe directly.
	/// </remarks>
	[Theory]
	[InlineData(PadmarashkaCaveAI.FirstWipePercent, 7000L)]
	[InlineData(PadmarashkaCaveAI.SecondWipePercent, 7000L)]
	[InlineData(PadmarashkaCaveAI.EnrageWipePercent, 0L)]
	public void TheVortexWipesTrailTheirThresholdBySevenSeconds(int phase, long delay)
	{
		Assert.Equal(delay, PadmarashkaCaveAI.WipeDelayFor(phase));
	}

	/// <summary>
	/// <b>And all three are rungs on her ladder.</b>
	/// </summary>
	/// <remarks>
	/// A wipe keyed on a number absent from <c>HpPhases</c> would satisfy both pins above and never
	/// fire — which is exactly what the first version of this pin missed: it walked her health down and
	/// asked the table, so any number in range looked "reached". This joins the table to the ladder that
	/// reaches it.
	/// </remarks>
	[Fact]
	public void AndAllThreeAreRungsOnHerLadder()
	{
		int[] wipes = PadmarashkaCaveAI.PhaseThresholds
			.Where(PadmarashkaCaveAI.WipesThreatAt).ToArray();

		Assert.Equal([PadmarashkaCaveAI.FirstWipePercent, PadmarashkaCaveAI.SecondWipePercent,
			PadmarashkaCaveAI.EnrageWipePercent], wipes);
	}
}
