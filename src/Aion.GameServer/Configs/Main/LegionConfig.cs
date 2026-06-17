using System.Text.RegularExpressions;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/LegionConfig (Simple). SCREAMING_SNAKE field names + @Property defaults.
/// Java java.util.regex.Pattern → System.Text.RegularExpressions.Regex.
/// </summary>
public static class LegionConfig
{
    /// <summary>Announcement pattern (checked when announcement is created). Key: gameserver.legion.pattern</summary>
    [Property(key: "gameserver.legion.pattern", defaultValue: "[a-zA-Z ]{2,32}")]
    public static Regex LEGION_NAME_PATTERN = new Regex("[a-zA-Z ]{2,32}");

    /// <summary>Self Intro pattern. Key: gameserver.legion.selfintropattern</summary>
    [Property(key: "gameserver.legion.selfintropattern", defaultValue: ".{1,32}")]
    public static Regex SELF_INTRO_PATTERN = new Regex(".{1,32}");

    /// <summary>Nickname pattern. Key: gameserver.legion.nicknamepattern</summary>
    [Property(key: "gameserver.legion.nicknamepattern", defaultValue: ".{1,10}")]
    public static Regex NICKNAME_PATTERN = new Regex(".{1,10}");

    /// <summary>Sets disband legion time. Key: gameserver.legion.disbandtime</summary>
    [Property(key: "gameserver.legion.disbandtime", defaultValue: "86400")]
    public static int LEGION_DISBAND_TIME = 86400;

    /// <summary>Sets required kinah to create a legion. Key: gameserver.legion.creationrequiredkinah</summary>
    [Property(key: "gameserver.legion.creationrequiredkinah", defaultValue: "10000")]
    public static int LEGION_CREATE_REQUIRED_KINAH = 10000;

    /// <summary>Sets required kinah to create emblem. Key: gameserver.legion.emblemrequiredkinah</summary>
    [Property(key: "gameserver.legion.emblemrequiredkinah", defaultValue: "800000")]
    public static int LEGION_EMBLEM_REQUIRED_KINAH = 800000;

    /// <summary>Key: gameserver.legion.level2requiredkinah</summary>
    [Property(key: "gameserver.legion.level2requiredkinah", defaultValue: "100000")]
    public static int LEGION_LEVEL2_REQUIRED_KINAH = 100000;

    /// <summary>Key: gameserver.legion.level3requiredkinah</summary>
    [Property(key: "gameserver.legion.level3requiredkinah", defaultValue: "1000000")]
    public static int LEGION_LEVEL3_REQUIRED_KINAH = 1000000;

    /// <summary>Key: gameserver.legion.level4requiredkinah</summary>
    [Property(key: "gameserver.legion.level4requiredkinah", defaultValue: "5000000")]
    public static int LEGION_LEVEL4_REQUIRED_KINAH = 5000000;

    /// <summary>Key: gameserver.legion.level5requiredkinah</summary>
    [Property(key: "gameserver.legion.level5requiredkinah", defaultValue: "25000000")]
    public static int LEGION_LEVEL5_REQUIRED_KINAH = 25000000;

    /// <summary>Key: gameserver.legion.level6requiredkinah</summary>
    [Property(key: "gameserver.legion.level6requiredkinah", defaultValue: "50000000")]
    public static int LEGION_LEVEL6_REQUIRED_KINAH = 50000000;

    /// <summary>Key: gameserver.legion.level7requiredkinah</summary>
    [Property(key: "gameserver.legion.level7requiredkinah", defaultValue: "75000000")]
    public static int LEGION_LEVEL7_REQUIRED_KINAH = 75000000;

    /// <summary>Key: gameserver.legion.level8requiredkinah</summary>
    [Property(key: "gameserver.legion.level8requiredkinah", defaultValue: "100000000")]
    public static int LEGION_LEVEL8_REQUIRED_KINAH = 100000000;

    /// <summary>Key: gameserver.legion.level2requiredmembers</summary>
    [Property(key: "gameserver.legion.level2requiredmembers", defaultValue: "10")]
    public static int LEGION_LEVEL2_REQUIRED_MEMBERS = 10;

    /// <summary>Key: gameserver.legion.level3requiredmembers</summary>
    [Property(key: "gameserver.legion.level3requiredmembers", defaultValue: "20")]
    public static int LEGION_LEVEL3_REQUIRED_MEMBERS = 20;

    /// <summary>Key: gameserver.legion.level4requiredmembers</summary>
    [Property(key: "gameserver.legion.level4requiredmembers", defaultValue: "30")]
    public static int LEGION_LEVEL4_REQUIRED_MEMBERS = 30;

    /// <summary>Key: gameserver.legion.level5requiredmembers</summary>
    [Property(key: "gameserver.legion.level5requiredmembers", defaultValue: "40")]
    public static int LEGION_LEVEL5_REQUIRED_MEMBERS = 40;

    /// <summary>Key: gameserver.legion.level6requiredmembers</summary>
    [Property(key: "gameserver.legion.level6requiredmembers", defaultValue: "50")]
    public static int LEGION_LEVEL6_REQUIRED_MEMBERS = 50;

    /// <summary>Key: gameserver.legion.level7requiredmembers</summary>
    [Property(key: "gameserver.legion.level7requiredmembers", defaultValue: "60")]
    public static int LEGION_LEVEL7_REQUIRED_MEMBERS = 60;

