using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
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
/// <b>Half translated: his route.</b> Retail walks him through six waypoints and despawns him at the
/// last. The six are now in our walker data, taken from the client, and the straight move below ends at
/// the sixth of them rather than at the approximation it used to carry — but he still walks to it in a
/// line instead of along the route, because binding a <c>walker_id</c> would set him patrolling from
/// the moment the instance opens and retail has him stand still until somebody comes near.
/// </para>
/// <para>
/// <b>Not translated.</b> 800438 shouts twice in retail — <c>STR_CHAT_IDTiamat_Murugan_3_02</c> on
/// waking and <c>_3_03</c> three seconds later off an idle timer — and only one of the two has an id
/// we can resolve, so he keeps the single shout he had. Muragan the Loyal's six waypoints are a route
/// our spawn data does not carry, so the straight move to the door stands in for them. And his rung
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

    /// <summary>Retail's <c>despawn_self</c> on arriving at the end of the route.</summary>
    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetNpcId() == 800435)
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
