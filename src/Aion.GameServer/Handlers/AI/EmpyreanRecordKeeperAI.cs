using Aion.GameServer.Ai;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/EmpyreanRecordKeeperAI (@author xTz, Luzien, w4terbomb).</summary>
[AIName("empyreanrecordkeeper")]
public class EmpyreanRecordKeeperAI : NpcAI
{
    private readonly AtomicBoolean greeted = new AtomicBoolean();

    public EmpyreanRecordKeeperAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int msg = GetNpcId() switch
        {
            799568 => 1111451,
            799569 => 1111452,
            205331 => 1111453,
            205332 => 1111454,
            205333 => 1111455,
            205334 => 1111456,
            205335 => 1111457,
            205336 => 1111458,
            205337 => 1111459,
            205338 => 1111463,
            205339 => 1111464,
            205340 => 1111465,
            205341 => 1111466,
            205342 => 1111467,
            205343 => 1111468,
            205344 => 1111469,
            _ => 0,
        };
        if (msg != 0)
            PacketSendUtility.BroadcastMessage(GetOwner(), msg, 1000);
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        // the first record keeper is spawned with the instance, before anyone can hear it
        if (GetNpcId() == 799567 && creature is Player && greeted.CompareAndSet(false, true))
            PacketSendUtility.BroadcastMessage(GetOwner(), 1111450, 1000);
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        IInstanceHandler instanceHandler = GetPosition().GetWorldMapInstance().GetInstanceHandler();
        if (dialogActionId == DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            switch (GetNpcId())
            {
                case 799567: instanceHandler.OnChangeStage(StageType.START_STAGE_1_ELEVATOR); break;
                case 799568: instanceHandler.OnChangeStage(StageType.START_STAGE_2_ELEVATOR); break;
                case 799569: instanceHandler.OnChangeStage(StageType.START_STAGE_3_ELEVATOR); break;
                case 205331: instanceHandler.OnChangeStage(StageType.START_STAGE_4_ELEVATOR); break;
                case 205338: instanceHandler.OnChangeStage(StageType.START_STAGE_5); break;
                case 205332: instanceHandler.OnChangeStage(StageType.START_STAGE_5_ROUND_1); break;
                case 205339: instanceHandler.OnChangeStage(StageType.START_STAGE_6); break;
                case 205333: instanceHandler.OnChangeStage(StageType.START_STAGE_6_ROUND_1); break;
                case 205340: instanceHandler.OnChangeStage(StageType.START_STAGE_7); break;
                case 205334: instanceHandler.OnChangeStage(StageType.START_STAGE_7_ROUND_1); break;
                case 205341: instanceHandler.OnChangeStage(StageType.START_STAGE_8); break;
                case 205335: instanceHandler.OnChangeStage(StageType.START_STAGE_8_ROUND_1); break;
                case 205342: instanceHandler.OnChangeStage(StageType.START_STAGE_9); break;
                case 205336: instanceHandler.OnChangeStage(StageType.START_STAGE_9_ROUND_1); break;
                case 205343: instanceHandler.OnChangeStage(StageType.START_STAGE_10); break;
                case 205337: instanceHandler.OnChangeStage(StageType.START_STAGE_10_ROUND_1); break;
                case 205344: instanceHandler.OnChangeStage(StageType.PASS_GROUP_STAGE_10); break; // ends the game for everyone, the keeper stays
            }
            if (GetNpcId() != 205344)
                AIActions.DeleteOwner(this);
        }
        else if (dialogActionId == DialogAction.SETPRO2 && GetNpcId() == 799567)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            instanceHandler.OnChangeStage(StageType.START_STAGE_7);
            AIActions.DeleteOwner(this);
        }
        return true;
    }
}
