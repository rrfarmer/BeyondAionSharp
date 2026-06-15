using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author bobobear, Estrayl
/// </summary>
[AIName("IDTiamat_2_Kahrun_Talk")]
public class IDTiamat_2_KahrunTalkAI : NpcAI
{
    private readonly AtomicBoolean isActivated = new AtomicBoolean();

    public IDTiamat_2_KahrunTalkAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (GetPosition().GetWorldMapInstance().GetNpcs(730625).Count == 0)
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            if (isActivated.CompareAndSet(false, true))
            {
                GetOwner().OverrideNpcType(CreatureType.PEACE);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500604, 500);
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    GetMoveController().MoveToPoint(500f, 516.6f, 240.27f);
                    SetStateIfNot(AIState.WALKING);
                    GetOwner().SetState(CreatureState.ACTIVE, true);
                    PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.WALK));
                    ThreadPoolManager.GetInstance().Schedule(ct2 =>
                    {
                        SpawnOrb();
                        return ValueTask.CompletedTask;
                    }, 1500L);
                    return ValueTask.CompletedTask;
                }, 2000L);
            }
        }
        return true;
    }

    private void SpawnOrb()
    {
        Spawn(730625, 503.2197f, 516.6517f, 242.6040f, (sbyte)0, 4);
        AIActions.DeleteOwner(this);
    }
}
