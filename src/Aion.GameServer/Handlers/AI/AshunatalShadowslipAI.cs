using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Ashunatal Shadowslip (217376), Aturam Sky Fortress. Retail pattern <c>Station_NinjaNM</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His three shadow waves were already right in
/// <c>ai/spawn_helpers.xml</c> — one decay shadow at 90%, three explosion shadows at 70%, two
/// disruption shadows at 50% — and **the step that clears them was missing**. At forty percent retail
/// despawns his own spawn group <em>and</em> broadcasts <c>7063</c> at a hundred metres, and every one
/// of the four shadow patterns answers it by leaving.
/// <para>
/// <b>Retail's belt-and-braces is the point, not redundancy.</b> The disruption shadow splits into
/// more of itself, and those children belong to <em>its</em> spawn group rather than his — so
/// <c>despawn SPAWN_ID_1</c> cannot reach them and the broadcast can. This is retail confirming, in
/// its own data, the rule the Queen Serusia entry arrived at from the other direction: when a summon
/// summons, the outer boss's cleanup does not reach the inner one.
/// </para>
/// <para>
/// The broadcast alone does the whole job here, and that is deliberate: <see cref="SummonerAI"/>'s
/// tracked-spawn cleanup is private, and duplicating it would clear strictly less than the call
/// already clears.
/// </para>
/// <para>
/// <b>Not translated:</b> the self-cast on each wave and the one at forty percent (<c>SKILLI_INDEX_0</c>
/// and <c>_4</c>), his four <c>say_to_all</c> shouts, message <c>7061</c> and <c>7062</c> — whose only
/// listeners are the two <c>Station_NinjaCTRL</c> npcs, instance furniture our data never places — and
/// the <c>control_door</c> pair on his death.
/// </para>
/// </remarks>
[AIName("ashunatal_shadowslip")]
public class AshunatalShadowslipAI : SummonerAI, HpPhases.PhaseHandler
{
	/// <summary>Retail's <c>7063</c>: every shadow standing, go.</summary>
	public const int ClearTheBoard = 7063;

	/// <summary>Retail's <c>range_as_meter</c> on that broadcast.</summary>
	private const float CallReach = 100f;

	private readonly HpPhases hpPhases = new HpPhases(40);

	public AshunatalShadowslipAI(Npc owner)
		: base(owner)
	{
	}

	protected override void HandleAttack(Creature creature)
	{
		base.HandleAttack(creature);
		hpPhases.TryEnterNextPhase(this);
	}

	public void HandleHpPhase(int phaseHpPercent)
	{
		if (phaseHpPercent == 40)
			NpcMessageBus.Broadcast(GetOwner(), ClearTheBoard, GetOwner(), CallReach);
	}

	protected override void HandleDied()
	{
		base.HandleDied();
		hpPhases.Reset();
	}

	protected override void HandleBackHome()
	{
		base.HandleBackHome();
		hpPhases.Reset();
	}
}

/// <summary>
/// Ashunatal's explosion shadows (217379). Retail pattern <c>Station_Shadow1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A bomb on a twelve-second fuse.</b> It engages,
/// waits twelve seconds, shouts, casts once and is gone — so a raid either kills the three of them
/// inside twelve seconds or wears whatever they do. It arms the timer on entering combat and never
/// re-arms it, which is what makes it a fuse rather than a beat.
/// <para>
/// <b>The blast is a skill index and is not built</b>, so on this server the fuse runs out and the
/// shadow simply leaves. That is the half of retail that can be ported honestly; the timing, the
/// one-shot, and the dismissal are all here.
/// </para>
/// </remarks>
[AIName("explosion_shadow")]
public class ExplosionShadowAI : PatternAi
{
	/// <summary>Retail's <c>BTIMERI_INDEX_0</c> and its <c>delay</c>.</summary>
	private const int Fuse = 0;
	private const int FuseMillis = 12000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "light the fuse", [], Do.ArmTimer(Fuse, FuseMillis))),

		OnBattleTimer = Of(Branch(8, "and go off", [When.Timer(Fuse)], Do.DespawnSelf())),

		OnMessage = Of(Branch(9, "the board is cleared",
			[When.Message(AshunatalShadowslipAI.ClearTheBoard)], Do.DespawnSelf())),
	};

	public ExplosionShadowAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Ashunatal's decay shadows (217380). Retail pattern <c>Station_Shadow2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The one shadow that is not a bomb.</b> It casts
