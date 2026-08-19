using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Celestius, the third-floor boss of Taloc's Hollow. Retail pattern <c>Elim_ComadAe</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tallocsHollow/CelestiusAI (@author xTz). Retail-sourced corrections below;
/// see docs/retail-ai-fidelity.md.
/// <para>
/// <b>His summons are guarded, and here they were not.</b> Retail's rung fires only while his health is
/// <c>is_hp_in_boundary larger_than=61</c> <b>and</b> his current target is
/// <c>is_distance_longer_than distance=10</c>. This class called three every twenty-five seconds from
/// the moment it was hit until it died, whatever his health and wherever the raid stood. So the two
/// things that make the mechanic answerable — <b>push him past sixty per cent and the adds stop</b>, and
/// <b>stand on him and he fights instead of calling</b> — did not exist.
/// </para>
/// <para>
/// <b>The first wave came five seconds early.</b> Retail arms <c>BTIMERI_INDEX_1</c> at <b>6000</b> on
/// entering attack state; this class opened at <b>1000</b>.
/// </para>
/// <para>
/// <b>And the old wave was never cleared.</b> Retail despawns <c>SPAWN_ID_1</c> through <c>_3</c> in the
/// same breath as it spawns the new three. With a thirty-second lifetime against a twenty-five-second
/// cycle, this class left the previous wave standing for five seconds of every cycle — six summons on
/// the floor where retail never has more than three.
/// </para>
/// <para>
/// <b>Checked and correct:</b> the three spawn points and their walker routes. Each retail
/// <c>pathname</c> pairs with the point it is spawned at, and each of our routes
/// (<c>30019000001</c>..<c>3</c>) begins at that same point, so the pairing here is right — worth
/// stating because a rotation between three summon paths is exactly the defect that has turned up twice
/// before in this instance's neighbours.
/// </para>
/// <para>
/// <b>Not translated.</b> Seven skill indices across two battle timers: his opening pair, the three
/// health-stepped rungs on <c>BTIMERI_INDEX_0</c> at 75, 50 and 25 per cent, and the casts that take
/// <c>BTIMERI_INDEX_1</c>'s place once he is under sixty-one. One consequence is visible here: below
/// that line retail re-arms <c>INDEX_1</c> at <b>15000</b> rather than 25000, which cannot be observed
/// while the rungs it drives are casts we cannot make. The timer below stays at 25000 throughout.
/// Also absent: <c>set_condition_spawn_variable IDElim_3F_Boss</c> on his death, and the ghost
/// (<c>CaspaGhost_01</c>) and cutscene that go with it.
/// </para>
/// </remarks>
[AIName("celestius")]
public class CelestiusAI : AggressiveNpcAI
{
    /// <summary>
    /// Retail <c>Elim_ComadAe</c> gives all three summons thirty seconds. They walk a path, so without
    /// it three more joined the patrol every time the branch fired and none ever left it.
    /// </summary>
    private const int SummonLife = 30;

    private const int SUMMONS_ID = 281514;

    /// <summary>Retail's <c>add_battle_timer BTIMERI_INDEX_1</c> on entering attack state, and its re-arm.</summary>
    public const long OpeningMillis = 6000L;
    public const long RepeatMillis = 25_000L;

    /// <summary>Retail's two guards on the rung: <c>larger_than=61</c> and <c>distance=10</c>.</summary>
    public const int HealthFloorPercent = 61;
    public const float TargetMustBeBeyond = 10f;

    private readonly AtomicBoolean isSpawnTaskStarted = new AtomicBoolean();
    private ScheduledTask? helpersTask;

    /// <summary>Retail's guards, both of which have to hold for the wave to be called.</summary>
    private bool WouldCall()
    {
        if (GetLifeStats().GetHpPercentage() <= HealthFloorPercent)
            return false;

        return GetOwner().GetTarget() is Creature target
            && !target.IsDead()
            && PositionUtil.GetDistance(GetOwner(), target) > TargetMustBeBeyond;
    }

    public CelestiusAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if ((creature is Player || creature is Summon) && isSpawnTaskStarted.CompareAndSet(false, true))
            StartHelpersCall();
    }

    private void StartHelpersCall()
    {
        helpersTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead() || !WouldCall())
                return ValueTask.CompletedTask;

            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18981, 44, GetOwner()).UseNoAnimationSkill();

            // Retail despawns SPAWN_ID_1..3 in the same branch, before spawning the new three.
            DeleteSummons();

            StartWalker((Npc)SpawnFor(SUMMONS_ID, 518, 813, 1378, (sbyte)0, SummonLife), "3001900001");
            StartWalker((Npc)SpawnFor(SUMMONS_ID, 551, 795, 1376, (sbyte)0, SummonLife), "3001900002");
            StartWalker((Npc)SpawnFor(SUMMONS_ID, 574, 854, 1375, (sbyte)0, SummonLife), "3001900003");
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(OpeningMillis), System.TimeSpan.FromMilliseconds(RepeatMillis));
    }

    private void StartWalker(Npc npc, string walkId)
    {
        npc.GetSpawn().SetWalkerId(walkId);
        WalkManager.StartWalking((NpcAI)npc.GetAi());
        npc.SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
    }

    private void CleanUp()
    {
        CancelTask();
        DeleteSummons();
    }

    private void CancelTask()
    {
        if (helpersTask != null && !helpersTask.IsDone())
        {
            helpersTask.Cancel(true);
        }
    }

    private void DeleteSummons()
    {
        GetPosition().GetWorldMapInstance().GetNpcs(SUMMONS_ID).ToList().ForEach(npc => npc.GetController().Delete());
    }

    protected override void HandleBackHome()
    {
        CleanUp();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CleanUp();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CleanUp();
        base.HandleDied();
    }
}
