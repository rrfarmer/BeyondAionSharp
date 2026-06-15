using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/crucibleChallenge/RecordkeeperAI (@author xTz).</summary>
[AIName("recordkeeper")]
public class RecordkeeperAI : NpcAI
{
    public RecordkeeperAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        int instanceId = GetPosition().GetInstanceId();
        IInstanceHandler instanceHandler = GetPosition().GetWorldMapInstance().GetInstanceHandler();
        if (dialogActionId == DialogAction.SETPRO1)
        {
            switch (GetNpcId())
            {
                case 205668: // start stage 1
                    instanceHandler.OnChangeStage(StageType.START_STAGE_1_ROUND_1);
                    break;
                case 205674: // move to stage 2
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1796.5513f, 306.9967f, 469.25f, (byte)60);
                    Spawn(205683, 1821.5643f, 311.92484f, 469.4562f, (sbyte)60);
                    Spawn(205669, 1784.4633f, 306.98645f, 469.25f, (sbyte)0);
                    break;
                case 205669: // start stage 2
                    instanceHandler.OnChangeStage(StageType.START_STAGE_2_ROUND_1);
                    break;
                case 205675: // move to stage 3
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1324.433f, 1738.2279f, 316.476f, (byte)70);
                    Spawn(205684, 1358.4021f, 1758.744f, 319.1873f, (sbyte)70);
                    Spawn(205670, 1307.5472f, 1732.9865f, 316.0777f, (sbyte)6);
                    break;
                case 205670: // start stage 3
                    instanceHandler.OnChangeStage(StageType.START_STAGE_3_ROUND_1);
                    break;
                case 205676: // movet to stage 4
                    switch (Rnd.Get(1, 2))
                    {
                        case 1:
                            TeleportService.TeleportTo(player, 300320000, instanceId, 1283.1246f, 791.6683f, 436.6403f, (byte)60);
                            Spawn(205685, 1308.9664f, 796.20276f, 437.29678f, (sbyte)60);
                            Spawn(205671, 1271.4222f, 791.36145f, 436.64017f, (sbyte)0);
                            break;
                        case 2:
                            TeleportService.TeleportTo(player, 300320000, instanceId, 1270.8877f, 237.93307f, 405.38028f, (byte)60);
                            Spawn(205663, 1295.7217f, 242.15009f, 406.03677f, (sbyte)60);
                            Spawn(205666, 1258.7214f, 237.85518f, 405.3968f, (sbyte)0);
                            break;
                    }

                    break;
                case 205666: // start stage 4
                    instanceHandler.OnChangeStage(StageType.START_ALTERNATIVE_STAGE_4_ROUND_1);
                    break;
                case 205671:
                    instanceHandler.OnChangeStage(StageType.START_STAGE_4_ROUND_1);
                    break;
                case 205667: // move to stage 5
                case 205677:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 357.98798f, 349.19116f, 96.09108f, (byte)60);
                    Spawn(205686, 383.30933f, 354.07846f, 96.07846f, (sbyte)60);
                    Spawn(205672, 346.52298f, 349.25586f, 96.0098f, (sbyte)0);
                    break;
                case 205672: // start stage 5
                    instanceHandler.OnChangeStage(StageType.START_STAGE_5_ROUND_1);
                    break;
                case 205678: // move to stage 6
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1759.5004f, 1273.5414f, 389.11743f, (byte)10);
                    Spawn(205687, 1747.3901f, 1250.201f, 389.11765f, (sbyte)16);
                    Spawn(205673, 1767.1036f, 1288.4425f, 389.11728f, (sbyte)76);
                    break;
                case 205673: // start stage 6
                    instanceHandler.OnChangeStage(StageType.START_STAGE_6_ROUND_1);
                    break;
                case 205679: // get score
                    GetPosition().GetWorldMapInstance().GetInstanceHandler().DoReward(player);
                    break;
            }

            if (GetNpcId() != 205679)
            {
                AIActions.DeleteOwner(this);
            }
        }

        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }
}
