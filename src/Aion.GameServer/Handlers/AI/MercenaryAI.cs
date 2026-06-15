using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author ViAl, Whoop
/// </summary>
[AIName("mercenary")]
public class MercenaryAI : GeneralNpcAI
{
    public MercenaryAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (!player.IsLegionMember())
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
            return;
        }
        FortressLocation location = SiegeService.GetInstance().GetFortress(((SiegeNpc)GetOwner()).GetSiegeId());
        if (!location.IsVulnerable())
            return;
        if (location.GetLegionId() != player.GetLegion().GetLegionId())
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
            return;
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        int siegeId = ((SiegeNpc)GetOwner()).GetSiegeId();
        int zoneId = 0;
        switch (dialogActionId)
        {
            case DialogAction.SELECT1_2:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1097));
                break;
            case DialogAction.SELECT1_3:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1182));
                break;
            case DialogAction.SELECT1_4:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1267));
                break;
            case DialogAction.SELECT2:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1352));
                break;
            case DialogAction.SELECT3:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1693));
                break;
            case DialogAction.SELECT4:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 2034));
                break;
            case DialogAction.SETPRO1:
                switch (siegeId)
                {
                    case 1011:
                    case 1221:
                    case 1231:
                    case 1241:
                        zoneId = 1;
                        break;
                    case 2011:
                    case 2021:
                    case 3011:
                    case 3021:
                    case 7011:
                        switch (GetNpcId())
                        {
                            case 832043: // 2011
                            case 832059:
                            case 832047: // 2021
                            case 832063:
                            case 832051: // 3011
                            case 832067:
                            case 832055: // 3021
                            case 832071:
                            case 804557: // 7011
                            case 804558:
                                zoneId = 1;
                                break;
                            case 832044: // 2011
                            case 832060:
                            case 832048: // 2021
                            case 832064:
                            case 832052: // 3011
                            case 832068:
                            case 832056: // 3021
                            case 832072:
                                zoneId = 2;
                                break;
                            case 832045: // 2011
                            case 832061:
                            case 832049: // 2021
                            case 832065:
                            case 832053: // 3011
                            case 832069:
                            case 832057: // 3021
                            case 832073:
                            case 802435: // 7011
                            case 802436:
                                zoneId = 3;
                                break;
                            case 832046: // 2011
                            case 832062:
                            case 832050: // 2021
                            case 832066:
                            case 832054: // 3011
                            case 832070:
                            case 832058: // 3021
                            case 832074:
                                zoneId = 4;
                                break;
                            case 804559:
                            case 804560:
                                zoneId = 5;
                                break;
                        }
                        break;
                }
                break;
            case DialogAction.SETPRO2:
                switch (siegeId)
                {
                    case 1011:
                    case 1221:
                    case 1231:
                    case 1241:
                        zoneId = 2;
                        break;
                    case 7011:
                        switch (GetNpcId())
                        {
                            case 804557:
                            case 804558:
                                zoneId = 2;
                                break;
                            case 802435:
                            case 802436:
                                zoneId = 4;
                                break;
                            case 804559:
                            case 804560:
                                zoneId = 6;
                                break;
                        }
                        break;
                }
                break;
            case DialogAction.SETPRO3:
                switch (siegeId)
                {
                    case 1221:
                    case 1231:
                        zoneId = 3;
                        break;
                    case 7011: // Currently no npcid switch necessary
                        zoneId = 7;
                        break;
                }
                break;
        }
        CheckMercenaryZone(player, siegeId, zoneId);
        return true;
    }

    private void CheckMercenaryZone(Player player, int siegeId, int zoneId)
    {
        FortressSiege siege = (FortressSiege)SiegeService.GetInstance().GetSiege(siegeId);
        if (siege == null)
            return;

        MercenaryLocation mLoc = siege.GetMercenaryLocationByZoneId(zoneId);
        if (mLoc == null || !mLoc.IsRequestValid())
            return;
        if (!player.GetInventory().DecreaseByItemId(186000236, mLoc.GetCosts()))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
            return;
        }
        PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(mLoc.GetMsgId()));
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 2375));
        mLoc.Spawn();
    }
}
