using Aion.GameServer.Ai;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/EmpyreanRecordKeeperAI (@author xTz, Luzien).</summary>
[AIName("empyreanrecordkeeper")]
public class EmpyreanRecordKeeperAI : NpcAI
{
    public EmpyreanRecordKeeperAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int msg = 0;
        switch (GetNpcId())
        {
            case 799568:
                msg = 1111460;
                break;
            case 799569:
                msg = 1111461;
                break;
            case 205331:
                msg = 1111462;
                break;
            case 205338:
                msg = 1111463;
                break;
            case 205339:
                msg = 1111464;
                break;
            case 205340:
                msg = 1111465;
                break;
            case 205341:
                msg = 1111466;
                break;
            case 205342:
                msg = 1111467;
                break;
            case 205343:
                msg = 1111468;
                break;
            case 205344:
                msg = 1111469;
                break;
        }
        if (msg != 0)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), msg, 1000);
        }
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
                case 799567:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_1_ELEVATOR);
                    break;
                case 799568:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_2_ELEVATOR);
                    break;
                case 799569:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_3_ELEVATOR);
                    break;
                case 205331:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_4_ELEVATOR);
                    break;
                case 205338: // teleport to stage 5
                    instanceHandler.OnChangeStage(StageType.START_STAGE_5);
                    break;
                case 205332:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_5_ROUND_1);
                    break;
                case 205339: // teleport to stage 6
                    instanceHandler.OnChangeStage(StageType.START_STAGE_6);
                    break;
                case 205333:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_6_ROUND_1);
                    break;
                case 205340: // teleport to stage 7
                    instanceHandler.OnChangeStage(StageType.START_STAGE_7);
                    break;
                case 205334:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_7_ROUND_1);
                    break;
                case 205341: // teleport to stage 8
                    instanceHandler.OnChangeStage(StageType.START_STAGE_8);
                    break;
                case 205335:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_8_ROUND_1);
                    break;
                case 205342: // teleport to stage 9
                    instanceHandler.OnChangeStage(StageType.START_STAGE_9);
                    break;
                case 205336:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_9_ROUND_1);
                    break;
                case 205343: // teleport to stage 9
                    instanceHandler.OnChangeStage(StageType.START_STAGE_10);
                    break;
                case 205337:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_10_ROUND_1);
                    break;
                case 205344: // get score
                    GetPosition().GetWorldMapInstance().GetInstanceHandler().DoReward(player);
                    break;
            }
            AIActions.DeleteOwner(this);
        }
        else if (dialogActionId == DialogAction.SETPRO2 && GetNpcId() == 799567)
        { // start with stage 7
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            instanceHandler.OnChangeStage(StageType.START_STAGE_7);
            AIActions.DeleteOwner(this);
        }
        return true;
    }
}
