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

/// <summary>Java parity: ai/instance/tiamatStrongHold/MuraganAI (Cheatkiller).</summary>
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
        GetMoveController().MoveToPoint(838, 1317, 396);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(10000));
    }

    private void OpenSuramaDoor()
    {
        if (GetOwner().GetNpcId() == 800436)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 390835);
            GetPosition().GetWorldMapInstance().SetDoorState(56, true);
            AIActions.DeleteOwner(this);
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
