using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Spiritmaster atmach (214843), who summoned nothing at all. Retail pattern <c>Naga_WhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He ran plain <c>aggressive</c>, and the
/// <c>spawn_helpers.xml</c> block that looked like his implementation was never read by anything — and
/// named two npcs (214832, 215226) that appear nowhere in his retail pattern.
/// <para>
/// <b>What he actually does.</b> A trap at his feet the moment the fight starts, and <b>two faithful
/// underlings once</b> when he first crosses 35%, three metres out, for half an hour. The underlings are
/// filed under retail's <c>SPAWN_ID_2</c> and cleared both when he dies and when he resets; the trap is
/// <c>SPAWN_ID_1</c> and is cleared by neither.
/// </para>
/// <para>
/// <b>The heartbeat is a three-band ladder on one timer.</b> Timer 0 comes round every six seconds and
/// the band it lands in decides which secondary timer is lit — 1 above 76, 2 between 37 and 74, 3 below
/// 35 — each of them a one-shot handover guarded by its own flag. The last rung takes timer 0 with no
/// band at all, so the heartbeat keeps running once every handover is spent. All of that is translated;
/// what hangs off the secondary timers is not.
/// </para>
/// <para>
/// <b>Not translated:</b> ten skill indices (0 through 9), which this port cannot resolve to skill ids,
/// and with them the <c>on_spelled</c> reprisal — a 10% roll against a player caster, guarded on
/// <c>is_skill_count_left</c>, for which there is no vocabulary here either. Also the shout and
/// <c>broadcast_message</c> 3309, which nothing in this tree answers.
/// </para>
/// </remarks>
[AIName("spiritmaster_atmach")]
public class SpiritmasterAtmachAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c>: the trap, which nothing clears.</summary>
	private const int Trap = 1;

	/// <summary>Retail's <c>SPAWN_ID_2</c>: the underlings, cleared on death and on reset.</summary>
	private const int Underlings = 2;

	/// <summary>Retail <c>BIDLF1_Dragon_G4NFrRain_A_50_An</c>, which runs <c>NTrap_A</c>.</summary>
	private const int FrostRain = 281246;

	/// <summary>Retail <c>BD3_Naga_SubLiz_44_Ae</c> — "faithful underling".</summary>
	private const int FaithfulUnderling = 280645;

	private const float UnderlingRing = 3f;
	private const int UnderlingLife = 1800;

	/// <summary>Retail's <c>ALPHA_3</c>, <c>ALPHA_2</c> and <c>ZETA_4</c>: the three band handovers.</summary>
	private const int BelowThirtyFive = 3;
	private const int MidBand = 2;
	private const int TopBand = 4;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(10, "the trap, and the heartbeat", When.Always,
				Do.ArmTimer(0, 6000),
				Do.ArmTimer(1, 17000),
				Do.SpawnNear(FrostRain, Trap))),

		OnBattleTimer = Of(
			// Below thirty-five. The handover is what brings the underlings, and it fires once.
			Branch(8, "", [When.Timer(3), When.HpBelow(35)],
				Do.ArmTimer(3, 15000)),

			Branch(7, "crossing thirty-five brings two underlings",
				[When.Timer(0), When.HpBelow(35), When.FirstTime(BelowThirtyFive)],
				Do.ArmTimer(0, 6000),
				Do.ArmTimer(3, 15000),
				Do.SpawnNear(FaithfulUnderling, Underlings, count: 2, range: UnderlingRing,
					liveSeconds: UnderlingLife)),

			// The middle band. is_hp_in_boundary is exclusive at both ends, so 36..75 is 37 to 74.
			Branch(6, "", [When.Timer(2), When.HpBetween(37, 74)],
				Do.ArmTimer(2, 17000)),

			Branch(5, "crossing seventy-five", [When.Timer(0), When.HpBetween(37, 74), When.FirstTime(MidBand)],
				Do.ArmTimer(0, 6000),
				Do.ArmTimer(2, 17000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(4, "", [When.Timer(1), When.HpBetween(77, 99), When.FirstTime(TopBand)],
				Do.ArmTimer(1, 17000)),

			Branch(3, "", [When.Timer(1), When.HpBetween(77, 99)],
				Do.ArmTimer(1, 17000)),

			// The heartbeat itself, last, so every band above gets first refusal on timer 0.
			Branch(2, "", [When.Timer(0)],
				Do.ArmTimer(0, 6000))),

		// Retail clears only the underlings, on both endings. The trap outlives him either way.
		OnLeaveAttack = Of(
			Branch(9, "", When.Always,
				Do.Despawn(Underlings))),

		OnDie = Of(
			Branch(11, "", When.Always,
				Do.Despawn(Underlings))),
	};

	public SpiritmasterAtmachAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
