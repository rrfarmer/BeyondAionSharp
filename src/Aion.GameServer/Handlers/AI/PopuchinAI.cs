using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Popuchin (217373), the Aturam Sky Fortress boss. Retail pattern <c>Station_FlightNM</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/aturamSkyFortress/PopuchinAI (xTz). Retail-sourced corrections below; see
/// docs/retail-ai-fidelity.md.
/// <para>
/// <b>His two bomb mechanics are separate timers, not one alternating chain.</b> Retail arms four
/// battle timers on entering attack state and the bombs live on two of them:
/// </para>
/// <list type="table">
/// <item><term>guided bombs</term><description><c>BTIMERI_INDEX_0</c>, opening <b>7500</b>, re-armed at
/// <b>40000</b>, guarded by <c>is_hp_in_boundary larger_than=50</c>. Two per firing, on his own
/// point.</description></item>
/// <item><term>scattered bombs</term><description><c>BTIMERI_INDEX_3</c>, opening <b>2500</b>, re-armed
/// at <b>25000</b> once it fires and at <b>2500</b> while it cannot, guarded by
/// <c>is_hp_lower_than percent=50</c>. Ten per firing, <c>spawn_range=35</c>.</description></item>
/// </list>
/// <para>
/// This class ran <b>one</b> task: wait 15500, wind up, then spawn two guided bombs or ten scattered
/// ones depending on his health, and start again. So the guided bombs came <b>twice as often as
/// retail's</b> and the scattered ones <b>at less than half the rate</b>, and neither had its own
/// opening. The 2500 opening on the scattered timer matters most: retail's second phase starts
/// throwing bombs within two and a half seconds of him crossing fifty percent, and here it took the
/// better part of twenty.
/// </para>
/// <para>
/// <b>And the scatter was a third of its width.</b> Retail's <c>spawn_range=35</c>; this port used 12,
/// so ten bombs that should cover the platform landed in a huddle around him.
/// </para>
/// <para>
/// <b>Retail's fifty-percent edge is exclusive on both sides</b> — <c>larger_than=50</c> and
/// <c>lower_than=50</c> — so at exactly half health neither rung fires and he throws nothing. That is
/// mirrored rather than tidied up.
/// </para>
/// <para>
/// <b>Not translated.</b> His four cast rungs are all skill indices: a cast on entering attack state
/// (<c>SKILLI_INDEX_4</c>), a bombardment on <c>BTIMERI_INDEX_1</c> every 15000 above half health, and
/// two more on <c>BTIMERI_INDEX_2</c> every 12500 below it, one of them behind
/// <c>test_probability percent=30</c>. None of those four timers exists here at all. Nor do his six
/// <c>say_to_all</c> lines (<c>STR_CHAT_ShulackNM_00</c> through <c>_05</c>), which have no ids we can
/// resolve. The wind-up casts kept below are this port's own, from Java.
/// </para>
/// </remarks>
[AIName("popuchin")]
public class PopuchinAI : AggressiveNpcAI
{
    /// <summary>The two bombs he puts out: guided above half health, scattered below it.</summary>
    public const int GuidedBomb = 217374;
    public const int ScatteredBomb = 217375;

    /// <summary>Retail's <c>BTIMERI_INDEX_0</c>: opening, and the delay it re-arms itself with.</summary>
    public const long GuidedOpeningMillis = 7500L;
    public const long GuidedRepeatMillis = 40_000L;

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_3</c>. It ticks at <see cref="ScatterOpeningMillis"/> until his health
    /// is low enough for the rung to match, and only then re-arms at <see cref="ScatterRepeatMillis"/>.
    /// </summary>
    public const long ScatterOpeningMillis = 2500L;
    public const long ScatterRepeatMillis = 25_000L;

    /// <summary>Retail's counts and <c>spawn_range</c> for each.</summary>
    public const int GuidedCount = 2;
    public const int ScatterCount = 10;
    public const float ScatterRange = 35f;

    /// <summary>The wind-up before either salvo. This port's own, inherited from Java.</summary>
    private const long WindUpFirstMillis = 3000L;
    private const long WindUpSecondMillis = 1500L;

    private bool isHome = true;
    private ScheduledTask guidedTask;
    private ScheduledTask scatterTask;

