using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Mail;

namespace Aion.GameServer.Services.Mail;

/// <summary>Java parity: services/mail/MailFormatter (Rolandas). Builds system mails from SYSTEM_MAIL_TEMPLATES with parameter-substitution: blackcloud cash-item, house maintenance/auction, abyss-siege reward, guild-dominion reward, custom abyss-defeat. Anonymous MailPart subclasses -> nested sealed classes capturing locals via ctor; Duration.ofMillis(...).toDays()->long ms/86400000; currentTimeMillis/1000->UtcNow.ToUnixTimeMilliseconds()/1000; Timestamp.toLocalDateTime()->DateTimeOffset.DateTime; getMonthValue/getDayOfMonth->Month/Day; Integer/Long.toString->ToString. MailTemplate/MailPart/House/SiegeLocation red-tolerated.</summary>
public sealed class MailFormatter
{
    public static void SendBlackCloudMail(string recipientName, int itemObjectId, int itemCount)
    {
        MailTemplate template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate("$$CASH_ITEM_MAIL", "", Race.PC_ALL);

        MailPart formatter = new BlackCloudMailPart(itemObjectId, itemCount);

        string title = template.GetFormattedTitle(formatter);
        string body = template.GetFormattedMessage(formatter);

        SystemMailService.SendMail("$$CASH_ITEM_MAIL", recipientName, title, body, itemObjectId, itemCount, 0, LetterType.BLACKCLOUD);
    }

