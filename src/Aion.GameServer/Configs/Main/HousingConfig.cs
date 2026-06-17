using System.Collections.Generic;
using Aion.Commons.Configuration;
using Aion.GameServer.Model.Templates.Housing;
using Quartz;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/HousingConfig (Rolandas). @Property defaults→field initializers; CronExpression/int[]/Map populated by config loader. Float fees converge HouseBids; bid mins converge House.GetDefaultAuctionPrice. HouseType red-tolerated.</summary>
public static class HousingConfig
{
    /// <summary>Distance Visibility. Key: gameserver.housing.visibility.distance (default 200)</summary>
    [Property(key: "gameserver.housing.visibility.distance", defaultValue: "200")]
    public static float VISIBILITY_DISTANCE = 200f;

    /// <summary>Key: gameserver.housing.auction.enable (default true)</summary>
    [Property(key: "gameserver.housing.auction.enable", defaultValue: "true")]
    public static bool ENABLE_HOUSE_AUCTIONS = true;

    /// <summary>Key: gameserver.housing.pay.enable (default true)</summary>
    [Property(key: "gameserver.housing.pay.enable", defaultValue: "true")]
    public static bool ENABLE_HOUSE_PAY = true;

    /// <summary>Key: gameserver.housing.auction.end_time (default "0 0 12 ? * SUN"). Initialized from the Java
    /// @Property defaultValue via CronExpressions.GetOrCreate (no invented value) so AuctionEndTask is ACTIVE like
    /// Java rather than silently AbstractCronTask-"deactivated" on a null expression.</summary>
    [Property(key: "gameserver.housing.auction.end_time", defaultValue: "0 0 12 ? * SUN")]
    public static CronExpression HOUSE_AUCTION_END_TIME = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 12 ? * SUN");

    /// <summary>Key: gameserver.housing.auction.register_days (Java @Property has NO defaultValue → DO_NOT_OVERWRITE;
    /// the field initializer {1,5} is kept when the key is absent, else bound from the shipped value "1, 5").
    /// HousingBidService indexes [0]/[1] unconditionally, so a null here NREs auction registration.</summary>
    [Property(key: "gameserver.housing.auction.register_days")]
    public static int[] HOUSE_AUCTION_REGISTER_DAYS = { 1, 5 };

    /// <summary>Key: gameserver.housing.maintain.time (default "0 0 0 ? * MON"). Initialized from the Java
    /// @Property defaultValue via CronExpressions.GetOrCreate (no invented value).</summary>
    [Property(key: "gameserver.housing.maintain.time", defaultValue: "0 0 0 ? * MON")]
    public static CronExpression HOUSE_MAINTENANCE_TIME = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0 ? * MON");

    /// <summary>Auction default bid prices. Key: gameserver.housing.auction.default_bid.house (default 0)</summary>
    [Property(key: "gameserver.housing.auction.default_bid.house", defaultValue: "0")]
    public static int HOUSE_MIN_BID = 0;
    /// <summary>Key: gameserver.housing.auction.default_bid.mansion (default 0)</summary>
    [Property(key: "gameserver.housing.auction.default_bid.mansion", defaultValue: "0")]
    public static int MANSION_MIN_BID = 0;
    /// <summary>Key: gameserver.housing.auction.default_bid.estate (default 0)</summary>
    [Property(key: "gameserver.housing.auction.default_bid.estate", defaultValue: "0")]
    public static int ESTATE_MIN_BID = 0;
    /// <summary>Key: gameserver.housing.auction.default_bid.palace (default 0)</summary>
    [Property(key: "gameserver.housing.auction.default_bid.palace", defaultValue: "0")]
    public static int PALACE_MIN_BID = 0;

    /// <summary>Auction minimal level required for bidding. Key: gameserver.housing.auction.bidding.min_level.house (default 0)</summary>
    [Property(key: "gameserver.housing.auction.bidding.min_level.house", defaultValue: "0")]
    public static int HOUSE_MIN_BID_LEVEL = 0;
    /// <summary>Key: gameserver.housing.auction.bidding.min_level.mansion (default 0)</summary>
    [Property(key: "gameserver.housing.auction.bidding.min_level.mansion", defaultValue: "0")]
    public static int MANSION_MIN_BID_LEVEL = 0;
    /// <summary>Key: gameserver.housing.auction.bidding.min_level.estate (default 0)</summary>
    [Property(key: "gameserver.housing.auction.bidding.min_level.estate", defaultValue: "0")]
    public static int ESTATE_MIN_BID_LEVEL = 0;
    /// <summary>Key: gameserver.housing.auction.bidding.min_level.palace (default 0)</summary>
    [Property(key: "gameserver.housing.auction.bidding.min_level.palace", defaultValue: "0")]
    public static int PALACE_MIN_BID_LEVEL = 0;

    /// <summary>Key: gameserver.housing.auction.registration_fee (default 0.3)</summary>
    [Property(key: "gameserver.housing.auction.registration_fee", defaultValue: "0.3")]
    public static float AUCTION_REGISTRATION_FEE_PERCENT = 0.3f;
    /// <summary>Key: gameserver.housing.auction.sales_commission (default 0.1)</summary>
    [Property(key: "gameserver.housing.auction.sales_commission", defaultValue: "0.1")]
    public static float AUCTION_SALES_COMMISION_PERCENT = 0.1f;
    /// <summary>Key: gameserver.housing.auction.grace_end_refund (default 0.5)</summary>
    [Property(key: "gameserver.housing.auction.grace_end_refund", defaultValue: "0.5")]
    public static float AUCTION_GRACE_END_REFUND_PERCENT = 0.5f;

    /// <summary>Key: gameserver.housing.auction.steplimit (default 100)</summary>
    [Property(key: "gameserver.housing.auction.steplimit", defaultValue: "100")]
    public static float AUCTION_BID_STEP_LIMIT = 100f;

    /// <summary>Key: gameserver.housing.auction.auto_fill.time (default "0 0 0 ? * MON"). Initialized from the Java
    /// @Property defaultValue via CronExpressions.GetOrCreate (no invented value).</summary>
    [Property(key: "gameserver.housing.auction.auto_fill.time", defaultValue: "0 0 0 ? * MON")]
    public static CronExpression AUCTION_AUTO_FILL_TIME = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0 ? * MON");
    /// <summary>@Properties keyPattern ^gameserver\.housing\.auction\.auto_fill\.limit\.(.+)</summary>
    [Properties(keyPattern: "^gameserver\\.housing\\.auction\\.auto_fill\\.limit\\.(.+)")]
    public static Dictionary<HouseType, int> AUCTION_AUTO_FILL_LIMITS;
}
