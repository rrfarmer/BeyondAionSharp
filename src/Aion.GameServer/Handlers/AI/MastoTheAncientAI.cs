using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Masto the Ancient of Brusthonin. Retail pattern <c>ND2_EhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Five health bands, and what changes across them
/// is how often he throws his target away.</b> Thirty-one skills are blocked and none of that matters
/// to the shape: the pattern is a scatter cadence, and the cadence is the fight.
/// <para>
/// <b>Above eighty he settles.</b> He scatters once, six seconds in, when the opening timer fires — and
/// then the only branch his band has is a skill on a fifteen-second timer with no switch attached. A
/// tank holds him.
/// </para>
/// <para>
/// <b>From eighty down he does not.</b> Each of the next three bands has two branches that scatter: the
/// opener, once, when the band is entered, and a repeat on the band's own timer — twenty-five seconds
/// at 61–80, thirty at 41–60, twenty-five at 21–40. So the middle of the fight is a boss who will not
/// be held.
/// </para>
/// <para>
/// <b>And below twenty he stops scattering altogether and turns on the off-tank.</b> The bottom band's
/// opener switches to <c>ATTACKERI_SECOND_HATING</c> rather than a random attacker, and the
/// thirty-second timer that carries the band afterwards has no switch at all. He picks the
/// second-most-hated player once and then holds them to the end.
/// </para>
/// <para>
/// <b>One difference from the other bands that turns out not to matter, recorded because it reads as
/// though it should.</b> The bottom opener is the only one that does not re-arm the opener timer, which
/// looks like the mechanism that stops the scattering. It is not: the opener timer's own fallback
/// branch has no switch either, so whether that timer keeps running or dies, nothing moves him. A
/// mutation adding the re-arm back changes no pin, and that is correct rather than a gap in the pins —
/// <b>the band's own flag is what ends the scattering, and the missing re-arm is inert.</b> Kept as
/// retail writes it.
/// </para>
/// <para>
/// <b>Health of exactly twenty belongs to no band</b> — the bottom guard is <c>lower_than 20</c> and the
/// one above is <c>larger_than 21</c>. **Third boss in three entries to carry that hole**, after
/// Guardian Vingeveu and Chaoslord Kalabar. It is not a slip in one pattern; it is how NCSoft writes a
/// banded ladder, and it is kept every time.
/// </para>
/// <para>
/// <b>Not translated:</b> thirty-one skills, which is every <c>use_skill</c> and
/// <c>use_skill_by_attacker_indicator</c> in the pattern, including the lowest-health targeting that
/// makes the bottom band vicious. <b>The band structure is what survives, and the band structure is
/// what a raid plans around.</b> Retail's <c>points_to_add=100</c> on each switch is dropped, as it has
/// been since the Anuhart casters — <c>SwitchTarget</c> does not carry a payload.
/// </para>
/// <para>
/// <b>And one dead action:</b> <c>on_enter_idle_state</c> sets <c>FLAGVARI_ZETA_5</c>, which no branch
/// reads — the same dead flag Chaoslord Kalabar carries, in the same slot.
/// </para>
/// </remarks>
[AIName("masto_the_ancient")]
public class MastoTheAncientAI : PatternAi
{
	// The opening timer, the timer every band's opener rides, and one timer per band.
	private const int Opening = 0;
	private const int BandOpener = 1;
	private const int TopBandTimer = 2;
	private const int SecondBandTimer = 3;
	private const int ThirdBandTimer = 5;
	private const int FourthBandTimer = 6;
	private const int BottomBandTimer = 8;

	// One flag per band below the top, which has no opener of its own.
	private const int OpenedSecond = 2;
	private const int OpenedThird = 3;
	private const int OpenedFourth = 4;
	private const int OpenedBottom = 5;

	/// <summary>Retail's <c>is_hp_lower_than percent=20</c>, against the band above it at <c>21</c>.</summary>
	private const int Bottom = 20;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(16, "engaging", [], Do.ArmTimer(Opening, 6_000))),

		OnBattleTimer = Of(
			// Bottom band. Its repeat carries no switch, and its opener does not re-arm the opener
			// timer -- see the class remarks. Both are retail's, and together they are why he settles.
			Branch(14, "bottom band, keeping its own timer",
				[When.Timer(BottomBandTimer), When.HpBelow(Bottom)],
				Do.ArmTimer(BottomBandTimer, 30_000)),

			Branch(13, "bottom band, opening it: the off-tank",
				[When.Timer(BandOpener), When.HpBelow(Bottom), When.FirstTime(OpenedBottom)],
				Do.ArmTimer(BottomBandTimer, 30_000),
				Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

			Branch(11, "fourth band, keeping it up",
				[When.HpBetween(21, 40), When.Timer(FourthBandTimer)],
				Do.ArmTimer(FourthBandTimer, 25_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(10, "fourth band, opening it",
				[When.HpBetween(21, 40), When.Timer(BandOpener), When.FirstTime(OpenedFourth)],
				Do.ArmTimer(BandOpener, 8_000),
				Do.ArmTimer(FourthBandTimer, 20_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(9, "third band, keeping it up",
				[When.HpBetween(41, 60), When.Timer(ThirdBandTimer)],
				Do.ArmTimer(ThirdBandTimer, 30_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(7, "third band, opening it",
				[When.HpBetween(41, 60), When.Timer(BandOpener), When.FirstTime(OpenedThird)],
				Do.ArmTimer(BandOpener, 8_000),
				Do.ArmTimer(ThirdBandTimer, 20_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(6, "second band, keeping it up",
				[When.Timer(SecondBandTimer), When.HpBetween(61, 80)],
				Do.ArmTimer(SecondBandTimer, 25_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(4, "second band, opening it",
				[When.Timer(BandOpener), When.HpBetween(61, 80), When.FirstTime(OpenedSecond)],
				Do.ArmTimer(BandOpener, 8_000),
				Do.ArmTimer(SecondBandTimer, 20_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			// The top band's only branch. No switch: this is the band a tank can hold.
			Branch(3, "top band, and nothing but a skill",
				[When.Timer(TopBandTimer), When.HpBetween(81, 100)],
				Do.ArmTimer(TopBandTimer, 15_000)),

			Branch(2, "the opening scatter",
				[When.Timer(Opening)],
				Do.ArmTimer(BandOpener, 8_000),
				Do.ArmTimer(TopBandTimer, 7_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(1, "the opener timer, waiting for a band",
				[When.Timer(BandOpener)],
				Do.ArmTimer(BandOpener, 6_000))),
	};

	public MastoTheAncientAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