    public static void SendHouseMaintenanceMail(House ownedHouse, string ownerName, long impoundTimeMillis, long kinah)
    {
        string templateName;
        long daysUntilImpoundment = (impoundTimeMillis - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 86400000L;
        if (daysUntilImpoundment <= 0)
            templateName = "$$HS_OVERDUE_3RD";
        else if (daysUntilImpoundment <= 7)
            templateName = "$$HS_OVERDUE_2ND";
        else if (daysUntilImpoundment <= 14)
            templateName = "$$HS_OVERDUE_1ST";
        else
            return;

        MailTemplate template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate(templateName, "", Race.PC_ALL);

        MailPart formatter = new HouseMaintenanceMailPart(ownedHouse, impoundTimeMillis);

        string title = template.GetFormattedTitle(null);
        string message = template.GetFormattedMessage(formatter);

        SystemMailService.SendMail(templateName, ownerName, title, message, 0, 0, kinah, LetterType.NORMAL);
    }

    public static void SendHouseAuctionMail(House ownedHouse, PlayerCommonData playerData, AuctionResult? result, long time, long returnKinah)
    {
        MailTemplate template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate("$$HS_AUCTION_MAIL", "", playerData.GetRace());
        if (ownedHouse == null || result is not AuctionResult auctionResult)
            return;

        MailPart formatter = new HouseAuctionMailPart(ownedHouse, time, auctionResult, playerData);

        string title = template.GetFormattedTitle(formatter);
        string message = template.GetFormattedMessage(formatter);

        SystemMailService.SendMail("$$HS_AUCTION_MAIL", playerData.GetName(), title, message, 0, 0, returnKinah, LetterType.NORMAL);
    }

    public static void SendAbyssRewardMail(SiegeLocation siegeLocation, PlayerCommonData playerData, AbyssSiegeLevel level, SiegeResult result,
        long time, int attachedItemObjId, long attachedItemCount, long attachedKinahCount)
    {
        MailTemplate template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate("$$ABYSS_REWARD_MAIL", "", playerData.GetRace());

        MailPart formatter = new AbyssRewardMailPart(siegeLocation, time, level, playerData, result);

        string title = template.GetFormattedTitle(formatter);
        string message = template.GetFormattedMessage(formatter);

        SystemMailService.SendMail("$$ABYSS_REWARD_MAIL", playerData.GetName(), title, message, attachedItemObjId, attachedItemCount, attachedKinahCount,
            LetterType.NORMAL);
    }

    public static void SendGuildDominionRewardMail(Player player, int territorialId, DateTimeOffset participantDate, int itemId, int itemCount)
    {
        MailTemplate template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate("$$GD_REWARD_MAIL", "", player.GetRace());
        DateTime participationDate = participantDate.DateTime;
        MailPart formatter = new GuildDominionMailPart(participationDate, territorialId, player);

        string title = template.GetFormattedTitle(formatter);
        string body = template.GetFormattedMessage(formatter);

        SystemMailService.SendMail("$$GD_REWARD_MAIL", player.GetName(), title, body, itemId, itemCount, 0, LetterType.NORMAL);
    }

    public static void SendCustomAbyssDefeatRewardMail(PlayerCommonData playerCommonData, int itemId, int itemCount)
    {
        SystemMailService.SendMail(playerCommonData.GetRace() == Race.ELYOS ? "%NPC:203700" : "%NPC:204052", // Fasimedes, Vidar
                playerCommonData.GetName(), "$901513", // Reward Statement
                "", itemId, itemCount, 0, LetterType.NORMAL);
    }

    private sealed class BlackCloudMailPart : MailPart
    {
        private readonly int itemObjectId;
        private readonly int itemCount;

        public BlackCloudMailPart(int itemObjectId, int itemCount)
        {
            this.itemObjectId = itemObjectId;
            this.itemCount = itemCount;
        }

        public override string GetParamValue(string name)
        {
            if ("itemid".Equals(name))
                return itemObjectId.ToString();
            else if ("count".Equals(name))
                return itemCount.ToString();
            else if ("unk1".Equals(name))
                return "0";
            else if ("purchasedate".Equals(name))
                return (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000).ToString();
            return "";
        }
    }

    private sealed class HouseMaintenanceMailPart : MailPart
    {
        private readonly House ownedHouse;
        private readonly long impoundTimeMillis;

        public HouseMaintenanceMailPart(House ownedHouse, long impoundTimeMillis)
        {
            this.ownedHouse = ownedHouse;
            this.impoundTimeMillis = impoundTimeMillis;
        }

        public override string GetParamValue(string name)
        {
            if ("address".Equals(name))
                return ownedHouse.GetAddress().GetId().ToString();
            else if ("datetime".Equals(name))
                return (impoundTimeMillis / 60000).ToString();
            return "";
        }
    }

    private sealed class HouseAuctionMailPart : MailPart
    {
        private readonly House ownedHouse;
        private readonly long time;
        private readonly AuctionResult result;
        private readonly PlayerCommonData playerData;

        public HouseAuctionMailPart(House ownedHouse, long time, AuctionResult result, PlayerCommonData playerData)
        {
            this.ownedHouse = ownedHouse;
            this.time = time;
            this.result = result;
            this.playerData = playerData;
        }

        public override string GetParamValue(string name)
        {
            if ("address".Equals(name))
                return ownedHouse.GetAddress().GetId().ToString();
            else if ("datetime".Equals(name))
                return (time / 1000).ToString();
            else if ("resultid".Equals(name))
                return result.GetId().ToString();
            else if ("raceid".Equals(name))
                return playerData.GetRace().GetRaceId().ToString();
            return "";
        }
    }

    private sealed class AbyssRewardMailPart : MailPart
    {
        private readonly SiegeLocation siegeLocation;
        private readonly long time;
        private readonly AbyssSiegeLevel level;
        private readonly PlayerCommonData playerData;
        private readonly SiegeResult result;

        public AbyssRewardMailPart(SiegeLocation siegeLocation, long time, AbyssSiegeLevel level, PlayerCommonData playerData, SiegeResult result)
        {
            this.siegeLocation = siegeLocation;
            this.time = time;
            this.level = level;
            this.playerData = playerData;
            this.result = result;
        }

        public override string GetParamValue(string name)
        {
            if ("siegelocid".Equals(name))
                return siegeLocation.GetTemplate().GetId().ToString();
            else if ("datetime".Equals(name))
                return (time / 1000).ToString();
            else if ("rankid".Equals(name))
                return level.GetId().ToString();
            else if ("raceid".Equals(name))
                return playerData.GetRace().GetRaceId().ToString();
            else if ("resultid".Equals(name))
                return result.GetId().ToString();
            return "";
        }
    }

    private sealed class GuildDominionMailPart : MailPart
    {
        private readonly DateTime participationDate;
        private readonly int territorialId;
        private readonly Player player;

        public GuildDominionMailPart(DateTime participationDate, int territorialId, Player player)
        {
            this.participationDate = participationDate;
            this.territorialId = territorialId;
            this.player = player;
        }

        public override string GetParamValue(string name)
        {
            string val = "";
            if ("month".Equals(name))
            {
                val = participationDate.Month.ToString();
            }
            else if ("day".Equals(name))
            {
                val = participationDate.Day.ToString();
            }
            else if ("territorial".Equals(name))
            {
                val = territorialId.ToString();
            }
            else if ("legionName".Equals(name))
            {
                val = player.GetLegion() == null ? "" : player.GetLegion().GetName();
            }
            return val;
        }
    }
}
