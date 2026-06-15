using System;
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
/// @author xTz
/// </summary>
[AIName("alarm")]
public class AlarmAI : AggressiveNpcAI
{
    private bool canThink = true;
    private AtomicBoolean startedEvent = new AtomicBoolean(false);

    public AlarmAI(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player player)
        {
            if (PositionUtil.GetDistance(GetOwner(), player) <= 23)
            {
                if (startedEvent.CompareAndSet(false, true))
                {
                    canThink = false;
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1500380);
                    PacketSendUtility.BroadcastPacket(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_Station_DoorCtrl_Evileye());
                    GetSpawnTemplate().SetWalkerId("3002400002");
                    WalkManager.StartWalking(this);
                    GetOwner().SetState(CreatureState.ACTIVE, true);
                    PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                    // both doors are inverted, they visually close by opening.
                    // they also have no collision but there are invisible walls anyway, as you are never supposed to enter the rooms behind them
                    GetPosition().GetWorldMapInstance().SetDoorState(128, true);
                    GetPosition().GetWorldMapInstance().SetDoorState(138, true);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (!IsDead())
                        {
                            Despawn();
                        }
                        return ValueTask.CompletedTask;
                    }, 3000L);
                }
            }
        }
    }

    private void Despawn()
    {
        AIActions.DeleteOwner(this);
    }
}
