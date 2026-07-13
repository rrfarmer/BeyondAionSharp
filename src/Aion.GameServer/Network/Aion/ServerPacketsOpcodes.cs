using System;
using System.Collections.Generic;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Network.Aion;

/// <summary>
/// Java parity: network/aion/ServerPacketsOpcodes (-Nemesiss-). Static registry mapping each
/// AionServerPacket subclass to its client opcode. Generated from the Java source; entries whose
/// SM_ class is not (yet) present in the ServerPackets namespace are commented out so typeof resolves.
/// </summary>
public class ServerPacketsOpcodes
{
    private static readonly Dictionary<Type, int> opcodes = new Dictionary<Type, int>();

    static ServerPacketsOpcodes()
    {
        AddPacketOpcode(0, typeof(SM_VERSION_CHECK));
        AddPacketOpcode(1, typeof(SM_STATS_INFO));
        AddPacketOpcode(2, typeof(SM_GM_SHOW_PLAYER_STATUS));
        AddPacketOpcode(3, typeof(SM_STATUPDATE_HP));
        AddPacketOpcode(4, typeof(SM_STATUPDATE_MP));
        AddPacketOpcode(5, typeof(SmAttackStatus));
        AddPacketOpcode(6, typeof(SM_STATUPDATE_DP));
        AddPacketOpcode(7, typeof(SM_DP_INFO));
        AddPacketOpcode(8, typeof(SM_STATUPDATE_EXP));
        AddPacketOpcode(10, typeof(SM_NPC_ASSEMBLER));
        AddPacketOpcode(11, typeof(SM_LEGION_UPDATE_NICKNAME));
        AddPacketOpcode(12, typeof(SM_LEGION_HISTORY));
        AddPacketOpcode(13, typeof(SM_ENTER_WORLD_CHECK));
        AddPacketOpcode(14, typeof(SM_NPC_INFO));
        AddPacketOpcode(15, typeof(SM_PLAYER_SPAWN));
        AddPacketOpcode(17, typeof(SM_GATHERABLE_INFO));
        AddPacketOpcode(19, typeof(SM_GM_SEARCH));
        AddPacketOpcode(20, typeof(SM_TELEPORT_LOC));
        AddPacketOpcode(21, typeof(SM_POSITION_SELF));
        AddPacketOpcode(22, typeof(SM_DELETE));
        AddPacketOpcode(23, typeof(SM_LOGIN_QUEUE));
        AddPacketOpcode(24, typeof(SM_MESSAGE));
        AddPacketOpcode(25, typeof(SM_SYSTEM_MESSAGE));
        AddPacketOpcode(26, typeof(SM_INVENTORY_INFO));
        AddPacketOpcode(27, typeof(SM_INVENTORY_ADD_ITEM));
        AddPacketOpcode(28, typeof(SM_DELETE_ITEM));
        AddPacketOpcode(29, typeof(SM_INVENTORY_UPDATE_ITEM));
        AddPacketOpcode(30, typeof(SM_UI_SETTINGS));
        AddPacketOpcode(31, typeof(SM_PLAYER_STANCE));
        AddPacketOpcode(32, typeof(SM_PLAYER_INFO));
        AddPacketOpcode(33, typeof(SM_CASTSPELL));
        AddPacketOpcode(34, typeof(SM_GATHER_ANIMATION));
        AddPacketOpcode(35, typeof(SM_GATHER_UPDATE));
        AddPacketOpcode(36, typeof(SM_UPDATE_PLAYER_APPEARANCE));
        AddPacketOpcode(37, typeof(SM_EMOTION));
        AddPacketOpcode(38, typeof(SM_GAME_TIME));
        AddPacketOpcode(39, typeof(SM_TIME_CHECK));
        AddPacketOpcode(40, typeof(SM_LOOKATOBJECT));
        AddPacketOpcode(41, typeof(SM_TARGET_SELECTED));
        AddPacketOpcode(42, typeof(SM_SKILL_CANCEL));
        AddPacketOpcode(43, typeof(SM_CASTSPELL_RESULT));
        AddPacketOpcode(44, typeof(SM_SKILL_LIST));
        AddPacketOpcode(45, typeof(SM_SKILL_REMOVE));
        AddPacketOpcode(46, typeof(SM_SKILL_ACTIVATION));
        AddPacketOpcode(49, typeof(SM_ABNORMAL_STATE));
        AddPacketOpcode(50, typeof(SM_ABNORMAL_EFFECT));
        AddPacketOpcode(51, typeof(SM_SKILL_COOLDOWN));
        AddPacketOpcode(52, typeof(SM_QUESTION_WINDOW));
        AddPacketOpcode(53, typeof(SM_CLOSE_QUESTION_WINDOW));
        AddPacketOpcode(54, typeof(SM_ATTACK));
        AddPacketOpcode(55, typeof(SM_MOVE));
        AddPacketOpcode(57, typeof(SM_HEADING_UPDATE));
        AddPacketOpcode(58, typeof(SM_TRANSFORM));
        AddPacketOpcode(59, typeof(SM_GM_SHOW_PLAYER_SKILLS));
        AddPacketOpcode(60, typeof(SM_DIALOG_WINDOW));
        AddPacketOpcode(61, typeof(SM_HOUSE_UPDATE));
        AddPacketOpcode(62, typeof(SM_SELL_ITEM));
        AddPacketOpcode(63, typeof(SM_GM_SHOW_LEGION_INFO));
        AddPacketOpcode(64, typeof(SM_GM_BOOKMARK_ADD));
        AddPacketOpcode(65, typeof(SM_VIEW_PLAYER_DETAILS));
        AddPacketOpcode(66, typeof(SM_GM_SHOW_LEGION_MEMBERLIST));
        AddPacketOpcode(67, typeof(SM_WEATHER));
        AddPacketOpcode(68, typeof(SM_PLAYER_STATE));
        AddPacketOpcode(70, typeof(SM_ACTION_ANIMATION));
        AddPacketOpcode(71, typeof(SM_QUEST_LIST));
        AddPacketOpcode(72, typeof(SM_KEY));
        AddPacketOpcode(73, typeof(SM_SUMMON_PANEL_REMOVE));
        AddPacketOpcode(74, typeof(SM_EXCHANGE_REQUEST));
        AddPacketOpcode(75, typeof(SM_EXCHANGE_ADD_ITEM));
        AddPacketOpcode(77, typeof(SM_EXCHANGE_ADD_KINAH));
        AddPacketOpcode(78, typeof(SM_EXCHANGE_CONFIRMATION));
        AddPacketOpcode(79, typeof(SM_EMOTION_LIST));
        AddPacketOpcode(81, typeof(SM_TARGET_UPDATE));
        AddPacketOpcode(82, typeof(SM_HOUSE_EDIT));
        AddPacketOpcode(83, typeof(SM_PLASTIC_SURGERY));
        AddPacketOpcode(84, typeof(SM_CONQUEROR_PROTECTOR));
        AddPacketOpcode(85, typeof(SM_INFLUENCE_RATIO));
        AddPacketOpcode(86, typeof(SM_FORTRESS_STATUS));
        AddPacketOpcode(87, typeof(SM_CAPTCHA));
        AddPacketOpcode(88, typeof(SM_RENAME));
        AddPacketOpcode(89, typeof(SM_SHOW_NPC_ON_MAP));
        AddPacketOpcode(90, typeof(SM_GROUP_INFO));
        AddPacketOpcode(91, typeof(SM_GROUP_MEMBER_INFO));
        AddPacketOpcode(92, typeof(SM_RIDE_ROBOT));
        AddPacketOpcode(98, typeof(SM_QUIT_RESPONSE));
        AddPacketOpcode(99, typeof(SM_CHAT_WINDOW));
        AddPacketOpcode(101, typeof(SM_PET));
        AddPacketOpcode(103, typeof(SM_ITEM_COOLDOWN));
        AddPacketOpcode(104, typeof(SM_UPDATE_NOTE));
        AddPacketOpcode(105, typeof(SM_PLAY_MOVIE));
        AddPacketOpcode(110, typeof(SM_LEGION_INFO));
        AddPacketOpcode(111, typeof(SM_LEGION_ADD_MEMBER));
        AddPacketOpcode(112, typeof(SM_LEGION_LEAVE_MEMBER));
        AddPacketOpcode(113, typeof(SM_LEGION_UPDATE_MEMBER));
        AddPacketOpcode(114, typeof(SM_LEGION_UPDATE_TITLE));
        AddPacketOpcode(115, typeof(SM_ATTACK_RESPONSE));
        AddPacketOpcode(116, typeof(SM_HOUSE_REGISTRY));
        AddPacketOpcode(119, typeof(SM_LEGION_UPDATE_SELF_INTRO));
        // AddPacketOpcode(120, typeof(SM_RIFT_STATUS)); // not present in ServerPackets ns
        AddPacketOpcode(121, typeof(SM_INSTANCE_SCORE));
        AddPacketOpcode(122, typeof(SM_AUTO_GROUP));
        AddPacketOpcode(123, typeof(SM_QUEST_COMPLETED_LIST));
        AddPacketOpcode(124, typeof(SM_QUEST_ACTION));
        AddPacketOpcode(125, typeof(SM_GAMEGUARD));
        // AddPacketOpcode(126, typeof(SM_BUY_LIST)); // not present in ServerPackets ns
        AddPacketOpcode(127, typeof(SM_NEARBY_QUESTS));
        AddPacketOpcode(128, typeof(SM_PING_RESPONSE));
        AddPacketOpcode(130, typeof(SM_CUBE_UPDATE));
        AddPacketOpcode(131, typeof(SM_HOUSE_SCRIPTS));
        AddPacketOpcode(132, typeof(SM_FRIEND_LIST));
        AddPacketOpcode(134, typeof(SM_PRIVATE_STORE));
        AddPacketOpcode(135, typeof(SM_GROUP_LOOT));
        AddPacketOpcode(136, typeof(SM_ABYSS_RANK_UPDATE));
        AddPacketOpcode(137, typeof(SM_MAY_LOGIN_INTO_GAME));
        AddPacketOpcode(138, typeof(SM_ABYSS_RANKING_PLAYERS));
        AddPacketOpcode(139, typeof(SM_ABYSS_RANKING_LEGIONS));
        AddPacketOpcode(140, typeof(SM_INSTANCE_STAGE_INFO));
        AddPacketOpcode(141, typeof(SM_INSTANCE_INFO));
        AddPacketOpcode(142, typeof(SM_PONG));
        AddPacketOpcode(144, typeof(SM_KISK_UPDATE));
        AddPacketOpcode(145, typeof(SM_PRIVATE_STORE_NAME));
        AddPacketOpcode(146, typeof(SM_BROKER_SERVICE));
        AddPacketOpcode(147, typeof(SM_INSTANCE_COUNT_INFO));
        AddPacketOpcode(148, typeof(SM_MOTION));
        // AddPacketOpcode(149, typeof(SM_BROKER_SETTLED_LIST)); // not present in ServerPackets ns
        AddPacketOpcode(150, typeof(SM_UNK_3_5_1));
        AddPacketOpcode(151, typeof(SM_TRADE_IN_LIST));
        AddPacketOpcode(152, typeof(SM_SECURITY_TOKEN));
        AddPacketOpcode(153, typeof(SM_SUMMON_PANEL));
        AddPacketOpcode(154, typeof(SM_SUMMON_OWNER_REMOVE));
        AddPacketOpcode(155, typeof(SM_SUMMON_UPDATE));
        AddPacketOpcode(156, typeof(SM_TRANSFORM_IN_SUMMON));
        AddPacketOpcode(157, typeof(SM_LEGION_MEMBERLIST));
        AddPacketOpcode(158, typeof(SM_LEGION_EDIT));
        // Opcode 159 [S_SHOP_POINT_INFO] is unsupported.
        AddPacketOpcode(161, typeof(SM_MAIL_SERVICE));
        AddPacketOpcode(162, typeof(SM_SUMMON_USESKILL));
        AddPacketOpcode(163, typeof(SM_WINDSTREAM));
        AddPacketOpcode(164, typeof(SM_WINDSTREAM_ANNOUNCE));
        AddPacketOpcode(165, typeof(SM_RECIPE_COOLDOWN));
        AddPacketOpcode(166, typeof(SM_FIND_GROUP));
        AddPacketOpcode(167, typeof(SM_REPURCHASE));
        AddPacketOpcode(168, typeof(SM_WAREHOUSE_INFO));
        AddPacketOpcode(169, typeof(SM_WAREHOUSE_ADD_ITEM));
        AddPacketOpcode(170, typeof(SM_DELETE_WAREHOUSE_ITEM));
        AddPacketOpcode(171, typeof(SM_WAREHOUSE_UPDATE_ITEM));
        // Opcodes 172-174 [S_SHOP_*] are unsupported.
        AddPacketOpcode(175, typeof(SM_ICON_INFO));
        AddPacketOpcode(176, typeof(SM_TITLE_INFO));
        AddPacketOpcode(177, typeof(SM_CHARACTER_SELECT));
        AddPacketOpcode(178, typeof(SM_GROUP_DATA_EXCHANGE));
        // AddPacketOpcode(179, typeof(SM_BROKER_REGISTERED_LIST)); // not present in ServerPackets ns
        AddPacketOpcode(180, typeof(SM_CRAFT_ANIMATION));
        AddPacketOpcode(181, typeof(SM_CRAFT_UPDATE));
        AddPacketOpcode(182, typeof(SM_ASCENSION_MORPH));
        AddPacketOpcode(183, typeof(SM_ITEM_USAGE_ANIMATION));
        AddPacketOpcode(184, typeof(SM_CUSTOM_SETTINGS));
        AddPacketOpcode(185, typeof(SM_DUEL));
        AddPacketOpcode(187, typeof(SM_PET_EMOTE));
        AddPacketOpcode(191, typeof(SM_QUESTIONNAIRE));
        AddPacketOpcode(193, typeof(SM_DIE));
        AddPacketOpcode(194, typeof(SM_RESURRECT));
        AddPacketOpcode(195, typeof(SM_FORCED_MOVE));
        AddPacketOpcode(196, typeof(SM_TELEPORT_MAP));
        AddPacketOpcode(197, typeof(SM_USE_OBJECT));
        AddPacketOpcode(199, typeof(SM_L2AUTH_LOGIN_CHECK));
        AddPacketOpcode(200, typeof(SM_CHARACTER_LIST));
        AddPacketOpcode(201, typeof(SM_CREATE_CHARACTER));
        AddPacketOpcode(202, typeof(SM_DELETE_CHARACTER));
        AddPacketOpcode(203, typeof(SM_RESTORE_CHARACTER));
        AddPacketOpcode(204, typeof(SM_POSITION));
        AddPacketOpcode(205, typeof(SM_LOOT_STATUS));
        AddPacketOpcode(206, typeof(SM_LOOT_ITEMLIST));
        AddPacketOpcode(207, typeof(SM_RECIPE_LIST));
        AddPacketOpcode(208, typeof(SM_MANTRA_EFFECT));
        AddPacketOpcode(209, typeof(SM_SIEGE_LOCATION_INFO));
        AddPacketOpcode(210, typeof(SM_SIEGE_LOCATION_STATE));
        AddPacketOpcode(211, typeof(SM_PLAYER_SEARCH));
        AddPacketOpcode(213, typeof(SM_LEGION_SEND_EMBLEM));
        AddPacketOpcode(214, typeof(SM_LEGION_SEND_EMBLEM_DATA));
        AddPacketOpcode(215, typeof(SM_LEGION_UPDATE_EMBLEM));
        AddPacketOpcode(217, typeof(SM_PLAYER_REGION));
        AddPacketOpcode(218, typeof(SM_SHIELD_EFFECT));
        AddPacketOpcode(220, typeof(SM_ABYSS_ARTIFACT_INFO3));
        AddPacketOpcode(221, typeof(SM_HOUSE_TELEPORT));
        AddPacketOpcode(222, typeof(SM_FRIEND_RESPONSE));
        AddPacketOpcode(223, typeof(SM_BLOCK_RESPONSE));
        AddPacketOpcode(224, typeof(SM_BLOCK_LIST));
        AddPacketOpcode(225, typeof(SM_FRIEND_NOTIFY));
        AddPacketOpcode(226, typeof(SM_TOWNS_LIST));
        AddPacketOpcode(227, typeof(SM_FRIEND_STATUS));
        // AddPacketOpcode(228, typeof(SM_VIRTUAL_AUTH)); // not present in ServerPackets ns
        AddPacketOpcode(229, typeof(SM_CHANNEL_INFO));
        AddPacketOpcode(230, typeof(SM_CHAT_INIT));
        AddPacketOpcode(231, typeof(SM_MACRO_LIST));
        AddPacketOpcode(232, typeof(SM_MACRO_RESULT));
        AddPacketOpcode(233, typeof(SM_NICKNAME_CHECK_RESPONSE));
        AddPacketOpcode(235, typeof(SM_BIND_POINT_INFO));
        AddPacketOpcode(236, typeof(SM_RIFT_ANNOUNCE));
        AddPacketOpcode(237, typeof(SM_ABYSS_RANK));
        AddPacketOpcode(238, typeof(SM_ACCOUNT_PROPERTIES));
        AddPacketOpcode(240, typeof(SM_FRIEND_UPDATE));
        AddPacketOpcode(241, typeof(SM_LEARN_RECIPE));
        AddPacketOpcode(242, typeof(SM_RECIPE_DELETE));
        AddPacketOpcode(243, typeof(SM_FORTRESS_INFO));
        AddPacketOpcode(244, typeof(SM_FLY_TIME));
        AddPacketOpcode(245, typeof(SM_ALLIANCE_INFO));
        AddPacketOpcode(246, typeof(SM_ALLIANCE_MEMBER_INFO));
        AddPacketOpcode(247, typeof(SM_LEAVE_GROUP_MEMBER));
        AddPacketOpcode(249, typeof(SM_SHOW_BRAND));
        AddPacketOpcode(250, typeof(SM_ALLIANCE_READY_CHECK));
        AddPacketOpcode(252, typeof(SM_PRICES));
        AddPacketOpcode(253, typeof(SM_TRADELIST));
        AddPacketOpcode(255, typeof(SM_RECONNECT_KEY));
        AddPacketOpcode(256, typeof(SM_HOUSE_BIDS));
        AddPacketOpcode(259, typeof(SM_RECEIVE_BIDS));
        AddPacketOpcode(262, typeof(SM_HOUSE_PAY_RENT));
        AddPacketOpcode(263, typeof(SM_HOUSE_OWNER_INFO));
        AddPacketOpcode(264, typeof(SM_OBJECT_USE_UPDATE));
        AddPacketOpcode(266, typeof(SM_PACKAGE_INFO_NOTIFY));
        AddPacketOpcode(268, typeof(SM_HOUSE_OBJECT));
        AddPacketOpcode(269, typeof(SM_DELETE_HOUSE_OBJECT));
        AddPacketOpcode(270, typeof(SM_HOUSE_OBJECTS));
        AddPacketOpcode(271, typeof(SM_HOUSE_RENDER));
        AddPacketOpcode(272, typeof(SM_DELETE_HOUSE));
        AddPacketOpcode(274, typeof(SM_GF_WEBSHOP_TOKEN_RESPONSE));
        AddPacketOpcode(275, typeof(SM_HOUSE_ACQUIRE));
        AddPacketOpcode(276, typeof(SM_STATS_STATUS_UNK));
        AddPacketOpcode(279, typeof(SM_MARK_FRIENDLIST));
        AddPacketOpcode(280, typeof(SM_CHALLENGE_LIST));
        // AddPacketOpcode(283, typeof(SM_DISPUTE_LAND)); // not present in ServerPackets ns
        AddPacketOpcode(284, typeof(SM_FIRST_SHOW_DECOMPOSABLE));
        AddPacketOpcode(285, typeof(SM_MEGAPHONE));
        AddPacketOpcode(286, typeof(SM_SECONDARY_SHOW_DECOMPOSABLE));
        AddPacketOpcode(288, typeof(SM_TUNE_RESULT));
        AddPacketOpcode(289, typeof(SM_UNWRAP_ITEM));
        AddPacketOpcode(290, typeof(SM_QUEST_REPEAT));
        // AddPacketOpcode(291, typeof(SM_UNK_4_5)); // not present in ServerPackets ns
        AddPacketOpcode(292, typeof(SM_AFTER_TIME_CHECK_4_7_5));
        AddPacketOpcode(293, typeof(SM_AFTER_SIEGE_LOCINFO_475));
        AddPacketOpcode(296, typeof(SM_BIND_POINT_TELEPORT));
        AddPacketOpcode(298, typeof(SM_UPGRADE_ARCADE));
        AddPacketOpcode(299, typeof(SM_ATREIAN_PASSPORT));
        AddPacketOpcode(302, typeof(SM_LEGION_DOMINION_RANK));
        AddPacketOpcode(303, typeof(SM_LEGION_DOMINION_LOC_INFO));
    }

    private static void AddPacketOpcode(int opcode, Type packetClass) => opcodes[packetClass] = opcode;

    public static int GetOpcode(Type packetClass)
    {
        if (opcodes.TryGetValue(packetClass, out int opcode))
            return opcode;
        throw new ArgumentException("There is no opcode for " + packetClass + " defined.");
    }
}