    public PopuchinAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_0</c> rung: above half health, shout, cast, and put out two guided
    /// bombs on his own point. Below half health nothing re-arms it and the guided bombs stop for good.
    /// </summary>
    private void ArmGuidedTimer(long delay)
    {
        if (IsDead() || isHome)
            return;

        guidedTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead() || isHome)
                return ValueTask.CompletedTask;
            if (GetLifeStats().GetHpPercentage() <= 50)
                return ValueTask.CompletedTask;

            WindUp(() =>
            {
                WorldPosition p = GetPosition();
                if (p != null && p.GetWorldMapInstance() != null)
                {
                    for (int i = 0; i < GuidedCount; i++)
                        Spawn(GuidedBomb, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                }

                ArmGuidedTimer(GuidedRepeatMillis);
            });

            return ValueTask.CompletedTask;
        }, delay);
    }

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_3</c> rung, and the priority-7 cycling timer underneath it: while he is
    /// above half health the timer just re-arms itself every 2500, so the first salvo lands within two
    /// and a half seconds of him crossing the line rather than at the top of some longer cycle.
    /// </summary>
    private void ArmScatterTimer(long delay)
    {
        if (IsDead() || isHome)
            return;

        scatterTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead() || isHome)
                return ValueTask.CompletedTask;

            if (GetLifeStats().GetHpPercentage() >= 50)
            {
                ArmScatterTimer(ScatterOpeningMillis);
                return ValueTask.CompletedTask;
            }

            WindUp(() =>
            {
                for (int i = 0; i < ScatterCount; i++)
                    RndSpawnInRange(ScatteredBomb, 1, ScatterRange);

                ArmScatterTimer(ScatterRepeatMillis);
            });

            return ValueTask.CompletedTask;
        }, delay);
    }

    /// <summary>
    /// The two casts this port has always played before a salvo. Retail's action list is atomic — the
    /// cast and the spawn are one line — so this wind-up is ours, kept because the skills are real and
    /// the indices that would place them are not resolvable.
    /// </summary>
    private void WindUp(System.Action then)
    {
        VisibleObject target = GetTarget();
        if (target is Player)
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19413, 49, target).UseNoAnimationSkill();

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead() || isHome)
                return ValueTask.CompletedTask;

            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19412, 49, GetOwner()).UseNoAnimationSkill();
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (!IsDead() && !isHome && GetOwner().IsSpawned())
                    then();
                return ValueTask.CompletedTask;
            }, WindUpSecondMillis);

            return ValueTask.CompletedTask;
        }, WindUpFirstMillis);
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome)
        {
            isHome = false;
            GetPosition().GetWorldMapInstance().SetDoorState(68, false);
            ArmGuidedTimer(GuidedOpeningMillis);
            ArmScatterTimer(ScatterOpeningMillis);
        }
    }

    /// <summary>
    /// Retail's <c>on_leave_attack_state</c>: <c>control_door</c> and <c>despawn spawn_id=SPAWN_ID_1</c>.
    /// </summary>
    /// <remarks>
    /// <b>The despawn was missing.</b> Every bomb he had put out stayed where it was when he reset, and
    /// the guided ones carried a ten-second self-delete only because that class had invented one. With
    /// the bomb's clock moved onto retail's aggro timer — where it belongs — this is the only thing that
    /// clears a bomb nobody ever went near, which is exactly the job retail gives it.
    /// </remarks>
    protected override void HandleBackHome()
    {
        isHome = true;
        base.HandleBackHome();
        GetPosition().GetWorldMapInstance().SetDoorState(68, true);
        if (guidedTask != null && !guidedTask.IsDone())
            guidedTask.Cancel(true);
        if (scatterTask != null && !scatterTask.IsDone())
            scatterTask.Cancel(true);

        DespawnBombs();
    }

    /// <summary>Retail's <c>SPAWN_ID_1</c> for this boss: both bomb npcs.</summary>
    private void DespawnBombs()
    {
        WorldMapInstance instance = GetPosition()?.GetWorldMapInstance();
        if (instance == null)
            return;

        foreach (Npc bomb in instance.GetNpcs(GuidedBomb, ScatteredBomb))
        {
            if (bomb != null && !bomb.GetLifeStats().IsAboutToDie() && bomb.IsSpawned())
                bomb.GetController().Delete();
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetPosition().GetWorldMapInstance().SetDoorState(68, true);
    }
}
