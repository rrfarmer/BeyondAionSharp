using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu
/// </summary>
[AIName("shugo_tomb_royalty_admirer")]
public class ShugoTombRoyaltyAdmirerAI : NpcAI
{
    public ShugoTombRoyaltyAdmirerAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        int instanceId = player.GetInstanceId();

        switch (dialogActionId)
        {
            case DialogAction.SETPRO1:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                switch (GetNpcId())
                {
                    case 831110:
                        ChangeStageList(player, StageList.START_STAGE_1_PHASE_1);
                        break;
                    case 831111:
                        ChangeStageList(player, StageList.START_STAGE_2_PHASE_1);
                        break;
                    case 831112:
                        ChangeStageList(player, StageList.START_STAGE_3_PHASE_1);
                        break;
                    case 831114: // Crown Prince's Delighted Admirer
                    case 831306: // Crown Prince's Disappointed Admirer
                        TeleportService.TeleportTo(player, 300560000, instanceId, 346.27332f, 424.07101f, 294.75793f, (byte)90, TeleportAnimation.FADE_OUT_BEAM);
                        break;
                    case 831115: // Empress' Delighted Admirer
                    case 831195: // Empress' Disappointed Admirer
                        TeleportService.TeleportTo(player, 300560000, instanceId, 450.8527f, 105.94637f, 212.20023f, (byte)90, TeleportAnimation.FADE_OUT_BEAM);
                        break;
                }
                break;
            case DialogAction.SETPRO2:
                switch (GetNpcId())
                {
                    case 831114: // Crown Prince's Delighted Admirer
                    case 831306: // Crown Prince's Disappointed Admirer
                        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(player.GetRace() == Race.ASMODIANS ? 21104 : 21095, player, player);
                        break;
                    case 831115: // Empress' Delighted Admirer
                    case 831195: // Empress' Disappointed Admirer
                        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(player.GetRace() == Race.ASMODIANS ? 21105 : 21096, player, player);
                        break;
                }
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
                break;
        }
        return true;
    }

    private void ChangeStageList(Player player, StageList stageList)
    {
        GetPosition().GetWorldMapInstance().GetInstanceHandler().OnChangeStageList(stageList);
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        AIActions.DeleteOwner(this);
    }
}
