using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Aion.GameServer.Model.DialogAction;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/DialogService (VladimirZ, Neon). NPC dialog action dispatch. `import static DialogAction.*`→`using static` (DialogAction is a class of const int; switch(int) bare-const cases valid); instanceof X x→is X x; switch-expression (SummonOwner) w/ exhaustive enum; anonymous RequestResponseHandler&lt;Npc&gt;→nested RecoveryResponseHandler (captures expLost/price via ctor); DialogPage.X.id()→DialogPage.X.Id() (extension), getByActionId→DialogPageExtensions.GetByActionId; cases declaring locals braced. Many services/SM_*/templates red-tolerated.</summary>
public class DialogService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(DialogService));

    public static void OnCloseDialog(Player player, VisibleObject target)
    {
        if (target == null)
            return;

        if (target is Npc npc)
        {
            npc.GetAi().OnCreatureEvent(AiEventType.DIALOG_FINISH, player);

            if (npc.GetObjectTemplate().SupportsAction(DialogAction.OPEN_LEGION_WAREHOUSE) && player.IsLegionMember())
                player.GetLegion().GetLegionWarehouse().UnsetInUse(player.GetObjectId());
        }

        Mailbox mailbox = player.GetMailbox();
        if (mailbox != null && mailbox.mailBoxState != PlayerMailboxState.CLOSED)
            mailbox.mailBoxState = PlayerMailboxState.CLOSED;
    }

    public static void OnDialogSelect(int dialogActionId, Player player, Npc npc, int questId, int extendedRewardIndex)
    {
        int targetObjectId = npc.GetObjectId();

        if (questId == 0)
        {
            switch (dialogActionId)
            {
                case BUY:
                {
                    TradeListTemplate tradeListTemplate = DataManager.TRADE_LIST_DATA.GetTradeListTemplate(npc.GetNpcId());
                    if (tradeListTemplate == null)
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM(npc.GetObjectTemplate().GetL10n()));
                        break;
                    }
                    int tradeModifier = tradeListTemplate.GetSellPriceRate();
                    int legionLevel = player.GetLegion() == null ? 0 : player.GetLegion().GetLegionLevel();
                    bool hasAnythingToSell = false;
                    foreach (TradeListTemplate.TradeTab tab in tradeListTemplate.GetTradeTablist())
                    {
                        GoodsList goodsList = DataManager.GOODSLIST_DATA.GetGoodsListById(tab.GetId());
                        if (goodsList == null || goodsList.GetLegionLevel() > legionLevel)
                            continue;
                        hasAnythingToSell = true;
                        break;
                    }
                    if (hasAnythingToSell)
                        PacketSendUtility.SendPacket(player,
                            new SM_TRADELIST(player, npc, tradeListTemplate, PricesService.GetVendorBuyModifier() * tradeModifier / 100));
                    else
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM(npc.GetObjectTemplate().GetL10n()));
                    break;
                }
                case DEPOSIT_CHAR_WAREHOUSE: // warehouse (2.5)
                case OPEN_VENDOR: // Consign trade?? npc karinerk, koorunerk (2.5)
                case OPEN_STIGMA_WINDOW: // stigma
                case CREATE_LEGION: // create legion
                case OPEN_STIGMA_ENCHANT:
                case GIVE_ITEM_PROC: // Godstone socketing (2.5)
                case REMOVE_ITEM_OPTION: // remove manastone (2.5)
                case CHANGE_ITEM_SKIN: // modify appearance (2.5)
                case ITEM_UPGRADE: // item upgrade (4.7)
                case CLOSE_LEGION_WAREHOUSE: // WTF??? Quest dialog packet (2.5)
                case COMBINE_TASK: // crafting (2.5)
                case OPEN_INSTANCE_RECRUIT: // handled by AI
                case INSTANCE_ENTRY: // (2.5)
                case COMPOUND_WEAPON: // armsfusion (2.5)
                case DECOMPOUND_WEAPON: // armsbreaking (2.5)
                case HOUSING_BUILD: // housing build
                case HOUSING_DESTRUCT: // housing destruct
                case HOUSING_PERSONAL_AUCTION: // housing auction
                case CHARGE_ITEM_SINGLE: // condition an individual item
                case CHARGE_ITEM_SINGLE2: // augmenting an individual item
                case TOWN_CHALLENGE: // town improvement
                    SendDialogWindow(dialogActionId, player, npc);
                    break;
                case DISPERSE_LEGION: // disband legion
                    LegionService.GetInstance().RequestDisbandLegion(npc, player);
                    break;
                case RECREATE_LEGION: // recreate legion
                    LegionService.GetInstance().RecreateLegion(npc, player);
                    break;
                case RECOVERY: // soul healing (2.5)
                {
                    long expLost = player.GetCommonData().GetExpRecoverable();
                    if (expLost == 0)
                    {
                        player.GetEffectController().RemoveByDispelSlotType(DispelSlotType.SPECIAL2);
                        player.GetCommonData().SetDeathCount(0);
                    }
                    double factor = (expLost < 1000000 ? 0.25 - (0.00000015 * expLost) : 0.1);
                    int price = (int)(expLost * factor);

                    RequestResponseHandler<Npc> responseHandler = new RecoveryResponseHandler(npc, expLost, price);
                    if (player.GetCommonData().GetExpRecoverable() > 0)
                    {
                        bool result = player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_ASK_RECOVER_EXPERIENCE, responseHandler);
                        if (result)
                        {
                            PacketSendUtility.SendPacket(player,
                                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_ASK_RECOVER_EXPERIENCE, 0, 0, price.ToString()));
                        }
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_DONOT_HAVE_RECOVER_EXPERIENCE());
                    }
                    break;
                }
                case ENTER_PVP: // (2.5)
                    switch (npc.GetNpcId())
                    {
                        case 204089: // pvp arena in pandaemonium.
                            TeleportService.TeleportTo(player, 120010000, 1, 984f, 1543f, 222.1f);
                            break;
                        case 203764: // pvp arena in sanctum.
                            TeleportService.TeleportTo(player, 110010000, 1, 1462.5f, 1326.1f, 564.1f);
                            break;
                        case 203981:
                            TeleportService.TeleportTo(player, 210020000, 1, 439.3f, 422.2f, 274.3f);
                            break;
                    }
                    break;
                case LEAVE_PVP: // (2.5)
                    switch (npc.GetNpcId())
                    {
                        case 204087:
                            TeleportService.TeleportTo(player, 120010000, 1, 1005.1f, 1528.9f, 222.1f);
                            break;
                        case 203875:
                            TeleportService.TeleportTo(player, 110010000, 1, 1470.3f, 1343.5f, 563.7f);
                            break;
                        case 203982:
                            TeleportService.TeleportTo(player, 210020000, 1, 446.2f, 431.1f, 274.5f);
                            break;
                    }
                    break;
                case AIRLINE_SERVICE: // flight and teleport (2.5)
                {
                    switch (npc.GetNpcId())
                    {
                        case 203679: // ishalgen teleporter
                        case 203194: // poeta teleporter
                            if (!player.GetCommonData().IsDaeva())
                            {
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
                                return;
                            }
                            break;
                    }
                    TeleportService.ShowMap(player, npc);
                    break;
                }
                case GATHER_SKILL_LEVELUP: // improve extraction (2.5)
                case COMBINE_SKILL_LEVELUP: // learn tailoring armor smithing etc. (2.5)
                    CraftSkillUpdateService.GetInstance().LearnSkill(player, npc);
                    break;
                case EXTEND_INVENTORY: // expand cube (2.5)
                    CubeExpandService.ExpandCube(player, npc);
                    break;
                case EXTEND_CHAR_WAREHOUSE: // (2.5)
                    WarehouseService.ExpandWarehouse(player, npc);
                    break;
                case OPEN_LEGION_WAREHOUSE: // legion warehouse (2.5)
                    LegionService.GetInstance().OpenLegionWarehouse(player, npc);
                    break;
                case EDIT_CHARACTER_ALL:
                case EDIT_CHARACTER_GENDER: // (2.5) and (4.3)
                    PacketSendUtility.SendPacket(player, new SM_PLASTIC_SURGERY(player, dialogActionId == EDIT_CHARACTER_GENDER));
                    player.GetCommonData().SetInEditMode(true);
                    break;
                case MATCH_MAKER: // dredgion
                {
                    if (AutoGroupConfig.AUTO_GROUP_ENABLE)
                    {
                        AutoGroupType agt = AutoGroupTypeExtensions.GetAutoGroup(npc.GetNpcId());
                        if (agt != null && PeriodicInstanceManager.GetInstance().IsRegistrationOpen(agt.GetTemplate().GetMaskId()))
                            PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(agt.GetTemplate().GetMaskId()));
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(targetObjectId, 1011));
                    }
                    break;
                }
                case FACTION_JOIN: // join npcFaction (2.5)
                    player.GetNpcFactions().EnterGuild(npc);
                    break;
                case FACTION_SEPARATE: // leave npcFaction (2.5)
                    player.GetNpcFactions().LeaveNpcFaction(npc);
                    break;
                case BUY_AGAIN: // repurchase (2.5)
                    PacketSendUtility.SendPacket(player, new SM_REPURCHASE(player, npc.GetObjectId()));
                    break;
                case FUNC_PET_ADOPT: // adopt pet (2.5)
                    PacketSendUtility.SendPacket(player, new SM_PET(PetAction.TALK_WITH_MERCHANT));
                    break;
                case FUNC_PET_ABANDON: // surrender pet (2.5)
                    PacketSendUtility.SendPacket(player, new SM_PET(PetAction.TALK_WITH_MINDER));
                    break;
                case CHARGE_ITEM_MULTI: // condition all equiped items
                    ItemChargeService.StartChargingEquippedItems(player, targetObjectId, 1);
                    break;
                case TRADE_IN:
                {
                    TradeListTemplate tradeListTemplate = DataManager.TRADE_LIST_DATA.GetTradeInListTemplate(npc.GetNpcId());
                    if (tradeListTemplate == null)
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM(npc.GetObjectTemplate().GetL10n()));
                    else
                        PacketSendUtility.SendPacket(player, new SM_TRADE_IN_LIST(npc, tradeListTemplate, 100));
                    break;
                }
                case SELL:
                case TRADE_SELL_LIST:
                    PacketSendUtility.SendPacket(player, new SM_SELL_ITEM(npc));
                    break;
                case GIVEUP_CRAFT_EXPERT: // relinquish Expert Status
                    RelinquishCraftStatus.RelinquishExpertStatus(player, CraftSkillUpdateService.GetInstance().GetProfessionByNpc(npc));
                    break;
                case GIVEUP_CRAFT_MASTER: // relinquish Master Status
                    RelinquishCraftStatus.RelinquishMasterStatus(player, CraftSkillUpdateService.GetInstance().GetProfessionByNpc(npc));
                    break;
                case FUNC_PET_H_ADOPT:
                    PacketSendUtility.SendPacket(player, new SM_PET(PetAction.H_ADOPT));
                    break;
                case FUNC_PET_H_ABANDON:
                    PacketSendUtility.SendPacket(player, new SM_PET(PetAction.H_ABANDON));
                    break;
                case CHARGE_ITEM_MULTI2: // augmenting all equipped items
                    ItemChargeService.StartChargingEquippedItems(player, targetObjectId, 2);
                    break;
                case HOUSING_RECREATE_PERSONAL_INS: // recreate personal house instance (studio)
                    HousingService.GetInstance().RecreatePlayerStudio(player);
                    break;
                default:
                    HandleQuestDialogueOrSendNextPage(dialogActionId, player, npc, questId, extendedRewardIndex);
                    break;
            }
        }
        else
        {
            HandleQuestDialogueOrSendNextPage(dialogActionId, player, npc, questId, extendedRewardIndex);
        }
    }

    private static void HandleQuestDialogueOrSendNextPage(int dialogActionId, Player player, Npc npc, int questId, int extendedRewardIndex)
    {
        if (questId != 0 || dialogActionId == USE_OBJECT || dialogActionId == EXCHANGE_COIN)
        {
            QuestEnv env = new QuestEnv(npc, player, questId, dialogActionId);
            env.SetExtendedRewardIndex(extendedRewardIndex);
            if (QuestEngine.GetInstance().OnDialog(env))
                return;
        }
        // action id = next page id
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), dialogActionId, questId));
    }

    private static void SendDialogWindow(int dialogActionId, Player player, Npc npc)
    {
        if (npc.GetObjectTemplate().SupportsAction(dialogActionId))
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPageExtensions.GetByActionId(dialogActionId).Id()));
    }

    public static bool IsInteractionAllowed(Player player, Npc npc)
    {
        if (npc.GetSummonOwner() != null && !IsSummonOwner(player, npc))
            return false;
        return !IsSubDialogRestricted(player, npc);
    }

    private static bool IsSummonOwner(Player player, Npc npc)
    {
        bool playerIsCreator = player.GetObjectId() == npc.GetCreatorId();
        return npc.GetSummonOwner() switch
        {
            SummonOwner.PRIVATE => playerIsCreator,
            SummonOwner.GROUP => playerIsCreator || player.GetCurrentGroup() != null && player.GetCurrentGroup().HasMember(npc.GetCreatorId()),
            SummonOwner.ALLIANCE => playerIsCreator || player.GetCurrentTeam() is PlayerAlliance alliance && alliance.HasMember(npc.GetCreatorId()),
            SummonOwner.LEGION => playerIsCreator || player.IsLegionMember() && player.GetLegion().IsMember(npc.GetCreatorId()),
            _ => false,
        };
    }

    private static bool IsSubDialogRestricted(Player player, Npc npc)
    {
        TalkInfo talkInfo = npc.GetObjectTemplate().GetTalkInfo();
        if (talkInfo == null || talkInfo.GetSubDialogType() == null)
            return false;
        switch (talkInfo.GetSubDialogType())
        {
            case SubDialogType.FORT_CAPTURE:
            {
                if (player.GetLegion() == null)
                    return true;
                ZoneTemplate fortZone = null;
                foreach (ZoneInstance zone in npc.FindZones())
                {
                    if (zone.GetZoneTemplate().GetZoneType() != ZoneClassName.FORT)
                        continue;
                    fortZone = zone.GetZoneTemplate();
                    break;
                }
                if (fortZone == null)
                {
                    log.LogWarning("Could not find FORT zone for npc: " + npc.GetNpcId());
                    return true;
                }
                List<int> siegeIds = fortZone.GetSiegeId();
                foreach (int siegeId in siegeIds)
                {
                    FortressLocation loc = SiegeService.GetInstance().GetFortress(siegeId);
                    if (loc == null)
                        continue;
                    if (loc.GetRace().GetRaceId() == player.GetRace().GetRaceId() && loc.GetLegionId() == player.GetLegion().GetLegionId())
                    {
                        return false;
                    }
                }
                return true;
            }
            case SubDialogType.SKILL_ID:
                return player.GetSkillList().GetSkillEntry(talkInfo.GetSubDialogValue()) == null;
            case SubDialogType.ITEM_ID:
                return player.GetInventory().GetItemCountByItemId(talkInfo.GetSubDialogValue()) == 0;
            case SubDialogType.RETURN:
                return player.GetInventory().GetItemCountByItemId(164000335) == 0; // Abbey Return Stone (30 days)
            case SubDialogType.ABYSSRANK:
                if (player.IsStaff())
                    return false;
                return player.GetAbyssRank().GetRank().GetId() < talkInfo.GetSubDialogValue();
            case SubDialogType.ABYSSRANKING:
                return AbyssRankingCache.GetInstance().GetRankingListPosition(player) > talkInfo.GetSubDialogValue();
            case SubDialogType.TARGET_LEGION_DOMINION:
                if (LegionDominionService.GetInstance().IsInCalculationTime())
                    return true;
                if (player.GetLegion() != null)
                {
                    return player.GetLegion().GetCurrentLegionDominion() != talkInfo.GetSubDialogValue();
                }
                return true;
            case SubDialogType.LEGION_DOMINION_NPC:
                if (player.GetLegion() != null)
                {
                    return player.GetLegion().GetOccupiedLegionDominion() != talkInfo.GetSubDialogValue();
                }
                return true;
            case SubDialogType.LEVEL:
                return player.GetLevel() != talkInfo.GetSubDialogValue();
            case SubDialogType.LEVEL_LOW:
                return player.GetLevel() > talkInfo.GetSubDialogValue();
            case SubDialogType.LEVEL_HIGH:
                return player.GetLevel() < talkInfo.GetSubDialogValue();
            default:
                log.LogWarning("Unhandled subdialog type " + talkInfo.GetSubDialogType() + " for npc: " + npc.GetNpcId());
                return true;
        }
    }

    // Java parity: anonymous RequestResponseHandler<Npc> in the RECOVERY case (acceptRequest override).
    private sealed class RecoveryResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly long expLost;
        private readonly int price;

        public RecoveryResponseHandler(Npc npc, long expLost, int price) : base(npc)
        {
            this.expLost = expLost;
            this.price = price;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (responder.GetInventory().GetKinah() >= price)
            {
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_GET_EXP2(expLost));
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_SUCCESS_RECOVER_EXPERIENCE());
                responder.GetCommonData().ResetRecoverableExp();
                responder.GetInventory().DecreaseKinah(price, ItemPacketService.ItemUpdateType.STATS_CHANGE);
                responder.GetEffectController().RemoveByDispelSlotType(DispelSlotType.SPECIAL2);
                responder.GetCommonData().SetDeathCount(0);
            }
            else
            {
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(price));
            }
        }
    }
}
