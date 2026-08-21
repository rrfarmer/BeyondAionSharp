using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="SealWaveAttackerAI"/> — Drakenspire Depths' wave conversation
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>The whole mechanic is one npc talking and another answering,</b> so almost every pin here is
/// written as a pair: the speaker is wounded into a band, and a second npc is asked whether it heard.
/// Asserting on the broadcast itself would pin the call and not the conversation, and the conversation
/// is what was missing.
/// <para>
/// <b>The peel-off is the only answer inside this family,</b> which makes it the microscope for
/// everything upstream of it — the band, the <c>is_user</c> guard, the range, and the tribe filter all
/// show up as the tank either turning or not turning. A pin that fails here has to be read carefully
/// for which of the four it is actually measuring.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SealWaveAttackerAiTests
{
	private const int Reshanta = 400010000;

	/// <summary><c>IDSeal_Wave_Group1_Fi</c>, tribe <c>IDSEAL_WAVE_TANKER</c> — the only one that peels off.</summary>
	private const int Tank = 236204;

	/// <summary><c>BIDSeal_Wave_Pr</c>, tribe <c>IDSEAL_WAVE_HEALER</c> — the only call the tank answers.</summary>
	private const int Healer = 855847;

	/// <summary><c>IDSeal_Wave_Group1_As</c>. Broadcasts the same 22755 and is ignored.</summary>
	private const int Assassin = 236205;

	/// <summary><c>IDSeal_Forward_Guard_Li_Fi</c> — the npc the aionemu class hated by id.</summary>
	private const int ForwardGuard = 236248;

	/// <summary><c>IDSeal_Wave_Arrow_Target</c> — the mark the ranged leader shells.</summary>
	private const int ArrowTarget = 855923;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(SealWaveAttackerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The healer's call takes the tank off whoever it names.</b> The whole chain in one pin: the
	/// healer crosses seventy, calls with the player's name attached, and the tank — which is on that
	/// same player — goes somewhere else.
	/// </summary>
	[Fact]
	public void TheHealersCallTakesTheTankOffWhoeverItNames()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(healer, hunted);
		BossAiHarness.SetHpPercent(healer, 60);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(other, tank.GetTarget());
	}

	/// <summary>
	/// <b>The assassin says the same word and nothing happens.</b> 22755 is broadcast by two of the five
	/// classes and only the healer's is answered; <c>tribe_name</c> is the entire difference, and without
	/// <see cref="AiPattern.When.SenderTribe"/> this pin and the one above would both pass on a pattern
	/// that was wrong.
	/// </summary>
	[Fact]
	public void TheAssassinSaysTheSameWordAndNothingHappens()
	{
		using BossAiHarness harness = NewHarness();
		Npc assassin = harness.Spawn(Assassin, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(assassin, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(assassin, hunted);
		BossAiHarness.SetHpPercent(assassin, 60);
		assassin.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(hunted, tank.GetTarget());
	}

	/// <summary>
	/// <b>A tank on somebody else keeps hold of them.</b> The call names one player, and a hearer fighting
	/// a different one has no reason to move — which is what <c>is_my_curent_target</c> is for, and the
	/// difference between a call-out and a room-wide scatter.
	/// </summary>
	[Fact]
	public void ATankOnSomebodyElseKeepsHoldOfThem()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, other);
		BossAiHarness.Rehate(tank, hunted);
		harness.Engage(healer, hunted);
		BossAiHarness.SetHpPercent(healer, 60);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(other, tank.GetTarget());
	}

	/// <summary>
	/// <b>Above seventy nobody calls.</b> The bands are the mechanic; a wave that shouted from full health
	/// would be a wave that scattered the tanks the moment it was touched.
	/// </summary>
	[Fact]
	public void AboveSeventyNobodyCalls()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(healer, hunted);
		BossAiHarness.SetHpPercent(healer, 85);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(hunted, tank.GetTarget());
	}

	/// <summary>
	/// <b>An NPC's blow does not set the wave shouting.</b> Retail guards every band on <c>is_user</c>,
	/// and the reason is standing right there in the room: the raid's own forward guards fight the wave,
	/// and without the guard that brawl alone would drive the calls.
	/// </summary>
	/// <remarks>
	/// <b>Written first with the tank holding a player,</b> and it survived the mutation that deletes the
	/// guard — because dropping <c>is_user</c> makes the healer call and name <em>the forward guard</em>,
	/// and a tank fighting a player is not on the npc the message names, so the peel-off correctly does
	/// nothing either way. The pin measured the wrong half of the branch.
	/// <para>
	/// So the tank is put <b>on the guard</b> here. Now the name in the message is the tank's own target,
	/// the peel-off has something to do, and the only thing standing between the brawl and a scattered
	/// room is the <c>is_user</c> guard itself.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnNpcsBlowDoesNotSetTheWaveShouting()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Npc guard = harness.Spawn(ForwardGuard, 301f, 300f, 200f);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, guard);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(healer, guard);
		BossAiHarness.SetHpPercent(healer, 60);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, guard);

		Assert.Same(guard, tank.GetTarget());
	}

	/// <summary>
	/// <b>Each band calls once.</b> Retail's two flags are shared between the melee and the spell handler,
	/// so an attacker being hit and cast at in the same band still calls once — and the second pin below
	/// is what tells a shared flag apart from a per-handler one.
	/// </summary>
	[Fact]
	public void EachBandCallsOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(healer, hunted);
		BossAiHarness.SetHpPercent(healer, 60);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);
		Assert.Same(other, tank.GetTarget());

		// Put the tank back on the named player and hit the healer again in the same band.
		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(hunted, tank.GetTarget());
	}

	/// <summary>
	/// <b>Crossing forty calls again.</b> The lower band has its own flag, so the same npc that already
	/// called at seventy still has one more in it.
	/// </summary>
	[Fact]
	public void CrossingFortyCallsAgain()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(healer, hunted);
		BossAiHarness.SetHpPercent(healer, 60);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		BossAiHarness.SetHpPercent(healer, 30);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(other, tank.GetTarget());
	}

	/// <summary><b>Every one of the eight wave-end numbers clears the room.</b></summary>
	[Theory]
	[InlineData(22764)]
	[InlineData(22765)]
	[InlineData(22766)]
	[InlineData(22767)]
	[InlineData(22768)]
	[InlineData(22769)]
	[InlineData(22770)]
	[InlineData(22771)]
	public void EveryOneOfTheEightWaveEndNumbersClearsTheRoom(int over)
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc attacker = harness.Spawn(Tank, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(crier, attacker);

		NpcMessageBus.Broadcast(crier, over, null, 100f);

		Assert.DoesNotContain(attacker, harness.LiveNpcs());
	}

	/// <summary>
	/// <b>The leaders' command buff reaches every attacker that hears it.</b> Retail's rung is
	/// unguarded — hear 22750, cast <c>SKILLI_INDEX_0</c> on yourself — and index 0 is the same skill
	/// for all 22 wave attackers, so one id pins the lot.
	/// </summary>
	[Theory]
	[InlineData(236204)]  // LeaderGourp-less Fi, priority 20
	[InlineData(236205)]  // As
	[InlineData(236206)]  // Ra
	[InlineData(236207)]  // Wi
	[InlineData(855847)]  // Pr
	[InlineData(236216)]  // LeaderGourp_Fi, still 20
	[InlineData(236217)]  // LeaderGourp_As
	[InlineData(236220)]  // LeaderGourp_Pr, retail's priority 12
	[InlineData(236219)]  // LeaderGourp_Wi, retail's priority 10
	public void TheCommandBuffReachesEveryAttackerThatHearsIt(int attackerId)
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc attacker = harness.Spawn(attackerId, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(leader, attacker);

		NpcMessageBus.Broadcast(leader, SealWaveAttackerAI.CommandBuff, null, 100f);

		Assert.Contains(BossAiHarness.DrainQueuedSkills(attacker),
			cast => cast.SkillId == SealWaveAttackerAI.CommandBuffSkill);
	}

	/// <summary>
	/// <b>236218 is the one attacker that does not hear it.</b> It runs <c>LeaderGourp_Ra</c>, whose
	/// number is 22753 rather than 22750, and this class used to hand it the same pattern as 236219.
	/// Without this pin the theory above would pass on a class that buffed all ten.
	/// </summary>
	[Fact]
	public void TheRangedLeaderDoesNotHearTheCommandBuff()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc archer = harness.Spawn(236218, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(leader, archer);

		NpcMessageBus.Broadcast(leader, SealWaveAttackerAI.CommandBuff, null, 100f);

		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(archer),
			cast => cast.SkillId == SealWaveAttackerAI.CommandBuffSkill);
	}

	/// <summary>
	/// <b>The buff is not cast at anything that speaks.</b> 22749 is one below it and is nobody's
	/// number; this keeps the rung answering its own message rather than every message.
	/// </summary>
	[Fact]
	public void ANumberBesideTheBuffDoesNotSetAnybodyCasting()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc attacker = harness.Spawn(Tank, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(leader, attacker);

		NpcMessageBus.Broadcast(leader, 22749, null, 100f);

		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(attacker),
			cast => cast.SkillId == SealWaveAttackerAI.CommandBuffSkill);
	}

	/// <summary>
	/// <b>The arrow target calls, and 236218 fires both shells at it.</b> Retail's priority-10 rung:
	/// two casts, both at the caller rather than at whatever the archer is fighting.
	/// </summary>
	/// <remarks>
	/// <b>Nothing here broadcasts anything.</b> The mark is spawned and that is all — its own
	/// <c>on_wake_up</c> shouts 22753, which is the half that did not exist until the extractors
	/// stopped refusing its pattern. A version of this pin that called <c>NpcMessageBus.Broadcast</c>
	/// by hand passed just as well and proved only half the loop.
	/// </remarks>
	[Fact]
	public void TheRangedLeaderShellsWhateverCallsForBombardment()
	{
		using BossAiHarness harness = NewHarness();
		Npc archer = harness.Spawn(236218, 300f, 300f, 200f);
		Npc mark = harness.Spawn(ArrowTarget, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(archer, mark);

		var fired = BossAiHarness.DrainQueuedSkills(archer);
		Assert.Contains(fired, cast => cast.SkillId == SealWaveAttackerAI.BombardmentSnare);
		Assert.Contains(fired, cast => cast.SkillId == SealWaveAttackerAI.BombardmentShell);
	}

	/// <summary>
	/// <b>Nobody else answers the mark.</b> 22753 belongs to the ranged leader alone; the tank hearing
	/// it too would put the whole wave on the marker instead of on the raid. Same spawn-and-wait as
	/// above: the mark shouts for itself.
	/// </summary>
	[Fact]
	public void TheRestOfTheWaveIgnoresTheMark()
	{
		using BossAiHarness harness = NewHarness();
		Npc tank = harness.Spawn(Tank, 300f, 300f, 200f);
		Npc mark = harness.Spawn(ArrowTarget, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(tank, mark);

		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(tank),
			cast => cast.SkillId == SealWaveAttackerAI.BombardmentShell);
	}

	/// <summary>
	/// <b>The shell ends the mark.</b> 17315 is both the archer's second cast and the skill the
	/// marker's own <c>on_spelled</c> despawns on, so the loop closes instead of leaving the mark
	/// standing until it decays. This is the half that needed <c>is_event_skill_id</c>.
	/// </summary>
	[Fact]
	public void TheShellTakesTheMarkAway()
	{
		using BossAiHarness harness = NewHarness();
		Npc archer = harness.Spawn(236218, 300f, 300f, 200f);
		Npc mark = harness.Spawn(ArrowTarget, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(archer, mark);

		BossAiHarness.SpellHit(mark, archer, SealWaveAttackerAI.BombardmentShell);

		Assert.DoesNotContain(mark, harness.LiveNpcs());
	}

	/// <summary>
	/// <b>The snare does not.</b> Only the second cast ends it; without this the pin above would pass
	/// on a marker that despawned when anything at all touched it.
	/// </summary>
	[Fact]
	public void TheSnareLeavesTheMarkStanding()
	{
		using BossAiHarness harness = NewHarness();
		Npc archer = harness.Spawn(236218, 300f, 300f, 200f);
		Npc mark = harness.Spawn(ArrowTarget, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(archer, mark);

		BossAiHarness.SpellHit(mark, archer, SealWaveAttackerAI.BombardmentSnare);

		Assert.Contains(mark, harness.LiveNpcs());
	}

	/// <summary>
	/// <b>A number the wave does not know leaves it standing.</b> 22763 is one below the first dismissal
	/// and is nobody's message; without this the eight above would pass on a pattern that despawned on
	/// anything at all.
	/// </summary>
	[Fact]
	public void ANumberTheWaveDoesNotKnowLeavesItStanding()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc attacker = harness.Spawn(Tank, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(crier, attacker);

		NpcMessageBus.Broadcast(crier, 22763, null, 100f);

		Assert.Contains(attacker, harness.LiveNpcs());
	}

	/// <summary>
	/// <b>One time in ten the guard's shout is taken personally,</b> and the hate lands on the guard that
	/// shouted rather than on both guards by id — which is the difference between this and the aionemu
	/// class it replaces.
	/// </summary>
	[Fact]
	public void OneTimeInTenTheGuardsShoutIsTakenPersonally()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc attacker = harness.Spawn(Tank, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, attacker);
		BossAiHarness.AlwaysRolls(attacker);

		NpcMessageBus.Broadcast(guard, SealWaveAttackerAI.GuardTaunt, null, 100f);

		Assert.Equal(SealWaveAttackerAI.TauntHate, attacker.GetAggroList().GetHate(guard));
	}

	/// <summary>
	/// <b>And nine times in ten it is not.</b> Retail rolls, and the class this replaces did not — it
	/// hated both guards on sight, every time, which is a different fight.
	/// </summary>
	[Fact]
	public void AndNineTimesInTenItIsNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc attacker = harness.Spawn(Tank, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, attacker);
		BossAiHarness.NeverRolls(attacker);

		NpcMessageBus.Broadcast(guard, SealWaveAttackerAI.GuardTaunt, null, 100f);

		Assert.Equal(0, attacker.GetAggroList().GetHate(guard));
	}
	/// <summary><c>IDSeal_Wave_LeadGroup_Pr</c> — three bands, and it never rolls for the taunt.</summary>
	private const int PriestLeader = 236220;

	/// <summary><c>IDSeal_Wave_LeadGroup_Fi</c> — the one leader with only two bands, and no peel-off.</summary>
	private const int TankLeader = 236216;

	/// <summary>
	/// <b>A leader calls the moment a player touches it,</b> at any health. Retail's third flag carries no
	/// health guard at all, and first-match-wins puts it above both bands — so the very first blow spends
	/// it and the two bands below are left with their own calls still in them.
	/// </summary>
	[Fact]
	public void ALeaderCallsTheMomentAPlayerTouchesIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(PriestLeader, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(leader, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(leader, hunted);
		// Untouched health: no band a rank-and-file attacker has would fire here.
		harness.Engage(leader, hunted);

		Assert.Same(other, tank.GetTarget());
	}

	/// <summary>
	/// <b>And the rank and file do not,</b> which is what makes the band above a leader difference rather
	/// than something the whole wave does.
	/// </summary>
	[Fact]
	public void AndTheRankAndFileDoNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, tank);

		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		harness.Engage(healer, hunted);
		harness.Engage(healer, hunted);

		Assert.Same(hunted, tank.GetTarget());
	}

	/// <summary>
	/// <b>The leader's first call does not spend the bands below it.</b> Three flags, three calls: one on
	/// contact, one crossing seventy, one crossing forty.
	/// </summary>
	[Fact]
	public void TheLeadersFirstCallDoesNotSpendTheBandsBelowIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc leader = harness.Spawn(PriestLeader, 300f, 300f, 200f);
		Npc tank = harness.Spawn(Tank, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(leader, tank);

		harness.Engage(leader, hunted);
		leader.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		// The contact band is spent. Crossing seventy still has a call of its own.
		harness.Engage(tank, hunted);
		BossAiHarness.Rehate(tank, other);
		BossAiHarness.SetHpPercent(leader, 60);
		leader.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(other, tank.GetTarget());
	}

	/// <summary>
	/// <b>A leader takes the guard's shout every time.</b> The rank and file roll one in ten for it and
	/// the leaders have no <c>test_probability</c> at all — the same rung, two different fights.
	/// </summary>
	[Fact]
	public void ALeaderTakesTheGuardsShoutEveryTime()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(ForwardGuard, 300f, 300f, 200f);
		Npc leader = harness.Spawn(TankLeader, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, leader);
		BossAiHarness.NeverRolls(leader);

		NpcMessageBus.Broadcast(guard, SealWaveAttackerAI.GuardTaunt, null, 100f);

		Assert.Equal(SealWaveAttackerAI.TauntHate, leader.GetAggroList().GetHate(guard));
	}

	/// <summary>
	/// <b>The tank leader does not peel off.</b> It is the one leader retail gives two bands rather than
	/// three, and its answer to the healer is a pull built from skill indices — so where the rank-and-file
	/// tank turns, this one holds on.
	/// </summary>
	[Fact]
	public void TheTankLeaderDoesNotPeelOff()
	{
		using BossAiHarness harness = NewHarness();
		Npc healer = harness.Spawn(Healer, 300f, 300f, 200f);
		Npc leader = harness.Spawn(TankLeader, 305f, 300f, 200f);
		Player hunted = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ASMODIANS);
		Player other = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(healer, leader);

		harness.Engage(leader, hunted);
		BossAiHarness.Rehate(leader, other);
		harness.Engage(healer, hunted);
		BossAiHarness.SetHpPercent(healer, 60);
		healer.GetAi().OnCreatureEvent(AiEventType.Attack, hunted);

		Assert.Same(hunted, leader.GetTarget());
	}
}
