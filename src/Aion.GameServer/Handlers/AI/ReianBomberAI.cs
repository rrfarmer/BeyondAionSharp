using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("reian_bomber")]
public class ReianBomberAI : GeneralNpcAI
{
    private AtomicBoolean hasArrivedBoss = new AtomicBoolean(false);
    private int position = 1;

    public ReianBomberAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetSpawnTemplate().SetWalkerId("30028000024");
        WalkManager.StartWalking(this);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
    }

    protected override void HandleMoveArrived()
    {
        int point = GetOwner().GetMoveController().GetCurrentStep().GetStepIndex();
        base.HandleMoveArrived();
        if (hasArrivedBoss.Get())
        {
            StartHelpEvent();
        }
        else if (point == 7 && hasArrivedBoss.CompareAndSet(false, true))
        {
            GetSpawnTemplate().SetWalkerId(null);
            WalkManager.StopWalking(this);
            StartHelpEvent();
        }
    }

    private void StartHelpEvent()
    {
        GetMoveController().AbortMove();
        SetStateIfNot(AIState.IDLE);
        SetSubStateIfNot(AISubState.NONE);
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19374, 60, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                SetSubStateIfNot(AISubState.WALK_RANDOM);
                SetStateIfNot(AIState.WALKING);
                switch (position)
                {
                    case 1:
                        Help(359.763f, 585.597f, 145.525f);
                        GetMoveController().MoveToPoint(346.47787f, 604.0337f, 145.8766f);
                        position++;
                        break;
                    case 2:
                        Help(346.086f, 597.062f, 146.119f);
                        GetMoveController().MoveToPoint(370.93597f, 607.6427f, 145.41916f);
                        position++;
                        break;
                    case 3:
                        Help(362.143f, 604.723f, 146.125f);
                        GetMoveController().MoveToPoint(361.7722f, 584.4937f, 145.63573f);
                        position = 1;
                        break;
                }
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(8000));
    }

    private void DeleteNpc(Npc npc)
    {
        if (npc != null)
        {
            npc.GetController().Delete();
        }
    }

    private void Help(float x, float y, float z)
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            foreach (Npc npc in instance.GetNpcs(282530))
            {
                WorldPosition p = npc.GetPosition();
                if (p.GetX() == x && p.GetY() == y)
                {
                    DeleteNpc(npc);
                }
            }
            foreach (Npc npc in instance.GetNpcs(282387))
            {
                WorldPosition p = npc.GetPosition();
                if (p.GetX() == x && p.GetY() == y)
                {
                    return;
                }
            }
            Npc npc2 = (Npc)Spawn(282387, x, y, z, (sbyte)0);
            SkillEngine.SkillEngine.GetInstance().GetSkill(npc2, 19731, 1, npc2).UseNoAnimationSkill();
        }
    }
}
