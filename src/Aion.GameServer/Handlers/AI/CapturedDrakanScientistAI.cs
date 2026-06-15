using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
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

    private void StartWalk()
    {
        SetStateIfNot(AIState.WALKING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        GetMoveController().MoveToPoint(838, 1317, 396);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        ThreadPoolManager.GetInstance().Schedule(_ => { HandleNpcEscaping(); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(9000));
    }

    private void HandleNpcEscaping()
    {
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
