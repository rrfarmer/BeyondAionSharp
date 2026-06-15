using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("tamer_anikiki")]
public class TamerAnikikiAI : GeneralNpcAI
{
    private AtomicBoolean isStartedWalkEvent = new AtomicBoolean(false);

    public TamerAnikikiAI(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        base.HandleCreatureMoved(creature);
        if (GetNpcId() == 219040 && IsInRange(creature, 10) && creature is Player)
        {
            if (isStartedWalkEvent.CompareAndSet(false, true))
            {
                GetSpawnTemplate().SetWalkerId("3004600001");
                WalkManager.StartWalking(this);
                GetOwner().SetState(CreatureState.ACTIVE, true);
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                // Key Box
                Spawn(700553, 611, 481, 936, (sbyte)90);
                Spawn(700553, 657, 482, 936, (sbyte)60);
                Spawn(700553, 626, 540, 936, (sbyte)1);
                Spawn(700553, 645, 534, 936, (sbyte)75);
                PacketSendUtility.SendPacket((Player)creature, new SM_QUEST_ACTION(0, 180));
                PacketSendUtility.BroadcastToMap(GetOwner(), 1400262);
            }
        }
    }

    protected override void HandleMoveArrived()
    {
        if (GetNpcId() == 219040)
        {
            int point = GetOwner().GetMoveController().GetCurrentStep().GetStepIndex();
            if (point == 12)
            {
                GetSpawnTemplate().SetWalkerId(null);
                WalkManager.StopWalking(this);
                AIActions.DeleteOwner(this);
                return;
            }
            else if (point == 8)
            {
                base.HandleMoveArrived();
                GetOwner().SetState(CreatureState.WALK_MODE, true);
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                return;
            }
        }
        base.HandleMoveArrived();
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetNpcId() != 219040)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18189, 20, GetOwner()).UseNoAnimationSkill();
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(5000));
        }
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        if (GetNpcId() == 219037)
            return damage;
        else
            return 1;
    }
}
