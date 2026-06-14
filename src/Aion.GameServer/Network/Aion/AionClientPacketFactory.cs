using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Commons.Utils;
using ByteBuffer = global::Aion.Commons.Nio.ByteBuffer;
using Crypt = global::Aion.GameServer.Network.Crypt;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion;

/// <summary>
/// Java parity: network/aion/AionClientPacketFactory (Neon). Holds the opcode-&gt;packet table
/// (PacketInfo&lt;? extends AionClientPacket&gt;[250]) mapping each client opcode to its CM_* class and the
/// set of AionConnection.State values in which it is processable, and constructs the packet for an
/// incoming buffer (TryCreatePacket). Reflection Constructor -&gt; cached compiled factory delegate (infra).
/// </summary>
public static class AionClientPacketFactory
{
	private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AionClientPacketFactory));
	private static readonly PacketInfo[] packets = new PacketInfo[250];

	static AionClientPacketFactory()
	{
			packets[0] = new PacketInfo(typeof(CM_VERSION_CHECK), State.CONNECTED); // [C_VERSION (VersionPacket)]
			// packets[1] = [C_QUERY_PASSPORT (QueryPassportPacket)]
			packets[2] = new PacketInfo(typeof(CM_DISCONNECT), State.AUTHED, State.IN_GAME); // [C_LOGOUT (LogoutPacket)]
			packets[3] = new PacketInfo(typeof(CM_QUIT), State.AUTHED, State.IN_GAME); // [C_ASK_QUIT (AskQuitPacket)]
			packets[4] = new PacketInfo(typeof(CM_MAY_QUIT), State.IN_GAME); // [C_READY_TO_QUIT (ReadyToQuitPacket)]
			packets[5] = new PacketInfo(typeof(CM_REVIVE), State.IN_GAME); // [C_DEAD_RESTART (DeadRestartPacket)]
			// packets[6] = [C_CHECK_LEVEL_DATA_VERSION (CheckLevelDataVersionPacket)] sent when executing "\g_send_level_version 1" from the in-game console
			packets[7] = new PacketInfo(typeof(CM_CHARACTER_EDIT), State.AUTHED); // [C_EDIT_CHARACTER (EditCharacterPacket)]
			packets[8] = new PacketInfo(typeof(CM_ENTER_WORLD), State.AUTHED); // [C_ENTER_WORLD (EnterWorldPacket)]
			packets[9] = new PacketInfo(typeof(CM_LEVEL_READY), State.IN_GAME); // [C_LEVEL_READY (LevelReadyPacket)]
			packets[10] = new PacketInfo(typeof(CM_UI_SETTINGS), State.IN_GAME); // [C_SAVE_CLIENT_SETTINGS (SaveClientSettingsPacket)]
			packets[11] = new PacketInfo(typeof(CM_OBJECT_SEARCH), State.IN_GAME); // [C_FIND_NPC_POS (FindNpcPosPacket)]
			packets[12] = new PacketInfo(typeof(CM_CUSTOM_SETTINGS), State.IN_GAME); // [C_CHANGE_OPTION_FLAGS (ChangeOptionFlagsPacket)]
			// packets[13] = [C_CHANGE_DIRECTION (ChangeDirectionPacket)]
			packets[14] = new PacketInfo(typeof(CM_CAPTCHA), State.IN_GAME); // [C_CAPTCHA (ReceiveCaptchaAnswerPacket)]
			packets[15] = new PacketInfo(typeof(CM_TELEPORT_ANIMATION_DONE), State.IN_GAME); // [C_ACCEPT_TELEPORT (AcceptTeleportPacket)]
			packets[16] = new PacketInfo(typeof(CM_LEGION_SEND_EMBLEM_INFO), State.IN_GAME); // [C_REQUEST_GUILD_NAME (RequestGuildName)]
			packets[17] = new PacketInfo(typeof(CM_POSITION_SELF), State.IN_GAME); // [C_BLINK (ReturnBlinkPacket)]
			packets[18] = new PacketInfo(typeof(CM_TIME_CHECK), State.CONNECTED, State.AUTHED, State.IN_GAME); // [C_SYNC_TIME (SyncTimePacket)]
			packets[19] = new PacketInfo(typeof(CM_GATHER), State.IN_GAME); // [C_GATHER (GatherPacket)]
			// packets[20] = [C_MINIGAME (MinigamePacket)] likely tied to -minigame client start parameter
			packets[21] = new PacketInfo(typeof(CM_PET_EMOTE), State.IN_GAME); // [C_FUNCTIONAL_PET_MOVE (FunctionalPetActionMoveOrActionPacket)]
			packets[22] = new PacketInfo(typeof(CM_PET), State.IN_GAME); // [C_FUNCTIONAL_PET (FunctionalPetPacket)]
			packets[23] = new PacketInfo(typeof(CM_OPEN_STATICDOOR), State.IN_GAME); // [C_TOGGLE_DOOR (ToggleDoorPacket)]
			// packets[24] = [C_TOGGLE_CHEST (ToggleChestPacket)]
			// packets[25] = [C_GIVE_ITEM (GiveItemPacket)]
			// packets[26] = [C_PETITION (PetitionPacket)]
			packets[27] = new PacketInfo(typeof(CM_CHAT_MESSAGE_PUBLIC), State.IN_GAME); // [C_SAY (SayPacket)]
			packets[28] = new PacketInfo(typeof(CM_CHAT_MESSAGE_WHISPER), State.IN_GAME); // [C_WHISPER (WhisperPacket)]
			packets[29] = new PacketInfo(typeof(CM_LEGION_DOMINION_REQUEST_RANKING), State.IN_GAME); // [C_REQUEST_LEGION_DOMINION_RANKIN]
			packets[30] = new PacketInfo(typeof(CM_HOUSE_SCRIPT), State.IN_GAME); // [C_SAVE_HOUSE_SCRIPT (SaveHouseScriptPacket)]
			packets[31] = new PacketInfo(typeof(CM_TARGET_SELECT), State.IN_GAME); // [C_CHANGE_TARGET (ChangeTargetPacket)]
			packets[32] = new PacketInfo(typeof(CM_ATTACK), State.IN_GAME); // [C_ATTACK (AttackPacket)]
			packets[33] = new PacketInfo(typeof(CM_CASTSPELL), State.IN_GAME); // [C_USE_SKILL (UseSkillPacket)]
			packets[34] = new PacketInfo(typeof(CM_TOGGLE_SKILL_DEACTIVATE), State.IN_GAME); // [C_TURN_OFF_TOGGLE_SKILL (FUN_14060eed0)]
			packets[35] = new PacketInfo(typeof(CM_REMOVE_ALTERED_STATE), State.IN_GAME); // [C_TURN_OFF_ABNORMAL_STATUS (TurnOffAbnormalStatusPacket)]
			// packets[36] = [C_TURN_OFF_MAINTAIN_SKILL (&LAB_14060f880)]
			packets[37] = new PacketInfo(typeof(CM_USE_ITEM), State.IN_GAME); // [C_USE_ITEM (&LAB_14060fc40)]
			packets[38] = new PacketInfo(typeof(CM_EQUIP_ITEM), State.IN_GAME); // [C_USE_EQUIPMENT_ITEM (&LAB_140610070)]
			packets[39] = new PacketInfo(typeof(CM_CHAT_PLAYER_INFO), State.IN_GAME); // [C_ASK_PC_INFO (AskPCInfoPacket)]
			packets[40] = new PacketInfo(typeof(CM_PLAYER_LISTENER), State.IN_GAME); // [C_SAVE (SavePacket)]
			packets[41] = new PacketInfo(typeof(CM_BUILDER_COMMAND), State.IN_GAME); // [C_BUILDER_COMMAND (BuilderCommandPacket)]
			packets[42] = new PacketInfo(typeof(CM_BUILDER_CONTROL), State.IN_GAME); // [C_BUILDER_CONTROL (&LAB_140610e40)]
			packets[43] = new PacketInfo(typeof(CM_EMOTION), State.IN_GAME); // [C_ACTION (ActionPacket)]
			packets[44] = new PacketInfo(typeof(CM_PING), State.IN_GAME, State.AUTHED); // [C_ALIVE (AlivePacket)]
			packets[45] = new PacketInfo(typeof(CM_LEGION), State.IN_GAME); // [C_GUILD (GuildPacket)]
			packets[46] = new PacketInfo(typeof(CM_INSTANCE_LEAVE), State.IN_GAME); // [C_LEAVE_INSTANTDUNGEON (LeaveInstantDungeonPacket)]
			packets[47] = new PacketInfo(typeof(CM_LEGION_SEND_EMBLEM), State.IN_GAME); // [C_REQUEST_GUILD_EMBLEM_IMG (RequestGuildEmblemPacket)]
			packets[48] = new PacketInfo(typeof(CM_MOVE), State.IN_GAME); // [C_MOVE_NEW (MoveNewPacket)]
			packets[49] = new PacketInfo(typeof(CM_MOVE_IN_AIR), State.IN_GAME); // [C_PATH_FLY (PathFlyPacket)]
			packets[50] = new PacketInfo(typeof(CM_QUESTION_RESPONSE), State.IN_GAME); // [C_ANSWER (AnswerPacket)]
			packets[51] = new PacketInfo(typeof(CM_BUY_ITEM), State.IN_GAME); // [C_BUY_SELL (&LAB_140615590)]
			packets[52] = new PacketInfo(typeof(CM_SHOW_DIALOG), State.IN_GAME); // [C_START_DIALOG (StartDialogPacket)]
			packets[53] = new PacketInfo(typeof(CM_CLOSE_DIALOG), State.IN_GAME); // [C_END_DIALOG (EndDialogPacket)]
			packets[54] = new PacketInfo(typeof(CM_DIALOG_SELECT), State.IN_GAME); // [C_HACTION (HActionPacket)]
			packets[55] = new PacketInfo(typeof(CM_LEGION_HISTORY), State.IN_GAME); // [C_REQUEST_GUILD_HISTORY (RequestGuildHistoryPacket)]
			// packets[56] = [C_BOOKMARK (BookmarkPacket)]
			// packets[57] = [C_DELETE_BOOKMARK (DeleteBookmarkPacket)]
			packets[58] = new PacketInfo(typeof(CM_SET_NOTE), State.IN_GAME); // [C_TODAY_WORDS (TodayWordsPacket)]
			packets[59] = new PacketInfo(typeof(CM_LEGION_MODIFY_EMBLEM), State.IN_GAME); // [C_CHANGE_EMBLEM_VER (ChangeEmblemVerPacket)]
			// packets[60] = [C_REQUEST_ABYSS_OP_POINTS]
			packets[61] = new PacketInfo(typeof(CM_CHAT_GROUP_INFO), State.IN_GAME); // [C_ASK_PARTY_INFO (AskPartyInfoPacket)]
			packets[62] = new PacketInfo(typeof(CM_CHECK_PAK), State.IN_GAME); // [C_ASK_LOG (AskLogPacket)]
			packets[63] = new PacketInfo(typeof(CM_EXCHANGE_REQUEST), State.IN_GAME); // [C_ASK_XCHG (AskExchangePacket)]
			packets[64] = new PacketInfo(typeof(CM_EXCHANGE_ADD_ITEM), State.IN_GAME); // [C_ADD_XCHG (AddExchangePacket)]
			// packets[65] = [C_REMOVE_XCHG (RemoveExchangePacket)]
			packets[66] = new PacketInfo(typeof(CM_EXCHANGE_ADD_KINAH), State.IN_GAME); // [C_XCHG_GOLD (ExchangeGoldPacket)]
			packets[67] = new PacketInfo(typeof(CM_EXCHANGE_LOCK), State.IN_GAME); // [C_CHECK_XCHG (CheckExchangePacket)]
			packets[68] = new PacketInfo(typeof(CM_EXCHANGE_OK), State.IN_GAME); // [C_ACCEPT_XCHG (&LAB_140618cd0)]
			packets[69] = new PacketInfo(typeof(CM_EXCHANGE_CANCEL), State.IN_GAME); // [C_CANCEL_XCHG (CancelExchangePacket)]
			packets[70] = new PacketInfo(typeof(CM_WINDSTREAM), State.IN_GAME); // [C_WIND_PATH (WindPathPacket)]
			packets[71] = new PacketInfo(typeof(CM_MOTION), State.IN_GAME); // [C_CUSTOM_ANIM (CustomAnimPacket)]
			packets[72] = new PacketInfo(typeof(CM_HOUSE_KICK), State.IN_GAME); // [C_HOUSING_KICK (HousingKickPacket)]
			packets[73] = new PacketInfo(typeof(CM_HOUSE_SETTINGS), State.IN_GAME); // [C_HOUSING_CONFIG (HousingConfigPacket)]
			packets[74] = new PacketInfo(typeof(CM_MANASTONE), State.IN_GAME); // [C_ENCHANT_ITEM (EnchantItemPacket)]
			packets[75] = new PacketInfo(typeof(CM_HOUSE_DECORATE), State.IN_GAME); // [C_HOUSING_CUSTOMIZE (HousingCustomizePacket)]
			packets[76] = new PacketInfo(typeof(CM_LEGION_WH_KINAH), State.IN_GAME); // [C_GUILD_FUND (GuildFundPacket)]
			packets[77] = new PacketInfo(typeof(CM_FIND_GROUP), State.IN_GAME); // [C_PARTY_MATCH (PartyMatchPacket)]
			packets[78] = new PacketInfo(typeof(CM_CHARGE_ITEM), State.IN_GAME); // [C_CHARGE_ITEM (&LAB_14061f4c0)]
			packets[79] = new PacketInfo(typeof(CM_GROUP_DATA_EXCHANGE), State.IN_GAME); // [C_CLIENT_BROADCAST (ClientBroadcastPacket)]
			packets[80] = new PacketInfo(typeof(CM_DELETE_QUEST), State.IN_GAME); // [C_GIVE_UP_QUEST (GiveUpQuestPacket)]
			packets[81] = new PacketInfo(typeof(CM_PLAY_MOVIE_END), State.IN_GAME); // [C_QUIT_CUTSCENE (QuitCutScenePacket)]
			packets[82] = new PacketInfo(typeof(CM_HOUSE_EDIT), State.IN_GAME); // [C_HOUSING_OBJECT (HousingObjectPacket)]
			// packets[83] = [C_HOUSING_OBJECT_LIST (HousingObjectListPacket)]
			packets[84] = new PacketInfo(typeof(CM_STOP_TRAINING), State.IN_GAME); // [C_ACCOUNT_INSTANTDUNGEON (AccountInstantDungeon)]
			// packets[85] = [C_UNUSED_NEW_5]
			// packets[86] = [C_QUERY_NUMBER_RESULT (QueryNumberResultPacket)]
			// packets[87] = [C_FATIGUE_KOREA (&LAB_140621240)]
			packets[88] = new PacketInfo(typeof(CM_BUY_TRADE_IN_TRADE), State.IN_GAME); // [C_TRADE_IN (&LAB_140621590)]
			packets[89] = new PacketInfo(typeof(CM_RECIPE_DELETE), State.IN_GAME); // [C_RECIPE_DELETE (RecipeDeletePacket)]
			packets[90] = new PacketInfo(typeof(CM_ITEM_REMODEL), State.IN_GAME); // [C_CHANGE_ITEM_SKIN (ChangeItemSkinPacket)]
			// packets[91] = new PacketInfo(typeof(CM_GODSTONE_SOCKET), State.IN_GAME); // [C_GIVE_ITEM_PROC (GiveItemProcPacket)] happens via CM_MANASTONE now (no npc required anymore)
			packets[92] = new PacketInfo(typeof(CM_SECURITY_TOKEN), State.CONNECTED, State.AUTHED, State.IN_GAME); // [C_REQ_WEB_SESSIONKEY (RequestWebSessionKey)]
			// packets[93] = [C_GET_ON_VEHICLE (GetOnVehiclePacket)]
			// packets[94] = [C_GET_OFF_VEHICLE (GetOffVehiclePacket)]
			packets[95] = new PacketInfo(typeof(CM_HOUSE_TELEPORT_BACK), State.IN_GAME); // [C_RETURN_TO_HOUSEGATE (ReturnToHouseGatePacket)]
			packets[96] = new PacketInfo(typeof(CM_PLAYER_STATUS_INFO), State.IN_GAME); // [C_PARTY (PartyPacket)]
			packets[97] = new PacketInfo(typeof(CM_INVITE_TO_GROUP), State.IN_GAME); // [C_PARTY_BY_NAME (&LAB_1406224a0)]
			// packets[98] = [C_ALLI_CHANGE_GROUP (AllianceChangeGroupPacket)]
			// packets[99] = [C_UNUSED_19]
			packets[100] = new PacketInfo(typeof(CM_VIEW_PLAYER_DETAILS), State.IN_GAME); // [C_VIEW_OTHER_INVENTORY (ViewOtherInventoryPacket)]
			// packets[101] = [C_USE_CP_RESET_COST_REQ]
			// packets[102] = [C_UPDATE_USE_CP]
			packets[103] = new PacketInfo(typeof(CM_PING_REQUEST), State.IN_GAME); // [C_PING (&LAB_140632c80)]
			packets[104] = new PacketInfo(typeof(CM_GAMEGUARD), State.IN_GAME, State.AUTHED); // [C_NCGUARD (FUN_140632e50)]
			// packets[105] = [C_UNUSED_21]
			// packets[106] = [C_PLATE (PlatePacket)]
			packets[107] = new PacketInfo(typeof(CM_CLIENT_COMMAND_ROLL), State.IN_GAME); // [C_SIMPLE_DICE (SimpleDicePacket)]
			packets[108] = new PacketInfo(typeof(CM_GROUP_DISTRIBUTION), State.IN_GAME); // [C_SPLIT_GOLD (SplitGoldPacket)]
			// packets[109] = new PacketInfo(typeof(CM_SHOW_LOCATION), State.IN_GAME); // [C_GET_PK_COUNT (CheckPkPacket)] when writing /loc or /location in chat (response is SM_SYSTEM_MESSAGE.STR_CMD_LOCATION_DESC)
			packets[110] = new PacketInfo(typeof(CM_MARK_FRIENDLIST), State.IN_GAME); // [C_QUERY_BUDDY (QueryBuddyPacket)]
			packets[111] = new PacketInfo(typeof(CM_FRIEND_ADD), State.IN_GAME); // [C_ADD_BUDDY (AddBuddyPacket)]
			packets[112] = new PacketInfo(typeof(CM_FRIEND_DEL), State.IN_GAME); // [C_REMOVE_BUDDY (RemoveBuddyPacket)]
			// packets[113] = [C_SMS (SMSPacket)]
			packets[114] = new PacketInfo(typeof(CM_DUEL_REQUEST), State.IN_GAME); // [C_DUEL (DuelPacket)]
			// packets[115] = [C_UNUSED__03]
			packets[116] = new PacketInfo(typeof(CM_DELETE_ITEM), State.IN_GAME); // [C_DESTROY_ITEM (DestroyItemPacket)]
			packets[117] = new PacketInfo(typeof(CM_BROKER_SELL_WINDOW), State.IN_GAME); // [C_VENDOR_AVG_SOLDPRICE]
			packets[118] = new PacketInfo(typeof(CM_ABYSS_RANKING_LEGIONS), State.IN_GAME); // [C_REQUEST_ABYSS_GUILD_INFO (RequestAbyssGuildInfoPacket)]
			packets[119] = new PacketInfo(typeof(CM_PRIVATE_STORE), State.IN_GAME); // [C_PERSONAL_SHOP (PersonalShopPacket)]
			packets[120] = new PacketInfo(typeof(CM_PRIVATE_STORE_NAME), State.IN_GAME); // [C_SHOP_MSG (ShopMsgPacket)]
			packets[121] = new PacketInfo(typeof(CM_SUMMON_COMMAND), State.IN_GAME); // [C_PET_ORDER (PetOrderPacket)]
			// packets[122] = [C_GIVE_EXP_TO_PET (GiveExpToPetPacket)]
			packets[123] = new PacketInfo(typeof(CM_BROKER_LIST), State.IN_GAME); // [C_VENDOR_ITEMLIST_CATEGORY (VendorItemListCategoryPacket)]
			packets[124] = new PacketInfo(typeof(CM_BROKER_SEARCH), State.IN_GAME); // [C_VENDOR_ITEMLIST_NAME (VendorItemListNamePacket)]
			packets[125] = new PacketInfo(typeof(CM_BROKER_REGISTERED), State.IN_GAME); // [C_VENDOR_MYLIST (VendorMyListPacket)]
			packets[126] = new PacketInfo(typeof(CM_BUY_BROKER_ITEM), State.IN_GAME); // [C_VENDOR_BUY (VendorBuyPacket)]
			packets[127] = new PacketInfo(typeof(CM_REGISTER_BROKER_ITEM), State.IN_GAME); // [C_VENDOR_COMMIT (&LAB_1406272c0)]
			packets[128] = new PacketInfo(typeof(CM_BROKER_CANCEL_REGISTERED), State.IN_GAME); // [C_VENDOR_CANCEL (VendorCancelPacket)]
			packets[129] = new PacketInfo(typeof(CM_BROKER_SETTLE_LIST), State.IN_GAME); // [C_VENDOR_MYLOG (VendorMyLogPacket)]
			packets[130] = new PacketInfo(typeof(CM_BROKER_SETTLE_ACCOUNT), State.IN_GAME); // [C_VENDOR_COLLECT (VendorCollectPacket)]
			// packets[131] = [C_COMMPACKET (CommPacket)]
			packets[132] = new PacketInfo(typeof(CM_SEND_MAIL), State.IN_GAME); // [C_MAIL_WRITE (MailWritePacket)]
			packets[133] = new PacketInfo(typeof(CM_CHECK_MAIL_LIST), State.IN_GAME); // [C_MAIL_LIST (MailListPacket)]
			packets[134] = new PacketInfo(typeof(CM_READ_MAIL), State.IN_GAME); // [C_MAIL_READ (MailReadPacket)]
			// packets[135] = [C_MAIL_SETREAD (MailSetReadPacket)]
			packets[136] = new PacketInfo(typeof(CM_GET_MAIL_ATTACHMENT), State.IN_GAME); // [C_MAIL_GETITEM (MailGetItemPacket)]
			packets[137] = new PacketInfo(typeof(CM_DELETE_MAIL), State.IN_GAME); // [C_MAIL_DELETE (MailDeletePacket)]
			// packets[138] = [C_DICE (DicePacket)]
			packets[139] = new PacketInfo(typeof(CM_TITLE_SET), State.IN_GAME); // [C_CHANGE_TITLE (ChangeTitlePacket)]
			// packets[140] = [C_REMOVE_TITLE (&LAB_14062bca0)]
			packets[141] = new PacketInfo(typeof(CM_CRAFT), State.IN_GAME); // [C_COMBINE (&LAB_14062c030)]
			// packets[142] = [C_LOCATION (LocationPacket)]
			// packets[143] = [C_MOVEBACK (MoveBackPacket)]
			// packets[144] = [C_RECONNECT (ReconnectPacket)]
			packets[145] = new PacketInfo(typeof(CM_QUESTIONNAIRE), State.IN_GAME); // [C_POLL_ANSWER (PollAnswer)]
			packets[146] = new PacketInfo(typeof(CM_REJECT_REVIVE), State.IN_GAME); // [C_REJECT_RESURRECT_BY_OTHER (RejectResurrectByOther)]
			packets[147] = new PacketInfo(typeof(CM_HEADING_UPDATE), State.IN_GAME); // [C_SPIN (SpinPacket)]
			packets[148] = new PacketInfo(typeof(CM_TELEPORT_SELECT), State.IN_GAME); // [C_DESTINATION_AIRPORT (DestinationAirport)]
			packets[149] = new PacketInfo(typeof(CM_L2AUTH_LOGIN_CHECK), State.CONNECTED); // [C_L2AUTH_LOGIN (L2AuthLoginPacket)]
			packets[150] = new PacketInfo(typeof(CM_CHARACTER_LIST), State.AUTHED); // [C_CHARACTER_LIST (&LAB_14062dce0)]
			packets[151] = new PacketInfo(typeof(CM_CREATE_CHARACTER), State.AUTHED); // [C_CREATE_CHARACTER (CreateCharacterPacket)]
			packets[152] = new PacketInfo(typeof(CM_DELETE_CHARACTER), State.AUTHED); // [C_DELETE_CHARACTER (DeleteCharacterPacket)]
			packets[153] = new PacketInfo(typeof(CM_RESTORE_CHARACTER), State.AUTHED); // [C_RESTORE_CHARACTER (RestoreCharacterPacket)]
			packets[154] = new PacketInfo(typeof(CM_START_LOOT), State.IN_GAME); // [C_LOOT (LootPacket)]
			packets[155] = new PacketInfo(typeof(CM_LOOT_ITEM), State.IN_GAME); // [C_LOOT_ITEM (&LAB_140630080)]
			packets[156] = new PacketInfo(typeof(CM_MOVE_ITEM), State.IN_GAME); // [C_MOVE_ITEM_TO_ANOTHER_SLOT (&LAB_140630410)]
			packets[157] = new PacketInfo(typeof(CM_SPLIT_ITEM), State.IN_GAME); // [C_MOVE_STACKABLE_ITEM (MoveStackableItemPacket)]
			packets[158] = new PacketInfo(typeof(CM_SHOW_BLOCKLIST), State.IN_GAME); // [C_RECIPE_LIST (RecipeListPacket)]
			packets[159] = new PacketInfo(typeof(CM_PLAYER_SEARCH), State.IN_GAME); // [C_SEARCH_USERS (SearchUserPacket)]
			packets[160] = new PacketInfo(typeof(CM_LEGION_UPLOAD_INFO), State.IN_GAME); // [C_UPLOAD_GUILD_EMBLEM_IMG_BEGIN (UploadGuildEmblemImgBegin)]
			packets[161] = new PacketInfo(typeof(CM_LEGION_UPLOAD_EMBLEM), State.IN_GAME); // [C_UPLOAD_GUILD_EMBLEM_IMG_DATA (&LAB_1406317e0)]
			packets[162] = new PacketInfo(typeof(CM_READ_EXPRESS_MAIL), State.IN_GAME); // [C_MAIL_POSTMAN (&LAB_14062af30)]
			packets[163] = new PacketInfo(typeof(CM_SUBZONE_CHANGE), State.IN_GAME); // [C_ALL_FOG_CLEARED (AllFogClearedPacket)]
			packets[164] = new PacketInfo(typeof(CM_QUEST_SHARE), State.IN_GAME); // [C_SHARE_QUEST (ShareQuestPacket)]
			// packets[165] = [C_ADD_BUDDY_ANS (&LAB_140633c60)]
			packets[166] = new PacketInfo(typeof(CM_BLOCK_ADD), State.IN_GAME); // [C_ADD_BLOCK (AddBlockPacket)]
			packets[167] = new PacketInfo(typeof(CM_BLOCK_DEL), State.IN_GAME); // [C_REMOVE_BLOCK (RemoveBlockPacket)]
			// packets[168] = [C_QUERY_BLOCK (QueryBlockPacket)]
			// packets[169] = [C_CHANGE_BLOCK_NAME (ChangeBlockNamePacket)]
			packets[170] = new PacketInfo(typeof(CM_FRIEND_STATUS), State.IN_GAME); // [C_CUR_STATUS (CurrentStatusPacket)]
			// packets[171] = new PacketInfo(typeof(CM_VIRTUAL_AUTH), State.AUTHED, State.IN_GAME); // [C_VIRTUAL_AUTH (VirtualAuthPacket)]
			packets[172] = new PacketInfo(typeof(CM_CHANGE_CHANNEL), State.IN_GAME); // [C_CHANGE_CHANNEL (ChangeChannelPacket)]
			// packets[173] = [C_FOLLOW_CHANNEL (FollowChannelPacket)]
			packets[174] = new PacketInfo(typeof(CM_CHAT_AUTH), State.IN_GAME); // [C_SIGN_CLIENT (SignClientPacket)]
			packets[175] = new PacketInfo(typeof(CM_MACRO_CREATE), State.IN_GAME); // [C_SAVE_MACRO (SaveMacroPacket)]
			packets[176] = new PacketInfo(typeof(CM_MACRO_DELETE), State.IN_GAME); // [C_DELETE_MACRO (DeleteMacroPacket)]
			packets[177] = new PacketInfo(typeof(CM_CHECK_NICKNAME), State.AUTHED); // [C_CHECK_EXIST (CheckExistPacket)]
			packets[178] = new PacketInfo(typeof(CM_REPLACE_ITEM), State.IN_GAME); // [C_SWAP_ITEM_SLOT (SwapItemSlotPacket)]
			packets[179] = new PacketInfo(typeof(CM_BLOCK_SET_REASON), State.IN_GAME); // [C_CHANGE_BLOCK_MEMO (&LAB_140636e00)]
			packets[180] = new PacketInfo(typeof(CM_DEBUG_COMMAND), State.IN_GAME); // [C_DEBUG_COMMAND (DebugCommandPacket)]
			packets[181] = new PacketInfo(typeof(CM_SHOW_BRAND), State.IN_GAME); // [C_TACTICS_SIGN (&LAB_1406375b0)]
			// packets[182] = new PacketInfo(typeof(CM_GM_COMMAND_ACTION), State.IN_GAME); // [C_SPECTATOR_MODE (SpectatorModePacket)]
			packets[183] = new PacketInfo(typeof(CM_RECONNECT_AUTH), State.AUTHED); // [C_RECONNECT_AUTH (&LAB_1406384c0)]
			packets[184] = new PacketInfo(typeof(CM_GROUP_LOOT), State.IN_GAME); // [C_GROUP_ITEM_DIST (&LAB_140638740)]
			packets[185] = new PacketInfo(typeof(CM_DISTRIBUTION_SETTINGS), State.IN_GAME); // [C_GROUP_CHANGE_LOOTDIST (GroupChangeLootDistPacket)]
			packets[186] = new PacketInfo(typeof(CM_MAY_LOGIN_INTO_GAME), State.AUTHED); // [C_SA_ACCOUNT_ITEM_QUERY (SAAccountItemQueryPacket)]
			// packets[187] = [C_SA_ACCOUNT_ITEM_ACK (SAAccountItemAckPacket)]
			packets[188] = new PacketInfo(typeof(CM_ABYSS_RANKING_PLAYERS), State.IN_GAME); // [C_REQUEST_ABYSS_RANKER_INFO (RequestAbyssRankerInfoPacket)]
			packets[189] = new PacketInfo(typeof(CM_MAC_ADDRESS), State.CONNECTED); // [C_ROUTE_INFO (RouteInfoPacket)]
			// packets[190] = // [C_CHECK_MESSAGE (&LAB_140639ed0)] sent when receiving S_CHECK_MESSAGE (opcode 80), contains 16 (static) bytes which change every 5s, maybe some kind of consistency check
			packets[191] = new PacketInfo(typeof(CM_REPORT_PLAYER), State.IN_GAME); // [C_ACCUSE_CHARACTER (AccuseCharacterPacket)]
			packets[192] = new PacketInfo(typeof(CM_INSTANCE_INFO), State.IN_GAME); // [C_INSTANCE_DUNGEON_COOLTIMES (InstanceDungeonCooltimePacket)]
			// packets[193] = // [C_SHOP_REQUEST]
			packets[194] = new PacketInfo(typeof(CM_SHOW_RESTRICTIONS), State.IN_GAME); // [C_ASK_BOT_POINT (&LAB_14063b3e0)] when writing /restriction in chat
			// packets[195] = new PacketInfo(typeof(CM_SUMMON_TELEPORT_RESPONSE), State.IN_GAME); // [C_RECALLED_BY_OTHER_ANSWER (&LAB_14063b5b0)] when player accepts/declines SM_SUMMON_TELEPORT_REQUEST window
			packets[196] = new PacketInfo(typeof(CM_SHOW_MAP), State.IN_GAME); // [C_REQUEST_SERIAL_KILLER_LIST (RequestSerialKillerListPacket)]
			packets[197] = new PacketInfo(typeof(CM_APPEARANCE), State.IN_GAME); // [C_ADDED_SERVICE_REQUEST (&LAB_14063ba60)]
			// packets[198] = [C_SNDC_CHECK_MESSAGE (SndcCheckMessagePacket)]
			// packets[199] = [C_GGAUTH_CHECK_ANSWER (GGAuthCheckAnswerPacket)]
			packets[200] = new PacketInfo(typeof(CM_AUTO_GROUP), State.IN_GAME); // [C_MATCHMAKER_REQ (MatchMakerReqPacket)]
			packets[201] = new PacketInfo(typeof(CM_SUMMON_MOVE), State.IN_GAME); // [C_CLIENTSIDE_NPC_MOVE (NpcMovePacket)]
			packets[202] = new PacketInfo(typeof(CM_SUMMON_EMOTION), State.IN_GAME); // [C_CLIENTSIDE_NPC_ACTION (NpcActionPacket)]
			packets[203] = new PacketInfo(typeof(CM_SUMMON_ATTACK), State.IN_GAME); // [C_CLIENTSIDE_NPC_ATTACK (NpcAttackPacket)]
			// packets[204] = // [C_CLIENTSIDE_NPC_BLINK (NpcReturnBlinkPacket)] sent when receiving S_CLIENTSIDE_NPC_BLINK (opcode 186)
			packets[205] = new PacketInfo(typeof(CM_SUMMON_CASTSPELL), State.IN_GAME); // [C_CLIENTSIDE_NPC_USE_SKILL (NpcUseSkillPacket)]
			packets[206] = new PacketInfo(typeof(CM_FUSION_WEAPONS), State.IN_GAME); // [C_COMPOUND_2H_WEAPON (&LAB_14063f670)]
			packets[207] = new PacketInfo(typeof(CM_BREAK_WEAPONS), State.IN_GAME); // [C_REMOVE_COMPOUND (RemoveCompoundOfTwoHandWeaponPacket)]
			packets[208] = new PacketInfo(typeof(CM_COMPOSITE_STONES), State.IN_GAME); // [C_COMPOUND_ENCHANT_ITEM (FUN_1406457b0)]
			// packets[209] = new PacketInfo(typeof(CM_TIME_CHECK_QUIT), State.IN_GAME); // [C_ASK_GLOBAL_PLAYTIME_FATIGUE_INFO (AskGlobalPlaytimeFatigueInfoPacket)]
			packets[210] = new PacketInfo(typeof(CM_CHARACTER_PASSKEY), State.AUTHED); // [C_2ND_PASSWORD (SecondPasswordPacket)]
			// packets[211] = [C_UNUSED_2ND_PASSWORD1]
			// packets[212] = [C_UNUSED_2ND_PASSWORD2]
			packets[213] = new PacketInfo(typeof(CM_CHECK_MAIL_UNK), State.IN_GAME); // [C_SA_GOODSLIST (ShopAgent2GoodsList)] TODO
			// packets[214] = [C_SA_CONFIRMGOODS (ShopAgent2ConfirmGoods)]
			// packets[215] = new PacketInfo(typeof(CM_DIRECT_ENTER_WORLD), State.IN_GAME); // [C_REQUEST_DIRECT_ENTER_WORLD (RequestDirectEnterWorldPacket)]
			// packets[216] = new PacketInfo(typeof(CM_REQUEST_BEGINNER_SERVER), State.IN_GAME); // [C_REQUEST_BEGINNER_SERVER (RequestBeginnerServerPacket)]
			// packets[217] = new PacketInfo(typeof(CM_REQUEST_RETURN_SERVER), State.IN_GAME); // [C_REQUEST_RETURN_SERVER (RequestReturnServerPacket)]
			packets[218] = new PacketInfo(typeof(CM_GET_HOUSE_BIDS), State.IN_GAME); // [C_REQUEST_AUCTION_LIST (RequestAuctionList)]
			packets[219] = new PacketInfo(typeof(CM_REGISTER_HOUSE), State.IN_GAME); // [C_REQUEST_AUCTION_REGISTER (&LAB_140642310)]
			// packets[220] = [C_REQUEST_AUCTION_CANCEL]
			packets[221] = new PacketInfo(typeof(CM_PLACE_BID), State.IN_GAME); // [C_REQUEST_AUCTION_BET (RequestAuctionBet)]
			packets[222] = new PacketInfo(typeof(CM_HOUSE_TELEPORT), State.IN_GAME); // [C_REQUEST_HOUSING_TELEPORT (RequestHousingTeleport)]
			packets[223] = new PacketInfo(typeof(CM_HOUSE_PAY_RENT), State.IN_GAME); // [C_REQUEST_HOUSING_CHARGE_FEE (RequestHousingChargeFee)]
			packets[224] = new PacketInfo(typeof(CM_USE_HOUSE_OBJECT), State.IN_GAME); // [C_USE_HOUSING_OBJECT (UseHousingObjectPacket)]
			packets[225] = new PacketInfo(typeof(CM_RELEASE_OBJECT), State.IN_GAME); // [C_CANCEL_USE_HOUSING_OBJECT (CancelUseHousingObjectPacket)]
			packets[226] = new PacketInfo(typeof(CM_HOUSE_OPEN_DOOR), State.IN_GAME); // [C_USE_HOUSING_DOOR (UseHousingDoorPacket)]
			// packets[227] = new PacketInfo(typeof(CM_IN_GAME_SHOP_INFO), State.IN_GAME); // [C_REQUEST_WEBNOTIFY_CLEAR (WebNotifyClearPacket)]
			// packets[228] = [C_HOUSING_REFRESH_TOKEN_REQ (HousingRefreshTokenReqPacket)]
			packets[229] = new PacketInfo(typeof(CM_GF_WEBSHOP_TOKEN_REQUEST), State.IN_GAME); // [C_GF_WEBSHOP_TOKEN_REQ (GFWebshopTokenReqPacket)]
			packets[230] = new PacketInfo(typeof(CM_SHOW_FRIENDLIST), State.IN_GAME); // [C_OFFLINE_BUDDY_LIST (OfflineBuddyList)]
			// packets[231] = [C_ANSWER_OFFLINE_BUDDY_REQUEST (AnswerOfflineBuddy)]
			packets[232] = new PacketInfo(typeof(CM_CHALLENGE_LIST), State.IN_GAME); // [C_CHALLENGE_TASK (&LAB_140645410)]
			packets[233] = new PacketInfo(typeof(CM_BONUS_TITLE), State.IN_GAME); // [C_CHANGE_ATTR_TITLE (ChangeAttrTitlePacket)]
			packets[234] = new PacketInfo(typeof(CM_USE_CHARGE_SKILL), State.IN_GAME); // [C_FIRE_CHARGE_SKILL (FUN_14060ed30)]
			packets[235] = new PacketInfo(typeof(CM_TUNE), State.IN_GAME); // [C_IDENTIFY_ITEM (IdentifyItem)]
			packets[236] = new PacketInfo(typeof(CM_SELECT_DECOMPOSABLE), State.IN_GAME); // [C_SELECT_DISASSEMBLY_ITEM (&LAB_140646480)]
			packets[237] = new PacketInfo(typeof(CM_MEGAPHONE), State.IN_GAME); // [C_MEGAPHONE (MegaphonePacket)]
			packets[238] = new PacketInfo(typeof(CM_TUNE_RESULT), State.IN_GAME); // [C_ANSWER_REIDENTIFY (AnswerReidentifyPacket)]
			packets[239] = new PacketInfo(typeof(CM_FRIEND_SET_MEMO), State.IN_GAME); // [C_CHANGE_BUDDY_MEMO (FUN_140647090)]
			packets[240] = new PacketInfo(typeof(CM_UNWRAP_ITEM), State.IN_GAME); // [C_UNPACK_ITEM (UnpackItemPacket)]
			// packets[241] = [C_REQUEST_NP_LOGIN_GAMESVR]
			// packets[242] = [C_REQUEST_NP_CONSUME_TOKEN]
			// packets[243] = [C_REQUEST_NP_AUTH_TOKEN]
			packets[244] = new PacketInfo(typeof(CM_BIND_POINT_TELEPORT), State.IN_GAME);// [C_HOTSPOT]
			// packets[245] = [C_ACCUSE_CHAT_SPAMMER]
			packets[246] = new PacketInfo(typeof(CM_UPGRADE_ARCADE), State.IN_GAME); // [C_GOTCHA_REQUEST]
			packets[247] = new PacketInfo(typeof(CM_ITEM_PURIFICATION), State.IN_GAME); // [C_ITEM_UPGRADE]
			packets[248] = new PacketInfo(typeof(CM_ATREIAN_PASSPORT), State.IN_GAME); // [C_REQ_LOGIN_EVENT_REWARD]
			// packets[249] = [C_REQ_REGISTER_MONEY_TRADE]
	}

	public static AionClientPacket TryCreatePacket(ByteBuffer data, AionConnection client)
	{
		State state = client.GetState();
		int opcode = Crypt.DecodeClientPacketOpcode(data.GetShort() & 0xffff);
		data.SetPosition(data.Position() + 3); // skip static code (short) and secondary opcode (byte)
		PacketInfo packetInfo = opcode < 0 || opcode >= packets.Length ? null : packets[opcode];
		if (packetInfo == null)
		{
			client.SendUnknownClientPacketInfo(opcode);
			if (NetworkConfig.LOG_UNKNOWN_PACKETS)
				log.LogWarning(string.Format("Aion client sent data with unknown opcode: 0x{0:X3}, state={1} {2}{3}", opcode, state.ToString(), Environment.NewLine, NetworkUtils.ToHex(data)));
			return null;
		}
		if (!packetInfo.IsValid(state))
		{
			if (NetworkConfig.LOG_IGNORED_PACKETS)
				log.LogWarning(client + " sent " + packetInfo.GetPacketClassName() + " but the connections current state (" + state
					+ ") is invalid for this packet. Packet won't be instantiated.");
			return null;
		}
		return packetInfo.NewPacket(opcode, data, client);
	}

	private sealed class PacketInfo
	{
		private readonly Type packetType;
		private readonly Func<int, ISet<State>, AionClientPacket> packetConstructor;
		private readonly ISet<State> validStates;

		public PacketInfo(Type packetClass, State state, params State[] otherStates)
		{
			this.packetType = packetClass;
			this.packetConstructor = CompileConstructor(packetClass);
			this.validStates = new HashSet<State>(otherStates) { state };
		}

		public bool IsValid(State state)
		{
			return validStates.Contains(state);
		}

		public AionClientPacket NewPacket(int opCode, ByteBuffer buffer, AionConnection con)
		{
			AionClientPacket packet = packetConstructor(opCode, validStates);
			packet.SetBuffer(buffer);
			packet.SetConnection(con);
			return packet;
		}

		public string GetPacketClassName()
		{
			return packetType.Name;
		}

		private static Func<int, ISet<State>, AionClientPacket> CompileConstructor(Type packetClass)
		{
			ConstructorInfo ctor = packetClass.GetConstructor(new[] { typeof(int), typeof(ISet<State>) });
			if (ctor == null)
				throw new TypeInitializationException(packetClass.FullName, new MissingMethodException(packetClass.FullName + "(int, ISet<State>)"));
			ParameterExpression opcodeParam = Expression.Parameter(typeof(int), "opcode");
			ParameterExpression statesParam = Expression.Parameter(typeof(ISet<State>), "validStates");
			NewExpression body = Expression.New(ctor, opcodeParam, statesParam);
			return Expression.Lambda<Func<int, ISet<State>, AionClientPacket>>(body, opcodeParam, statesParam).Compile();
		}
	}
}
