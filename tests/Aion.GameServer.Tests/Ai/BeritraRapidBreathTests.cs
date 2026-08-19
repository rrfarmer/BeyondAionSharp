using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Beritra's rapid-breath marks, which stood five metres below where retail puts them.
/// </summary>
/// <remarks>
/// Retail runs this from a control npc (<c>IDSeal_Skill_RapidBreath_CTRL</c>), not from Beritra: it
/// waits three seconds, picks one of four trios by a cascade of probability tests, spawns the three
/// marks and despawns itself. This port collapses the control npc away and spawns the trio directly.
/// <para>
/// Pinned as a table, not through a fight — the wave fires from <c>OnEndUseSkill</c>, which needs a
/// cast the harness cannot make. That is weaker than a behavioural pin: it fixes the marks and the
/// trios in place, and it would not notice if <c>HandlePulseWave</c> stopped reading them.
/// </para>
/// </remarks>
public sealed class BeritraRapidBreathTests
{
	/// <summary>
	/// <b>Every mark stands at 1755.</b> They were spawned at 1749.9.
	/// </summary>
	[Fact]
	public void EveryMarkStandsAtRetailsHeight()
	{
		Assert.Equal(8, Lv1HumanBeritraAI.Marks.Length);
		Assert.All(Lv1HumanBeritraAI.Marks, mark => Assert.Equal(1755f, mark.Z));
	}

	/// <summary>
	/// <b>And the eight are the eight retail names, in order.</b>
	/// </summary>
	/// <remarks>
	/// <c>BIDSeal_Skill_RapidBreath_Target_01</c>..<c>_08</c> are 855745..855752 consecutively, so the
	/// order is checkable: mark n is npc 855744+n.
	/// </remarks>
	[Fact]
	public void AndTheEightAreRetailsEightInOrder()
	{
		for (int i = 0; i < Lv1HumanBeritraAI.Marks.Length; i++)
			Assert.Equal(855745 + i, Lv1HumanBeritraAI.Marks[i].NpcId);
	}

	/// <summary>
	/// <b>The four trios are retail's four trios.</b>
	/// </summary>
	/// <remarks>
	/// Read mark by mark from <c>IDSeal_Skill_RapidBreath_CTRL</c>: 1/3/6 at twenty-five per cent, 2/5/7
	/// at thirty-three, 3/5/8 at fifty, 1/4/7 unguarded. A rotation among four trios is the failure this
	/// shape invites, and it would look right from any single pull.
	/// </remarks>
	[Theory]
	[InlineData(0, new[] { 1, 3, 6 })]
	[InlineData(1, new[] { 2, 5, 7 })]
	[InlineData(2, new[] { 3, 5, 8 })]
	[InlineData(3, new[] { 1, 4, 7 })]
	public void TheFourTriosAreRetailsFour(int set, int[] marks)
	{
		int[] oneBased = Lv1HumanBeritraAI.BreathSets[set].Select(i => i + 1).ToArray();
		Assert.Equal(marks, oneBased);
	}

	/// <summary>
	/// <b>And the picker walks them in retail's priority order.</b>
	/// </summary>
	/// <remarks>
	/// Retail is four rungs each carrying its own <c>test_probability</c>, tried highest priority first,
	/// which is a cascade of independent rolls rather than one weighted draw. Driven here with a stub
	/// that answers only the threshold being tested, so each set is reached by the rung that owns it.
	/// </remarks>
	[Theory]
	[InlineData(25, 0)]
	[InlineData(33, 1)]
	[InlineData(50, 2)]
	[InlineData(0, 3)]
	public void AndThePickerWalksThemInPriorityOrder(int answerTrue, int expectedSet)
	{
		int[] picked = Lv1HumanBeritraAI.PickBreathSet(percent => percent == answerTrue);

		Assert.Equal(Lv1HumanBeritraAI.BreathSets[expectedSet], picked);
	}

	/// <summary>
	/// <b>And when more than one rung would fire, the highest priority wins.</b>
	/// </summary>
	/// <remarks>
	/// The pin above cannot see the order at all: its stub makes exactly one threshold true, so any
	/// arrangement of the four rungs satisfies it — reversing them entirely passed. Priority only shows
	/// when several rungs would match at once, which is a roll low enough to satisfy all three tests.
	/// </remarks>
	[Fact]
	public void AndWhenSeveralWouldFireTheHighestPriorityWins()
	{
		Assert.Equal(Lv1HumanBeritraAI.BreathSets[0], Lv1HumanBeritraAI.PickBreathSet(_ => true));
	}

	/// <summary>
	/// <b>And a roll that satisfies none falls through to the unguarded trio.</b>
	/// </summary>
	[Fact]
	public void AndARollThatSatisfiesNoneFallsThrough()
	{
		Assert.Equal(Lv1HumanBeritraAI.BreathSets[3], Lv1HumanBeritraAI.PickBreathSet(_ => false));
	}
}