/// on its target the moment it engages and again every twelve seconds, for as long as it lives — no
/// self-despawn anywhere in its pattern. The three shadows are three different things and this is the
/// one that stays.
/// <para>
/// <b>Its whole content is that cast, which is a skill index</b>, so only the dismissal is built. The
/// class exists for the dismissal alone, and saying why is the point: leaving it on
/// <c>aggressive</c> would leave a shadow standing after the board is cleared, which is the one
/// visible thing about it we <em>can</em> get right.
/// </para>
/// </remarks>
[AIName("decay_shadow")]
public class DecayShadowAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(9, "the board is cleared",
			[When.Message(AshunatalShadowslipAI.ClearTheBoard)], Do.DespawnSelf())),
	};

	public DecayShadowAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Ashunatal's disruption shadows (217381). Retail pattern <c>Station_Shadow3_1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It splits.</b> Fifteen seconds after engaging it
/// casts and puts one more of itself on the floor — <b>two</b> instead of one on a thirty percent
/// roll — and then stops, because it never re-arms the timer. Two disruption shadows at fifty percent
/// are therefore two to four by the time Ashunatal reaches forty.
/// <para>
/// <b>The children are 217387, a different npc</b> (<c>Station_Shadow3_2</c>) that only knows how to
/// leave when told. Retail spawns them into <em>this</em> shadow's group, not the boss's, which is why
/// the boss's forty-percent step has to be a broadcast.
/// </para>
/// <para>
/// <b>The cast is a skill index and is not built</b>; the split and its odds are.
/// </para>
/// </remarks>
[AIName("disruption_shadow")]
public class DisruptionShadowAI : PatternAi
{
	private const int Split = 0;
	private const int SplitMillis = 15000;

	/// <summary>Retail's <c>IDStation_ShadowNinja3_NM_58_n_2</c>.</summary>
	private const int Child = 217387;

	/// <summary>Retail's own spawn group for the children — not the boss's.</summary>
	private const int Brood = 1;

	private const float Beside = 4f;

	/// <summary>Retail's <c>test_probability</c> on the branch that puts out two.</summary>
	private const int TwiceAsOften = 30;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "start splitting", [], Do.ArmTimer(Split, SplitMillis))),

		OnBattleTimer = Of(
			Branch(8, "two, one time in three", [When.Timer(Split), When.Chance(TwiceAsOften)],
				Do.SpawnNear(Child, Brood, count: 2, range: Beside)),

			Branch(7, "otherwise one", [When.Timer(Split)],
				Do.SpawnNear(Child, Brood, count: 1, range: Beside))),

		OnMessage = Of(Branch(9, "the board is cleared",
			[When.Message(AshunatalShadowslipAI.ClearTheBoard)], Do.DespawnSelf())),
	};

	public DisruptionShadowAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// What a disruption shadow splits into (217387). Retail pattern <c>Station_Shadow3_2</c>, whose
/// entire content is the dismissal.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The npc that makes Ashunatal's forty-percent step a
/// broadcast rather than a group despawn — see <see cref="AshunatalShadowslipAI"/>.
/// </remarks>
[AIName("disruption_shadow_spawn")]
public class DisruptionShadowSpawnAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(7, "the board is cleared",
			[When.Message(AshunatalShadowslipAI.ClearTheBoard)], Do.DespawnSelf())),
	};

	public DisruptionShadowSpawnAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