    /// <summary>Key: gameserver.legion.level8requiredmembers</summary>
    [Property(key: "gameserver.legion.level8requiredmembers", defaultValue: "70")]
    public static int LEGION_LEVEL8_REQUIRED_MEMBERS = 70;

    /// <summary>Key: gameserver.legion.level2requiredcontribution</summary>
    [Property(key: "gameserver.legion.level2requiredcontribution", defaultValue: "0")]
    public static int LEGION_LEVEL2_REQUIRED_CONTRIBUTION = 0;

    /// <summary>Key: gameserver.legion.level3requiredcontribution</summary>
    [Property(key: "gameserver.legion.level3requiredcontribution", defaultValue: "20000")]
    public static int LEGION_LEVEL3_REQUIRED_CONTRIBUTION = 20000;

    /// <summary>Key: gameserver.legion.level4requiredcontribution</summary>
    [Property(key: "gameserver.legion.level4requiredcontribution", defaultValue: "100000")]
    public static int LEGION_LEVEL4_REQUIRED_CONTRIBUTION = 100000;

    /// <summary>Key: gameserver.legion.level5requiredcontribution</summary>
    [Property(key: "gameserver.legion.level5requiredcontribution", defaultValue: "500000")]
    public static int LEGION_LEVEL5_REQUIRED_CONTRIBUTION = 500000;

    /// <summary>Key: gameserver.legion.level6requiredcontribution</summary>
    [Property(key: "gameserver.legion.level6requiredcontribution", defaultValue: "2500000")]
    public static int LEGION_LEVEL6_REQUIRED_CONTRIBUTION = 2500000;

    /// <summary>Key: gameserver.legion.level7requiredcontribution</summary>
    [Property(key: "gameserver.legion.level7requiredcontribution", defaultValue: "12500000")]
    public static int LEGION_LEVEL7_REQUIRED_CONTRIBUTION = 12500000;

    /// <summary>Key: gameserver.legion.level8requiredcontribution</summary>
    [Property(key: "gameserver.legion.level8requiredcontribution", defaultValue: "62500000")]
    public static int LEGION_LEVEL8_REQUIRED_CONTRIBUTION = 62500000;

    /// <summary>Sets max members of a level 1 legion. Key: gameserver.legion.level1maxmembers</summary>
    [Property(key: "gameserver.legion.level1maxmembers", defaultValue: "30")]
    public static int LEGION_LEVEL1_MAX_MEMBERS = 30;

    /// <summary>Key: gameserver.legion.level2maxmembers</summary>
    [Property(key: "gameserver.legion.level2maxmembers", defaultValue: "60")]
    public static int LEGION_LEVEL2_MAX_MEMBERS = 60;

    /// <summary>Key: gameserver.legion.level3maxmembers</summary>
    [Property(key: "gameserver.legion.level3maxmembers", defaultValue: "90")]
    public static int LEGION_LEVEL3_MAX_MEMBERS = 90;

    /// <summary>Key: gameserver.legion.level4maxmembers</summary>
    [Property(key: "gameserver.legion.level4maxmembers", defaultValue: "120")]
    public static int LEGION_LEVEL4_MAX_MEMBERS = 120;

    /// <summary>Key: gameserver.legion.level5maxmembers</summary>
    [Property(key: "gameserver.legion.level5maxmembers", defaultValue: "150")]
    public static int LEGION_LEVEL5_MAX_MEMBERS = 150;

    /// <summary>Key: gameserver.legion.level6maxmembers</summary>
    [Property(key: "gameserver.legion.level6maxmembers", defaultValue: "180")]
    public static int LEGION_LEVEL6_MAX_MEMBERS = 180;

    /// <summary>Key: gameserver.legion.level7maxmembers</summary>
    [Property(key: "gameserver.legion.level7maxmembers", defaultValue: "210")]
    public static int LEGION_LEVEL7_MAX_MEMBERS = 210;

    /// <summary>Key: gameserver.legion.level8maxmembers</summary>
    [Property(key: "gameserver.legion.level8maxmembers", defaultValue: "240")]
    public static int LEGION_LEVEL8_MAX_MEMBERS = 240;

    /// <summary>Enable/disable Legion Warehouse. Key: gameserver.legion.warehouse</summary>
    [Property(key: "gameserver.legion.warehouse", defaultValue: "true")]
    public static bool LEGION_WAREHOUSE = true;

    /// <summary>Enable/disable Legion Invite Other Faction. Key: gameserver.legion.inviteotherfaction</summary>
    [Property(key: "gameserver.legion.inviteotherfaction", defaultValue: "false")]
    public static bool LEGION_INVITEOTHERFACTION = false;

    /// <summary>Key: gameserver.legion.task.requirement.enable</summary>
    [Property(key: "gameserver.legion.task.requirement.enable", defaultValue: "true")]
    public static bool ENABLE_GUILD_TASK_REQ = true;

    /// <summary>Enable/Disable legion dominion key requirement. Key: gameserver.legion.require_key_for_stonespear_reach</summary>
    [Property(key: "gameserver.legion.require_key_for_stonespear_reach", defaultValue: "true")]
    public static bool REQUIRE_KEY_FOR_STONESPEAR_REACH = true;

    /// <summary>Min points reached in stonespear reach instance to account for a territory election. Key: gameserver.legion.stonespear_reach_min_points</summary>
    [Property(key: "gameserver.legion.stonespear_reach_min_points", defaultValue: "0")]
    public static int STONESPEAR_REACH_MIN_POINTS_FOR_TERRITORY = 0;
}
