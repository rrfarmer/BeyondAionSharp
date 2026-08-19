using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The three Muragan escort npcs in Tiamat Stronghold (800435, 800436, 800438).
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/MuraganAI (Cheatkiller). Retail-sourced corrections below;
/// see docs/retail-ai-fidelity.md. Each of the three has its own pattern —
/// <c>IDTiamat_Murugan1</c>, <c>IDTiamat_Murugan2</c> and <c>IDTiamat_murugan4</c>.
/// <para>
/// <b>Muragan the Loyal was deleted while he was still walking.</b> He starts at
/// (930.9, 1316.3, 401) and walks to (838, 1317, 396) — <b>ninety-three units</b> — on a clock that
/// removed him after <b>ten seconds</b>, which is not enough to cover it at any npc walk speed. So the
/// escort a group is meant to follow down the corridor vanished part-way along it. Retail chains six
/// waypoints and <c>despawn_self</c>s at the last one; he now goes when he arrives.
/// </para>
/// <para>
/// <b>And the door-opener deleted himself for no reason.</b> 800436's whole retail pattern is one
/// flag-guarded <c>on_see_user</c> rung that calls <c>control_door</c>. There is no
/// <c>despawn_self</c> anywhere in it — he opens the door and stays standing.
/// </para>
/// <para>
/// <b>He walks the route now.</b> Retail chains six waypoints and despawns him at the last. Those points
/// are in our walker data, taken from the client, and he is put on them at the trigger rather than by a
/// <c>walker_id</c> on his spawn — which would set him patrolling the moment the instance opens, where
/// retail has him stand still until somebody comes within fifteen metres. The straight line to the door
/// remains only as a fallback for a build whose walker data does not carry the route.
/// </para>
/// <para>
/// <b>Not translated.</b> 800438 shouts twice in retail — <c>STR_CHAT_IDTiamat_Murugan_3_02</c> on
/// waking and <c>_3_03</c> three seconds later off an idle timer — and only one of the two has an id
/// we can resolve, so he keeps the single shout he had. And his rung
/// ends with <c>set_condition_spawn_variable MURUGAN_SPAWN</c>, which is almost certainly how retail
/// replaces the guard captain with a body — <see cref="KillGuardCaptain"/> does that here directly,
/// because this port has no conditional-spawn mechanism to hang it on.
/// </para>
/// </remarks>
[AIName("muragan")]
public class MuraganAI : GeneralNpcAI
{
    private readonly AtomicBoolean isInMove = new AtomicBoolean(false);

