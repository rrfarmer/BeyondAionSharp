using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.ChatServer;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Time;
using GameServer = global::Aion.GameServer.GameServer;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_VERSION_CHECK (-Nemesiss-, Novo, cura, Neon). Client version-check + full server config blob. currentTimeMillis()/1000->DateTimeOffset; GameServer type aliased (namespace clash). GSConfig/MembershipConfig/GameServer/ChatServer/LoginServer/ServerTime/AtreianPassportService red-tolerated.</summary>
public class SM_VERSION_CHECK : AionServerPacket
{
    public const int INTERNAL_VERSION = 207;

    private int version;
    private EventTheme cityDecoration;

    public SM_VERSION_CHECK(EventTheme cityDecoration)
        : this(INTERNAL_VERSION, cityDecoration)
    {
    }

    public SM_VERSION_CHECK(int version, EventTheme cityDecoration)
    {
        this.version = version;
        this.cityDecoration = cityDecoration;
    }

    protected override void WriteImpl(AionConnection con)
    {
        int characterLimitCount = GSConfig.CHARACTER_LIMIT_COUNT;
        int limitFactionMode = GSConfig.CHARACTER_FACTION_LIMITATION_MODE;

        if (MembershipConfig.CHARACTER_ADDITIONAL_COUNT > characterLimitCount && MembershipConfig.CHARACTER_ADDITIONAL_ENABLE != 10)
            characterLimitCount = MembershipConfig.CHARACTER_ADDITIONAL_COUNT;

        if (GSConfig.ENABLE_RATIO_LIMITATION)
        {
            if (GameServer.GetRatiosFor(Race.ELYOS) > GSConfig.RATIO_MIN_VALUE)
                limitFactionMode = 1;
            else if (GameServer.GetRatiosFor(Race.ASMODIANS) > GSConfig.RATIO_MIN_VALUE)
                limitFactionMode = 2;
            else if (GameServer.GetCountFor(Race.ELYOS) + GameServer.GetCountFor(Race.ASMODIANS) > GSConfig.RATIO_HIGH_PLAYER_COUNT_DISABLING)
                limitFactionMode = 3;
        }

        if (version != INTERNAL_VERSION)
        {
            WriteC(1); // answerID
            // 0 - ok (no message)
            // 1 - The client version is not compatible with the game server.
            // 2 - The NPC script version is not compatible with the game server.
            // 3, 4, ... - An unknown error has occurred while checking the game server version.
            return;
        }
        WriteC(0); // answerID
        WriteC(NetworkConfig.GAMESERVER_ID); // serverId
        WriteD(150602); // GSServBuildDate (year month day)
        WriteD(150326); // DBServBuildDate (year month day)
        WriteD(0x00); // 0
        WriteD(150317); // NPCServBuildDate (year month day)
        WriteD(GameServer.START_TIME_SECONDS); // start server time in seconds
        WriteC(0x00); // 0
        WriteC(GSConfig.SERVER_COUNTRY_CODE); // country code
        WriteC(0x00); // 0
        WriteC((characterLimitCount * LoginServer.GetInstance().GetGameServerCount() * 0x10) | (limitFactionMode * 4) | GSConfig.CHARACTER_CREATION_MODE); // ServerFlag
        WriteD((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)); // PacketGenTimeOnServ (current UTC time in seconds)
        WriteH(GSConfig.MIN_SKILL_CAST_INTERVAL_MILLIS); // skillPacketDelay
        WriteC(1); // enableClientPet (always 1)
        WriteC(10); // minSendMailLevel (now 5 on official)
        WriteC(1); // minReceiveWhisperLevel (now 15 on official)
        WriteC(10); // minReceiveMailLevel
        WriteC(GSConfig.CHAT_SERVER_MIN_LEVEL); // ChannelChatLevel (min level to write in channel chats)
        WriteC(20); // Trial_ChannelChatLevel (now 1 on official)
        WriteC(20); // Trial_Channelchatwritelevel1 (before 30, now 66 on official)
        WriteC(1); // Trial_Channelchatwritelevel2 (always 1)
        WriteH(2); // MatchingCoolTimeSEC (always 2)
        WriteC(GSConfig.CHARACTER_REENTRY_TIME);
        WriteD(cityDecoration.GetId()); // SceneStatus
        WriteC(0); // fatigueKoreaUse
        WriteD(-ServerTime.GetStandardOffset()); // server time zone offset relative to UTC in seconds (excluding daylight savings)
        WriteC(0x04); // MaxHousingChargePerid
        WriteD(40014200); // spawn_version (4.0)
        WriteC(1); // DisposableItemTrade
        WriteD(0); // EnableNeutralChat (4.0)
        WriteD(0); // updateServerAddr (4.5)
        WriteH(3000); // updateServerPort (4.5)
        WriteH(1); // updateServerVersionCheckType (4.5)
        WriteC(0); // ReduceSellPriceforGold (4.7)
        WriteC(1); // RestrictWareandChargebyRank (4.7)
        WriteD(-ServerTime.GetDaylightSavings()); // TimeDstBias (servers current daylight saving time offset in seconds)
        WriteC(1); // SetDefaultAnimLength (4.7)
        WriteC(1); // 1 = activate stonespear siege (4.8)
        WriteD(0); // 1 = activate master server (4.8)
        WriteC(0); // unk/not used? (4.8)
        WriteC(AtreianPassportService.GetInstance().IsAtreianPassportDisabled() ? 1 : 0); // remove Atreian Passport menu entry 0/1 (4.8)
        WriteC(0); // Newbie or comeback icons/etc disable/enable? 0/1 (4.8)
        WriteC(0); // Disable evolution? 0/1 (4.8)
        WriteC(0); // Item destroy on Enchant? 0/1 (4.8)
        WriteC(0); // Disable some item wear level check? 0/1 (4.8)
        WriteC(0); // Item related enable/disable? 0/1 (4.8)
        WriteC(0); // Special page(rank points?) enable/disable? / CreateAllPlayerClass? 0/1 (4.8) // TODO test
        WriteD(GSConfig.ITEM_WRAP_LIMIT); // incFreeTradePackCount (4.8)
        WriteD(1000); // decDropProb (rate modifier, divide by 1000) not used? (4.8)
        WriteD(1000); // decUserSellItemPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // decUserSellAPPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incNpcSellItemMedalPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incNpcSellItemCoinPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incNpcSellItemAPPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incNpcSellItemQinaPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incNpcSellItemAPQinaPrice (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incItemUpgradeItemCnt (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incItemUpgradeAP (rate modifier, divide by 1000) (4.8)
        WriteD(1000); // incItemUpgradeQina (rate modifier, divide by 1000) (4.8)
        WriteC(0); // Augment disable or limit? 0/1 (4.8)
        WriteF(3.0f); // exp rate modifier? (4.8)
        WriteH(ChatServer.GetInstance().GetPublicIP().Length > 0 ? 1 : 0); // ChatServersCount
        if (ChatServer.GetInstance().GetPublicIP().Length > 0)
        {
            WriteC(0); // spacer or maybe id
            WriteB(ChatServer.GetInstance().GetPublicIP());
            WriteH(ChatServer.GetInstance().GetPublicPort());
        }
    }
}
