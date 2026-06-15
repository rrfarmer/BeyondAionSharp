using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

[AIName("legion_dominion_invasion_rift_open")]
public class LegionDominionInvasionRiftOpenAI : GeneralNpcAI
{
    private static readonly ILogger log = NullLogger.Instance;

    public LegionDominionInvasionRiftOpenAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        NpcTemplate template = GetOwner().GetObjectTemplate();
        TalkInfo talkInfo = template.GetTalkInfo();
        if (talkInfo == null || talkInfo.GetSubDialogType() != SubDialogType.LEGION_DOMINION_NPC)
            return;
        if (LegionDominionService.GetInstance().IsInCalculationTime())
            return;
        int territoryId = talkInfo.GetSubDialogValue();
        LegionDominionLocation loc = LegionDominionService.GetInstance().GetLegionDominionLoc(territoryId);
        if (loc == null || loc.GetInvasionRift() == null)
            return;
        LegionDominionInvasionRift invasionRift = loc.GetInvasionRift();
        int dialogPageId = 10;
        if (player.GetLegion() == null || player.GetLegion().GetOccupiedLegionDominion() != territoryId)
            dialogPageId = 1011;
        else if (player.GetInventory().GetFirstItemByItemId(invasionRift.GetKeyItemId()) == null)
            dialogPageId = 27;
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), dialogPageId));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            int territoryId = GetOwner().GetObjectTemplate().GetTalkInfo().GetSubDialogValue();
            LegionDominionLocation loc = LegionDominionService.GetInstance().GetLegionDominionLoc(territoryId);
            if (loc != null && loc.GetInvasionRift() != null)
            {
                LegionDominionInvasionRift invasionRift = loc.GetInvasionRift();
                if (!RiftService.GetInstance().IsRiftOpened(invasionRift.GetRiftId()))
                {
                    if (player.GetInventory().DecreaseByItemId(invasionRift.GetKeyItemId(), 1))
                    {
                        if (!LegionDominionService.GetInstance().OpenInvasionRift(loc.GetLocationId()))
                        {
                            log.LogError(
                                "{Player} tried to open rift in territory {Location} - rift opening failed, but item [id={KeyItemId}] got consumed!", player, loc.GetLocationId(),
                                invasionRift.GetKeyItemId());
                        }
                    }
                }
            }
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }
}
