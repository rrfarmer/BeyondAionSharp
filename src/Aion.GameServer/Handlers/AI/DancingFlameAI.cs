using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Vasharti's two dancing flames and the skill launchers they throw. Java parity:
/// ai/instance/rentusBase/DancingFlameAI (@author xTz, Estrayl), with the cadence and the launcher
/// taken from retail patterns <c>IDYun_Vasharti_Fire_Red</c>, <c>_Blue</c> and
/// <c>IDYun_Vasharti_Fire_SkillLauncher</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All four NPCs — the two flames and the two
/// launchers — share this <c>ai_name</c>, and the class treated them as one thing: every one of them
/// cast the buff directly, on a ten-then-nine-second timer, if a player stood within ten metres.
/// <para>
/// Retail splits the job in two. <b>A flame is a spawner</b>: every three seconds it puts a skill
/// launcher of its own colour on its own mark, and that launcher lives <b>two seconds</b>. <b>A
/// launcher is a caster</b>: its whole pattern is one self-cast as it appears. So the buff lands
/// three times as often as ours did, and it lands whether or not anyone is standing there — the
/// ten-metre check was ours.
/// </para>
/// <para>
/// <b>The one inference, stated.</b> The launcher's pattern casts <c>SKILLI_INDEX_0</c> and neither
/// launcher carries an <c>npc_skills</c> row, so the index does not resolve from our data. What does
/// resolve is the pair of skill ids the Java class already carried — 20536 and 20535 — which it
/// picked between with <c>GetNpcId() == 282998 ? … : …</c>, i.e. red launcher against everything
/// else. That test is kept as the colour mapping it plainly is, and written out per npc so the blue
/// launcher is named rather than implied. Structure from retail, skill ids from Java.
/// </para>
/// <para>
/// <b>282999 was reachable by nobody.</b> The blue launcher appeared in no spawn and in no code — it
/// was the "everything else" half of that ternary and nothing ever created it. It is spawned now.
/// </para>
/// </remarks>
[AIName("dancing_flame")]
public class DancingFlameAI : GeneralNpcAI
{
    private const int RedFlame = 282996;
    private const int BlueFlame = 282997;
    private const int RedLauncher = 282998;
    private const int BlueLauncher = 282999;

    /// <summary>The two flame buffs, from the Java class. See the remarks on how they are assigned.</summary>
    private const int RedBuff = 20536;
    private const int BlueBuff = 20535;

    private const int BuffLevel = 60;

    /// <summary>Retail's idle timer on a flame, and the launcher's <c>live_time</c>.</summary>
    private static readonly TimeSpan ThrowInterval = TimeSpan.FromSeconds(3);
    private const long LauncherLifeMillis = 2000L;

    private ScheduledTask? throwTask;

    public DancingFlameAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>Neither the flames nor their launchers can be damaged.</summary>
    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    private int LauncherFor(int flameId) => flameId == RedFlame ? RedLauncher : BlueLauncher;

    private int BuffFor(int launcherId) => launcherId == RedLauncher ? RedBuff : BlueBuff;

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        int id = GetNpcId();
        if (id == RedFlame || id == BlueFlame)
            StartThrowing(LauncherFor(id));
        else if (id == RedLauncher || id == BlueLauncher)
            CastOnce(BuffFor(id));
    }

    /// <summary>A flame's whole job: a launcher of its colour on its own mark, every three seconds.</summary>
    private void StartThrowing(int launcherId)
    {
        throwTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            Throw(launcherId);
            return ValueTask.CompletedTask;
        }, ThrowInterval, ThrowInterval);
    }

    private void Throw(int launcherId)
    {
        if (Spawn(launcherId, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                (sbyte)GetOwner().GetHeading()) is not Npc launcher)
            return;

        // Two seconds, which is retail's live_time and long enough for the cast it makes on waking.
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            launcher.GetController().DeleteIfAliveOrCancelRespawn();
            return ValueTask.CompletedTask;
        }, LauncherLifeMillis);
    }

    /// <summary>A launcher's whole job: one self-cast as it appears.</summary>
    private void CastOnce(int skillId)
    {
        SkillEngine.SkillEngine.GetInstance()
            .GetSkill(GetOwner(), skillId, BuffLevel, GetOwner())
            .UseNoAnimationSkill();
    }

    protected override void HandleDespawned()
    {
        if (throwTask != null && !throwTask.IsDone())
            throwTask.Cancel(true);
        throwTask = null;
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.ALLOW_DECAY:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