    public MuraganAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetOwner().GetNpcId() == 800438)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 390852, 1000);
        }
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CheckDistance(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        CheckDistance(creature);
    }

    private void CheckDistance(Creature creature)
    {
        if (creature is Player && PositionUtil.IsInRange(GetOwner(), creature, 15) && isInMove.CompareAndSet(false, true))
        {
            OpenSuramaDoor();
            StartWalk();
        }
    }

    private void StartWalk()
    {
        int owner = GetNpcId();
        if (owner == 800436 || owner == 800438)
            return;
        if (owner == 800435)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 390837);
            PacketSendUtility.BroadcastMessage(GetOwner(), 390838, 4000);
            KillGuardCaptain();
        }

        SetStateIfNot(AIState.WALKING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        if (!StartRoute())
            GetMoveController().MoveToPoint(DoorX, DoorY, DoorZ);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));

        // Retail despawns him at his last waypoint, not on a clock. The backstop below is ours: if the
        // move never reports arrival he would otherwise stand in the corridor for the whole instance.
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(WalkBackstopMillis));
    }

    /// <summary>
    /// Where Muragan the Loyal is walking: the sixth point of retail's own route,
    /// <c>Path_IDTiamat_Murugan_1</c>.
    /// </summary>
    /// <remarks>
    /// <b>Taken from the client rather than guessed.</b> The route is in
    /// <c>Map/Worlds/idtiamat_1/world_N_WayPoint_1.xml</c> and now in
    /// <c>npc_walker/300510000_Tiamat_Stronghold.xml</c>; its first point is his spawn to within half a
    /// metre and its sixth is this door. The numbers here were (838, 1317, 396), which is close enough
    /// to look right — nearly two metres off in y and nearly two in z.
    /// </remarks>
    public const float DoorX = 838.003113f;
    public const float DoorY = 1319.114136f;
    public const float DoorZ = 397.737579f;

    /// <summary>
    /// <b>Ours, not retail's.</b> Retail ends his route with <c>despawn_self</c> at the final waypoint
    /// and has no timer at all; this is only here so a move that never arrives cannot leave him
    /// standing. It replaces a ten-second delete that fired <b>while he was still walking</b>.
    /// </summary>
    public const long WalkBackstopMillis = 120_000L;

    /// <summary>Retail's route for Muragan the Loyal, <c>Path_IDTiamat_Murugan_1</c>, taken from the client.</summary>
    public const string RouteId = "3005100001";

    /// <summary>
    /// Retail despawns him at the sixth of the route's eleven points; route steps are zero-based, so the
    /// sixth is index five. Points seven to eleven exist in the data and he never walks them.
    /// </summary>
    public const int DespawnStepIndex = 5;

    /// <summary>
    /// Puts him on the imported route rather than walking him at the door in a straight line.
    /// </summary>
    /// <remarks>
    /// <c>WalkManager.StartRouteWalking</c> cannot be used: it is gated on <c>IsPathWalker</c>, which reads
    /// the <c>walker_id</c> on the spawn, and binding one would set him patrolling from the moment the
    /// instance opens. Retail has him stand still until somebody comes within fifteen metres and then
    /// walk, so the route is attached here instead, at the trigger.
    /// </remarks>
    private bool StartRoute()
    {
        WalkerTemplate route = DataManager.WALKER_DATA.GetWalkerTemplate(RouteId);
        if (route == null)
            return false;
        List<RouteStep> steps = route.GetRouteSteps();
        if (steps == null || steps.Count <= DespawnStepIndex)
            return false;

        // Point zero is where he is standing -- it is his spawn to within half a metre, which is how the
        // route was confirmed to be his in the first place. So the first leg is toward point one.
        GetMoveController().SetWalkerTemplate(route, 0);
        SetSubStateIfNot(AISubState.WALK_PATH);
        GetMoveController().SetRouteStep(steps[1]);
        GetMoveController().MoveToNextPoint();
        return true;
    }

    /// <summary>Retail's <c>despawn_self</c> at the last waypoint he actually walks.</summary>
    /// <remarks>
    /// The step index has to be read <b>before</b> <c>base</c>: the base handler runs
    /// <c>WalkManager.ChooseNextRouteStep</c>, which advances the move controller to the next step, so
    /// after it the index is the one he is leaving for and not the one he has reached.
    /// <para>
    /// When he is not on the route -- the fallback straight line, or either of the other two npcs --
    /// there is no step at all, and arrival is the end of the single move he was given.
    /// </para>
    /// </remarks>
    protected override void HandleMoveArrived()
    {
        RouteStep arrived = GetMoveController().GetCurrentStep();
        base.HandleMoveArrived();
        if (GetNpcId() != 800435)
            return;
        if (arrived == null || arrived.GetStepIndex() == DespawnStepIndex)
            AIActions.DeleteOwner(this);
    }

    /// <summary>
    /// Retail's <c>IDTiamat_Murugan2</c>, in full: a flag-guarded <c>on_see_user</c> that opens the
    /// door. <b>Nothing in it despawns him</b>, and this class did.
    /// </summary>
    private void OpenSuramaDoor()
    {
        if (GetOwner().GetNpcId() == 800436)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 390835);
            GetPosition().GetWorldMapInstance().SetDoorState(56, true);
        }
    }

    private void KillGuardCaptain()
    {
        WorldMapInstance instance = GetOwner().GetPosition().GetWorldMapInstance();
        foreach (Npc npc in instance.GetNpcs(219392))
        {
            Spawn(283145, npc.GetX(), npc.GetY(), npc.GetZ(), (sbyte)npc.GetHeading()); // 4.0
            npc.GetController().Delete();
        }
    }
}
