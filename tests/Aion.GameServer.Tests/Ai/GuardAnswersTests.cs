using System.Text.RegularExpressions;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The answering half of the guard call family, for npcs whose own class had no <c>on_message</c>.
/// </summary>
/// <remarks>
/// 102 artifact protectors answer <c>23100</c> in retail and none of them could here: the class carries
/// a call and a death announcement and never listened for anything. Measured with
/// <c>tools/client-extract/extract_guard_answers.py --gaps</c>, which also shows <c>23200</c> is already
/// fully bound and that the remaining <c>23000</c> shortfall is 24 npcs on five bespoke classes.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GuardAnswersTests
{
	private const int Reshanta = 400010000;

	/// <summary>A dread remnant lieutenant: an artifact protector that answers 23100.</summary>
	private const int Protector = 251450;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(ArtifactProtectorAI), typeof(GarrisonGuardCallAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary><b>An artifact protector now hears the garrison call and takes hate from it.</b></summary>
	[Fact]
	public void AnArtifactProtectorAnswersTheGarrisonCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Protector, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		NpcMessageBus.Broadcast(crier, GarrisonGuardCallAI.ThisOne, player, 25f);

		// Retail's idle rung: one point, and go for whoever it now hates most.
		Assert.Equal(1, listener.GetAggroList().GetHate(player));
	}

	/// <summary><b>And a protector that answers nothing in retail still hears nothing.</b></summary>
	[Fact]
	public void AProtectorOutsideTheTableStaysDeaf()
	{
		Assert.Empty(GuardAnswers.RungsFor(-1));
	}

	/// <summary>
	/// <b>The fighting rung is emitted before the idle one.</b> Their conditions differ only by
	/// <c>When.Fighting</c>, so the idle rung would swallow every call if it came first.
	/// </summary>
	[Fact]
	public void TheFightingRungOutranksTheIdleOne()
	{
		PatternBranch[] rungs = GuardAnswers.RungsFor(Protector);

		Assert.Equal(2, rungs.Length);
		Assert.True(rungs[0].Priority > rungs[1].Priority);
	}

	/// <summary>
	/// <b>Every player-targeted answer is retail's common pair, or one of its two named exceptions.</b>
	/// </summary>
	/// <remarks>
	/// 22 npcs answer with <c>do_nothing</c> — they hear the call and deliberately stand still — and 12
	/// answer with a thousand points and no fighting rung. Both are carried at their own values.
	/// <para>
	/// <c>do_nothing</c> is recorded as <b>no</b> points rather than zero, and that is not pedantry:
	/// <c>AggroInfo.AddHate</c> floors hate at 1, so a rung emitted with zero would put the guard retail
	/// tells to stand still into the fight with a single point and send it at the player.
	/// </para>
	/// </remarks>
	[Fact]
	public void EveryPlayerTargetedAnswerIsThePairOrANamedException()
	{
		int silent = 0;
		int heavy = 0;
		foreach ((int _, GuardAnswers.Answer[] answers) in GuardAnswers.ByNpc)
		{
			foreach (GuardAnswers.Answer answer in answers)
			{
				// The npc-versus-npc half is a different mechanic -- a different target, a millionfold
				// hate value, and in 30003's case no hate at all -- and has its own pins.
				if (answer.Call >= 30000)
					continue;

				if (answer.Idle == 1 && answer.Busy == 100)
					continue;

				if (answer.Idle < 0 && answer.Busy < 0)
					silent++;
				else if (answer.Idle == 1000 && answer.Busy < 0)
					heavy++;
				else
					Assert.Fail($"unclassified answer {answer.Call} {answer.Idle}/{answer.Busy}");
			}
		}

		Assert.Equal(22, silent);
		Assert.Equal(12, heavy);
	}

	/// <summary><b>And a do_nothing answer emits no rung, while staying in the table.</b></summary>
	[Fact]
	public void ADoNothingAnswerEmitsNoRung()
	{
		int quiet = 0;
		foreach ((int npcId, GuardAnswers.Answer[] answers) in GuardAnswers.ByNpc)
		{
			if (answers.Length != 1 || answers[0].Call >= 30000)
				continue;
			if (answers[0].Idle >= 0 || answers[0].Busy >= 0)
				continue;

			quiet++;
			Assert.Empty(GuardAnswers.RungsFor(npcId));

			// Still known, so its class does not fall back to the constants and answer anyway.
			Assert.True(GuardAnswers.Knows(npcId));
		}

		Assert.True(quiet > 0);
	}

	/// <summary>
	/// <b>The killer's npc-versus-npc rungs are bounded by the table, not by the class.</b>
	/// </summary>
	/// <remarks>
	/// The first attempt at this gated on a table filtered to npcs with a static spawn point, and both
	/// of this port's artifact killers are <em>summoned</em> — so the table named four killers where
	/// retail names 33, and the gate switched off two mechanics that already worked. Three pins caught
	/// it. The table is filtered on whether an npc exists now, not on whether we place it.
	/// </remarks>
	[Fact]
	public void TheKillersRetailNamesAnswerTheProtectorMessages()
	{
		// All three come when a protector calls.
		foreach (int killer in new[] { 235543, 251463, 251160 })
			Assert.True(GuardAnswers.Answers(killer, FortressKillerAI.ProtectorCalls));

		// Only the artifact killers stand down when one dies. The advance village killer does not, and
		// the class used to give it that rung anyway.
		Assert.True(GuardAnswers.Answers(251463, FortressKillerAI.ProtectorDown));
		Assert.True(GuardAnswers.Answers(251160, FortressKillerAI.ProtectorDown));
		Assert.False(GuardAnswers.Answers(235543, FortressKillerAI.ProtectorDown));

		// The despawn order is membership only: no hate, and no rung.
		GuardAnswers.Answer down = Assert.Single(
			GuardAnswers.ByNpc[251160], a => a.Call == FortressKillerAI.ProtectorDown);
		Assert.Equal(-1, down.Idle);
		Assert.Equal(-1, down.Busy);
	}

	/// <summary>An ahserion pod npc that answers 23000 and runs no pattern.</summary>
	private const int AhserionListener = 277187;

	/// <summary>
	/// <b>An npc with no pattern at all answers the call.</b> Sixteen Ahserion npcs and four others
	/// answer <c>23000</c> in retail on classes that run plain <c>aggressive</c>, so the rungs are
	/// applied directly rather than folded into a pattern.
	/// </summary>
	[Fact]
	public void AnNpcWithNoPatternStillAnswers()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(AhserionAggressiveNpcAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc crier = harness.Spawn(AhserionListener, 300f, 300f, 200f);
		Npc listener = harness.Spawn(AhserionListener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		Assert.True(GuardAnswers.AnswerCall(listener, crier, AbyssGuardCallAI.CallForHelp, player));

		Assert.Equal(1, listener.GetAggroList().GetHate(player));
	}

	/// <summary><b>And it ignores a message it has no answer for.</b></summary>
	[Fact]
	public void AndIgnoresAMessageItHasNoAnswerFor()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(AhserionAggressiveNpcAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc crier = harness.Spawn(AhserionListener, 300f, 300f, 200f);
		Npc listener = harness.Spawn(AhserionListener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		Assert.False(GuardAnswers.AnswerCall(listener, crier, 12345, player));
		Assert.Equal(0, listener.GetAggroList().GetHate(player));
	}

	/// <summary><b>A call naming somebody it is not at war with is heard and dropped.</b></summary>
	[Fact]
	public void ACallNamingAFriendIsDropped()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(AhserionAggressiveNpcAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc crier = harness.Spawn(AhserionListener, 300f, 300f, 200f);
		Npc listener = harness.Spawn(AhserionListener, 320f, 300f, 200f);

		// The answer is claimed -- this npc does answer 23000 -- but nothing lands.
		Assert.True(GuardAnswers.AnswerCall(listener, crier, AbyssGuardCallAI.CallForHelp, crier));
		Assert.Equal(0, listener.GetAggroList().GetHate(crier));
	}

	/// <summary><b>An npc never answers its own call.</b></summary>
	[Fact]
	public void AnNpcNeverAnswersItself()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(AhserionAggressiveNpcAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc listener = harness.Spawn(AhserionListener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		Assert.False(GuardAnswers.AnswerCall(listener, listener, AbyssGuardCallAI.CallForHelp, player));
		Assert.Equal(0, listener.GetAggroList().GetHate(player));
	}

	/// <summary>
	/// <b>A guard already fighting does not turn to face a friend the call named.</b>
	/// </summary>
	/// <remarks>
	/// The idle rung does not need its own enmity check -- <c>AggroList.AddHate</c> refuses a
	/// non-enemy anyway, which is why a mutation removing the check survived against the idle pin. The
	/// fighting rung does need it: that one calls <c>SetTarget</c> whether or not the hate lands, so
	/// without the check a guard would swing round to face something it cannot fight.
	/// </remarks>
	[Fact]
	public void AFightingGuardDoesNotTurnToFaceAFriend()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(AhserionAggressiveNpcAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc crier = harness.Spawn(AhserionListener, 300f, 300f, 200f);
		Npc listener = harness.Spawn(AhserionListener, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(listener, player);
		harness.Engage(listener, player);
		Assert.Same(player, listener.GetTarget());

		// The call names the crier -- a friend. The busy rung must not turn on it.
		GuardAnswers.AnswerCall(listener, crier, AbyssGuardCallAI.CallForHelp, crier);

		Assert.Same(player, listener.GetTarget());
	}

	/// <summary>A guardian veteran spellcaster: answers 23100 with a thousand points and never turns.</summary>
	private const int Outlier = 233127;

	/// <summary>
	/// <b>An npc whose retail answer is not the common pair gets its own numbers.</b> Two 23100
	/// answerers carry <c>points_to_add=1000</c> and have no fighting rung at all.
	/// </summary>
	/// <remarks>
	/// This is why the answering classes stopped sharing one static pattern. They hardcoded 1/100, which
	/// was right for every npc they held until these two were bound to them -- a shared pattern would
	/// have quietly given them a hundredth of retail's hate and a target switch retail never wrote.
	/// </remarks>
	[Fact]
	public void AnOutlierKeepsItsOwnNumbers()
	{
		GuardAnswers.Answer[] answers = GuardAnswers.ByNpc[Outlier];

		GuardAnswers.Answer only = Assert.Single(answers);
		Assert.Equal(23100, only.Call);
		Assert.Equal(1000, only.Idle);
		Assert.Equal(-1, only.Busy);

		// One rung, not two: retail gives it no fighting answer.
		Assert.Single(GuardAnswers.RungsFor(Outlier));
	}

	/// <summary>
	/// <b>And it answers with that thousand in a live fight.</b> The npc is bound to
	/// <c>garrison_guard_answer</c>, whose constants say 1.
	/// </summary>
	[Fact]
	public void AndTheOutlierAnswersWithItsOwnValue()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(GarrisonGuardAnswerAI), typeof(GarrisonGuardCallAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();
		Npc crier = harness.Spawn(Outlier, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Outlier, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		NpcMessageBus.Broadcast(crier, GarrisonGuardCallAI.ThisOne, player, 25f);

		Assert.Equal(1000, listener.GetAggroList().GetHate(player));
	}

	/// <summary>Talle: the one npc on <c>general</c> whose retail pattern answers 23100.</summary>
	private const int Talle = 802383;

	/// <summary>
	/// <b>A non-aggressive npc can now hear a guard's call — and this one still cannot act on it.</b>
	/// </summary>
	/// <remarks>
	/// Two separate things, and the pin asserts both rather than the one that looks like success.
	/// <para>
	/// <b>Fixed:</b> <c>GeneralNpcAI</c> did not implement <c>INpcMessageListener</c> at all, so a
	/// message never reached it. It could not be rebound to an answering class instead: every one of
	/// them descends from <c>AggressiveNpcAI</c>, and talle's retail pattern has no <c>on_see_user</c>
	/// rung, so rebinding would have made a non-aggressive npc attack on sight in order to fix its
	/// hearing. <see cref="GuardAnswers"/> gates the listening on the table, so every other npc on
	/// <c>general</c> pays a dictionary miss and nothing else.
	/// </para>
	/// <para>
	/// <b>Not fixed:</b> talle's tribe is <c>GENERAL</c>, so it is at war with nobody and the hate is
	/// refused by <c>AggroList.IsAware</c> before it can land. That is <em>not</em> a defect to repair
	/// here: Java gives it <c>GENERAL</c> too, and retail's own <c>npcs.xml</c> record for it carries no
	/// <c>tribe</c> element at all. An earlier reading of this log said retail gave it
	/// <c>ProtectGuard_Light</c>; that came from a neighbouring record found by pattern name, and it was
	/// wrong. The answer reaches the npc, which is all this port can currently justify.
	/// </para>
	/// <para>
	/// <b>So the hand-off itself is not mutation-tested.</b> Gutting the body of
	/// <c>GeneralNpcAI.OnNpcMessage</c> leaves every assertion here standing, because the one npc the
	/// change serves cannot act on the call anyway. The interface is pinned; the call inside it is not,
	/// and it will not be until an npc on <c>general</c> both answers a call and has a tribe to fight
	/// with. Said here rather than left for someone to discover from a green suite.
	/// </para>
	/// </remarks>
	[Fact]
	public void ANonAggressiveNpcHearsTheCallEvenThoughItCannotActOnIt()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(GeneralNpcAI), typeof(GarrisonGuardCallAI), typeof(AggressiveNpcAI))
			.Build();
		Npc crier = harness.Spawn(Talle, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Talle, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ASMODIANS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		// It listens now -- this is what the class could not do.
		Assert.True(listener.GetAi() is INpcMessageListener);
		Assert.True(GuardAnswers.AnswerCall(listener, crier, GarrisonGuardCallAI.ThisOne, player));

		// And the hate does not land, because the npc is at war with nobody.
		Assert.False(player.IsEnemy(listener));
		Assert.Equal(0, listener.GetAggroList().GetHate(player));
	}

	/// <summary><b>And an ordinary general npc is untouched by the same call.</b></summary>
	[Fact]
	public void AnOrdinaryGeneralNpcIsUntouched()
	{
		Assert.Empty(GuardAnswers.RungsFor(203100));
		Assert.False(GuardAnswers.ByNpc.ContainsKey(203100));
	}

	/// <summary>An upright lieutenant: retail gives it the killer's wake-up call.</summary>
	private const int AnsweringProtector = 251467;

	/// <summary>An initiate protector: on the same class, and retail leaves it standing.</summary>
	private const int SilentProtector = 263601;

	/// <summary>A dread remnant artifact killer, and one of the four that answer 30003.</summary>
	private const int ArtifactKiller = 251160;

	/// <summary>
	/// <b>A protector answers a waking killer only if retail says it does.</b> 282 npcs sat on the
	/// classes that answered <c>30001</c>; retail gives the rung to 135 of them.
	/// </summary>
	/// <remarks>
	/// The message is npc-versus-npc and carries <c>points_to_add=1000000</c> — a killer wakes, shouts
	/// once at fifty metres, and everything that answers drops what it is doing and comes. Answering it
	/// on the whole class meant 147 protectors that retail leaves at their posts abandoned them every
	/// time a killer spawned. Same defect as the one <c>SiegeDeathCalls</c> fixed for <c>30003</c>, one
	/// message over: the class was the population, where retail's population is per npc.
	/// </remarks>
	[Fact]
	public void OnlyTheProtectorsRetailNamesAnswerAWakingKiller()
	{
		Assert.True(GuardAnswers.Answers(AnsweringProtector, FortressKillerAI.KillerAwake));
		Assert.False(GuardAnswers.Answers(SilentProtector, FortressKillerAI.KillerAwake));
	}

	/// <summary><b>And the silent one really does stay put when the call goes out.</b></summary>
	[Fact]
	public void ASilentProtectorStaysAtItsPost()
	{
		using BossAiHarness harness = BossAiHarness.For(400010000).WithWorldSize(4096)
			.WithAi(typeof(ArtifactProtectorAI), typeof(FortressKillerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();
		Npc killer = harness.Spawn(ArtifactKiller, 300f, 300f, 200f);
		Npc answers = harness.Spawn(AnsweringProtector, 305f, 300f, 200f);
		Npc silent = harness.Spawn(SilentProtector, 306f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, answers);
		BossAiHarness.MakeMutuallyKnown(killer, silent);

		// Both are at war with the killer, so the difference below is the table and nothing else.
		Assert.True(killer.IsEnemy(answers));
		Assert.True(killer.IsEnemy(silent));

		NpcMessageBus.Broadcast(killer, FortressKillerAI.KillerAwake, killer,
			FortressKillerAI.WakeCallRange);

		Assert.True(answers.GetAggroList().GetHate(killer) > 0);
		Assert.Equal(0, silent.GetAggroList().GetHate(killer));
	}

	/// <summary>
	/// <b>An advance village killer does not stand down when a protector dies.</b> Retail gives it the
	/// 30002 answer and not the 30003 one; the class gave it both.
	/// </summary>
	[Fact]
	public void TheVillageKillerDoesNotStandDown()
	{
		using BossAiHarness harness = BossAiHarness.For(220070000).WithWorldSize(4096)
			.WithAi(typeof(FortressKillerAI), typeof(BaseProtectorAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();
		Npc killer = harness.Spawn(235543, 300f, 300f, 200f);
		Npc guard = harness.Spawn(234199, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, killer);
		Assert.Contains(killer, harness.LiveNpcs());

		NpcMessageBus.Broadcast(guard, FortressKillerAI.ProtectorDown, guard, 50f);

		Assert.Contains(killer, harness.LiveNpcs());
	}

	/// <summary>A south warden garrison warcaptain: retail sends it when a killer wakes.</summary>
	private const int AnsweringWarcaptain = 234199;

	/// <summary>A north warden warcaptain: same class, same tribe, and retail leaves it standing.</summary>
	private const int SilentWarcaptain = 234196;

	/// <summary>
	/// <b>A base protector answers a waking killer only if retail says it does.</b> 253 npcs sit on the
	/// class; retail names 117.
	/// </summary>
	/// <remarks>
	/// The same defect as the siege protectors', in a second class that was not checked when the first
	/// was fixed — 136 village and outpost warcaptains left their posts every time a killer spawned.
	/// The two npcs here are the same class and the same tribe and differ only in the table.
	/// </remarks>
	[Fact]
	public void OnlyTheBaseProtectorsRetailNamesAnswerAWakingKiller()
	{
		Assert.True(GuardAnswers.Answers(AnsweringWarcaptain, FortressKillerAI.KillerAwake));
		Assert.False(GuardAnswers.Answers(SilentWarcaptain, FortressKillerAI.KillerAwake));

		using BossAiHarness harness = BossAiHarness.For(220070000).WithWorldSize(4096)
			.WithAi(typeof(BaseProtectorAI), typeof(FortressKillerAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();
		Npc killer = harness.Spawn(235543, 300f, 300f, 200f);
		Npc answers = harness.Spawn(AnsweringWarcaptain, 305f, 300f, 200f);
		Npc silent = harness.Spawn(SilentWarcaptain, 306f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, answers);
		BossAiHarness.MakeMutuallyKnown(killer, silent);

		// Both are at war with the killer, so the difference is the table and nothing else.
		Assert.True(killer.IsEnemy(answers));
		Assert.True(killer.IsEnemy(silent));

		NpcMessageBus.Broadcast(killer, FortressKillerAI.KillerAwake, killer,
			FortressKillerAI.WakeCallRange);

		Assert.True(answers.GetAggroList().GetHate(killer) > 0);
		Assert.Equal(0, silent.GetAggroList().GetHate(killer));
	}

	/// <summary>
	/// <b>No AI class may declare <c>OnNpcMessage</c>, skip the hand-off, and own npcs the table
	/// answers for.</b>
	/// </summary>
	/// <remarks>
	/// Declaring the method <em>hides</em> the inherited one, so a class that neither calls
	/// <c>base.OnNpcMessage</c> nor consults <see cref="GuardAnswers"/> itself silently drops every
	/// answer the table holds for its npcs. That is how <c>AbstractSiegeProtectorAI</c> stayed deaf to
	/// everything but one message while the artifact protectors' folded-in rungs sat inert, and how
	/// <c>BaseProtectorAI</c> did the same after it. The compiler reports the hiding as CS0108, in a
	/// warning list nobody reads; this fails a test instead.
	/// </remarks>
	[Fact]
	public void NoClassHidesTheMessageHandlerAndKeepsTableNpcs()
	{
		string templates = File.ReadAllText(Path.Combine(BossAiHarness.RepoRoot(),
			"game-server", "data", "static_data", "npcs", "npc_templates.xml"));
		Dictionary<string, List<int>> byAiName = new Dictionary<string, List<int>>();
		foreach (Match bound in Regex.Matches(templates, @"npc_id=""(\d+)""[^>]*?\bai=""([\w_]+)"""))
		{
			if (!byAiName.TryGetValue(bound.Groups[2].Value, out List<int>? ids))
				byAiName[bound.Groups[2].Value] = ids = new List<int>();
			ids.Add(int.Parse(bound.Groups[1].Value));
		}

		List<string> offenders = new List<string>();
		string root = Path.Combine(BossAiHarness.RepoRoot(), "src", "Aion.GameServer", "Handlers", "AI");
		foreach (string file in Directory.EnumerateFiles(root, "*.cs"))
		{
			string text = File.ReadAllText(file);
			if (!text.Contains("void OnNpcMessage"))
				continue;
			// Only two things actually deliver a table answer: handing off to the inherited method, or
			// applying the rungs directly. Calling `GuardAnswers.Answers` is a *gate* on one message and
			// delivers nothing, so it must not excuse a class from this check.
			if (text.Contains("base.OnNpcMessage") || text.Contains("GuardAnswers.AnswerCall"))
				continue;

			foreach (Match named in Regex.Matches(text, @"AIName\(""([\w_]+)""\)"))
			{
				if (byAiName.TryGetValue(named.Groups[1].Value, out List<int>? ids)
					&& ids.Any(GuardAnswers.Knows))
				{
					offenders.Add($"{Path.GetFileName(file)} ({named.Groups[1].Value})");
				}
			}
		}

		Assert.Empty(offenders);
	}
}
