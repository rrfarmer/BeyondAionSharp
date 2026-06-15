using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/EnragedNightmareAI (Ritsu).</summary>
[AIName("enragednightmare")]
public class EnragedNightmareAI : AggressiveNpcAI
{
    private ScheduledTask skillTask;
    private Npc boss;

    public EnragedNightmareAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        UseSkill(this, boss);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        UseSkill(this, boss);
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        if (GetOwner().GetPosition().GetX() == 521.585f && GetOwner().GetPosition().GetY() == 510.16528f)
        {
            GetOwner().GetAi().SetStateIfNot(AIState.WALKING);
            WalkManager.StartWalking((NpcAI)GetOwner().GetAi());
            GetOwner().SetState(CreatureState.ACTIVE, true);
            GetOwner().GetMoveController().MoveToPoint(521.60913f, 551.00684f, 198.75f);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        }
        else
        {
            GetOwner().GetAi().SetStateIfNot(AIState.WALKING);
            WalkManager.StartWalking((NpcAI)GetOwner().GetAi());
            GetOwner().SetState(CreatureState.ACTIVE, true);
            GetOwner().GetMoveController().MoveToPoint(525.3831f, 585.3808f, 199.0476f);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
        }
    }

    private void UseSkill(NpcAI ai, Creature creature)
    {
        boss = GetPosition().GetWorldMapInstance().GetNpc(233467);
        if (boss != null && !boss.IsDead())
        {
            if (boss.GetLifeStats().GetHpPercentage() < 100)
            {
                EmoteManager.EmoteStopAttacking(GetOwner());
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21342, 1, boss).UseSkill();
                if (GetOwner().GetController().UseSkill(21342))
                {
                    skillTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        AIActions.DeleteOwner(this);
                        return ValueTask.CompletedTask;
                    }, System.TimeSpan.FromMilliseconds(3000));
                }
            }
        }
    }

    protected override void HandleBackHome()
    {
    }
}
