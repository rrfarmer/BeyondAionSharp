using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The three captured Drakan scientists in Tiamat Stronghold (800425, 800426, 800427).
/// </summary>
/// <remarks>
/// Java parity: @author Estrayl. Retail-sourced corrections below; see docs/retail-ai-fidelity.md.
/// Retail patterns <c>IDTiamat_Drakan_Surama_1</c>, <c>_2</c> and <c>_3</c>, each one rung:
/// <c>is_waypoint_index 5, despawn_self</c>.
/// <para>
/// <b>All three walked to the same point, and it was one of theirs.</b> This class sent every scientist
/// to <c>(838, 1317, 396)</c>, which is index 5 of <c>Path_IDTiamat_Drakan_Surama_1_1</c> — the route
/// belonging to <b>800425 alone</b>. Retail gives each its own eleven-point path; they start at opposite
/// ends of the corridor and their sixth points are five metres apart. Two of the three were walking to
/// another scientist's door.
/// </para>
/// <para>
/// <b>And they were deleted on a nine-second clock while still walking</b>, which is the same defect
/// <c>MuraganAI</c> carried in the same instance with the same hardcoded coordinate — the escort a group
/// is meant to follow vanished part-way down the corridor. Retail despawns them on arriving at index 5
/// and has no timer at all. The backstop kept here is ours, at two minutes, for a move that never
/// reports arrival.
/// </para>
/// <para>
/// <b>Retail defines ten of these paths</b>, two per spawn spot at each end of the corridor, and our
/// spawn table uses three; each of the three matches its npc's spawn to 0.00m, which is how they were
/// identified. The other seven are spots our data does not place a scientist on.
/// </para>
/// <para>
/// <b>Not translated.</b> The <c>on_wake_up</c> and <c>on_see_user</c> rungs of those patterns, and the
/// quest bookkeeping below is ours rather than retail's — retail ends the rung at <c>despawn_self</c>.
/// </para>
/// </remarks>
[AIName("captured_drakan_scientist")]
public class CapturedDrakanScientistAI : GeneralNpcAI
{
    private readonly AtomicInteger deadGuardingEyes = new AtomicInteger(0);
    private readonly AtomicBoolean isActivated = new AtomicBoolean(false);

    public CapturedDrakanScientistAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (creature is Player && isActivated.CompareAndSet(false, true))
        {
            GetKnownList().ForEachNpc(n =>
            {
                if (n.GetNpcId() == 219390 && PositionUtil.IsInRange(GetOwner(), n, 25))
                {
                    n.GetObserveController().Attach(new DeathObserver(_ => HandleObservedNpcDied()));
                }
            });
        }
    }

    private void HandleObservedNpcDied()
    {
        if (deadGuardingEyes.IncrementAndGet() >= 2)
            ThreadPoolManager.GetInstance().Schedule(_ => { StartWalk(); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(Rnd.Get(10, 20) * 100)); // NPCs will start walking after some delay
    }

    /// <summary>Retail's <c>is_waypoint_index 5</c>: the sixth point of the eleven, where each one goes.</summary>
    public const int EscapeStepIndex = 5;

    /// <summary>
    /// Ours, not retail's: retail ends the walk with <c>despawn_self</c> on arrival and carries no timer.
    /// This only exists so a move that never reports arrival cannot leave a scientist in the corridor. It
    /// replaces a nine-second delete that fired while they were still walking.
    /// </summary>
    public const long EscapeBackstopMillis = 120_000L;

    /// <summary>Which of retail's ten paths each scientist walks, matched to its spawn to 0.00m.</summary>
    /// <remarks>
    /// Public so it can be pinned. The link between this map and the walk itself is not covered by any
    /// test -- see the test class for why -- so the map at least is.
    /// </remarks>
    public static readonly IReadOnlyDictionary<int, string> Routes = new Dictionary<int, string>
    {
        [800425] = "3005100002",   // Path_IDTiamat_Drakan_Surama_1_1
        [800427] = "3005100003",   // Path_IDTiamat_Drakan_Surama_4_1
        [800426] = "3005100004",   // Path_IDTiamat_Drakan_Surama_4_2
    };

    private void StartWalk()
    {
        SetStateIfNot(AIState.WALKING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        if (!StartRoute())
            GetMoveController().MoveToPoint(838, 1317, 396);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        ThreadPoolManager.GetInstance().Schedule(_ => { HandleNpcEscaping(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(EscapeBackstopMillis));
    }

    /// <summary>
    /// Puts the scientist on its own path. Their spawns carry no <c>walker_id</c> and should not: retail
    /// has them stand until two guarding eyes are dead, and a bound walker would send them down the
    /// corridor the moment the instance opened.
    /// </summary>
    private bool StartRoute()
    {
        if (!Routes.TryGetValue(GetNpcId(), out string? routeId))
            return false;
        WalkerTemplate route = DataManager.WALKER_DATA.GetWalkerTemplate(routeId);
        List<RouteStep>? steps = route?.GetRouteSteps();
        if (steps == null || steps.Count <= EscapeStepIndex)
            return false;

        GetMoveController().SetWalkerTemplate(route, 0);
        SetSubStateIfNot(AISubState.WALK_PATH);
        GetMoveController().SetRouteStep(steps[1]);
        GetMoveController().MoveToNextPoint();
        return true;
    }

    /// <summary>Retail's <c>despawn_self</c> at index 5.</summary>
    /// <remarks>
    /// The index is read before <c>base</c>, which runs <c>ChooseNextRouteStep</c> and advances the
    /// controller. With no route — the fallback straight line — there is no step and arrival is the end of
    /// the single move.
    /// </remarks>
    protected override void HandleMoveArrived()
    {
        RouteStep arrived = GetMoveController().GetCurrentStep();
        base.HandleMoveArrived();
        if (arrived == null || arrived.GetStepIndex() == EscapeStepIndex)
            HandleNpcEscaping();
    }

    private void HandleNpcEscaping()
    {
        if (IsDead() || !GetOwner().IsSpawned())
            return;
        HandleQuestUpdate();
        AIActions.DeleteOwner(this);
    }

    private void HandleQuestUpdate()
    {
        foreach (Player player in GetPosition().GetWorldMapInstance().GetPlayersInside())
            UpdateQuestEntryIfPossible(player);
    }

    private void UpdateQuestEntryIfPossible(Player player)
    {
        int quest = player.GetRace().Equals(Race.ELYOS) ? 30708 : 30758;
        QuestState qs = player.GetQuestStateList().GetQuestState(quest);
        if (qs != null)
        {
            lock (qs)
            {
                if (qs.GetQuestVarById(0) != 5)
                {
                    qs.SetQuestVar(qs.GetQuestVarById(0) + 1);
                    PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
                }
            }
        }
    }
}
