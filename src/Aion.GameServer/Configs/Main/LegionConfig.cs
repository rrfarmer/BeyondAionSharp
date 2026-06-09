using System.Text.RegularExpressions;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/LegionConfig (Simple). SCREAMING_SNAKE field names + @Property defaults.
/// Java java.util.regex.Pattern → System.Text.RegularExpressions.Regex.
/// </summary>
public static class LegionConfig
{
    /// <summary>Announcement pattern (checked when announcement is created). Key: gameserver.legion.pattern</summary>
    public static Regex LEGION_NAME_PATTERN = new Regex("[a-zA-Z ]{2,32}");

    /// <summary>Self Intro pattern. Key: gameserver.legion.selfintropattern</summary>
    public static Regex SELF_INTRO_PATTERN = new Regex(".{1,32}");

    /// <summary>Nickname pattern. Key: gameserver.legion.nicknamepattern</summary>
    public static Regex NICKNAME_PATTERN = new Regex(".{1,10}");

    /// <summary>Sets disband legion time. Key: gameserver.legion.disbandtime</summary>
    public static int LEGION_DISBAND_TIME = 86400;

    /// <summary>Sets required kinah to create a legion. Key: gameserver.legion.creationrequiredkinah</summary>
    public static int LEGION_CREATE_REQUIRED_KINAH = 10000;

    /// <summary>Sets required kinah to create emblem. Key: gameserver.legion.emblemrequiredkinah</summary>
    public static int LEGION_EMBLEM_REQUIRED_KINAH = 800000;

    /// <summary>Key: gameserver.legion.level2requiredkinah</summary>
    public static int LEGION_LEVEL2_REQUIRED_KINAH = 100000;

    /// <summary>Key: gameserver.legion.level3requiredkinah</summary>
    public static int LEGION_LEVEL3_REQUIRED_KINAH = 1000000;

    /// <summary>Key: gameserver.legion.level4requiredkinah</summary>
    public static int LEGION_LEVEL4_REQUIRED_KINAH = 5000000;

    /// <summary>Key: gameserver.legion.level5requiredkinah</summary>
    public static int LEGION_LEVEL5_REQUIRED_KINAH = 25000000;

    /// <summary>Key: gameserver.legion.level6requiredkinah</summary>
    public static int LEGION_LEVEL6_REQUIRED_KINAH = 50000000;

    /// <summary>Key: gameserver.legion.level7requiredkinah</summary>
    public static int LEGION_LEVEL7_REQUIRED_KINAH = 75000000;

    /// <summary>Key: gameserver.legion.level8requiredkinah</summary>
    public static int LEGION_LEVEL8_REQUIRED_KINAH = 100000000;

    /// <summary>Key: gameserver.legion.level2requiredmembers</summary>
    public static int LEGION_LEVEL2_REQUIRED_MEMBERS = 10;

    /// <summary>Key: gameserver.legion.level3requiredmembers</summary>
    public static int LEGION_LEVEL3_REQUIRED_MEMBERS = 20;

    /// <summary>Key: gameserver.legion.level4requiredmembers</summary>
    public static int LEGION_LEVEL4_REQUIRED_MEMBERS = 30;

    /// <summary>Key: gameserver.legion.level5requiredmembers</summary>
    public static int LEGION_LEVEL5_REQUIRED_MEMBERS = 40;

    /// <summary>Key: gameserver.legion.level6requiredmembers</summary>
    public static int LEGION_LEVEL6_REQUIRED_MEMBERS = 50;

    /// <summary>Key: gameserver.legion.level7requiredmembers</summary>
    public static int LEGION_LEVEL7_REQUIRED_MEMBERS = 60;

    /// <summary>Key: gameserver.legion.level8requiredmembers</summary>
    public static int LEGION_LEVEL8_REQUIRED_MEMBERS = 70;

    /// <summary>Key: gameserver.legion.level2requiredcontribution</summary>
    public static int LEGION_LEVEL2_REQUIRED_CONTRIBUTION = 0;

    /// <summary>Key: gameserver.legion.level3requiredcontribution</summary>
    public static int LEGION_LEVEL3_REQUIRED_CONTRIBUTION = 20000;

    /// <summary>Key: gameserver.legion.level4requiredcontribution</summary>
    public static int LEGION_LEVEL4_REQUIRED_CONTRIBUTION = 100000;

    /// <summary>Key: gameserver.legion.level5requiredcontribution</summary>
    public static int LEGION_LEVEL5_REQUIRED_CONTRIBUTION = 500000;

    /// <summary>Key: gameserver.legion.level6requiredcontribution</summary>
    public static int LEGION_LEVEL6_REQUIRED_CONTRIBUTION = 2500000;

    /// <summary>Key: gameserver.legion.level7requiredcontribution</summary>
    public static int LEGION_LEVEL7_REQUIRED_CONTRIBUTION = 12500000;

    /// <summary>Key: gameserver.legion.level8requiredcontribution</summary>
    public static int LEGION_LEVEL8_REQUIRED_CONTRIBUTION = 62500000;

    /// <summary>Sets max members of a level 1 legion. Key: gameserver.legion.level1maxmembers</summary>
    public static int LEGION_LEVEL1_MAX_MEMBERS = 30;

    /// <summary>Key: gameserver.legion.level2maxmembers</summary>
    public static int LEGION_LEVEL2_MAX_MEMBERS = 60;

    /// <summary>Key: gameserver.legion.level3maxmembers</summary>
    public static int LEGION_LEVEL3_MAX_MEMBERS = 90;

    /// <summary>Key: gameserver.legion.level4maxmembers</summary>
    public static int LEGION_LEVEL4_MAX_MEMBERS = 120;

    /// <summary>Key: gameserver.legion.level5maxmembers</summary>
    public static int LEGION_LEVEL5_MAX_MEMBERS = 150;

    /// <summary>Key: gameserver.legion.level6maxmembers</summary>
    public static int LEGION_LEVEL6_MAX_MEMBERS = 180;

    /// <summary>Key: gameserver.legion.level7maxmembers</summary>
    public static int LEGION_LEVEL7_MAX_MEMBERS = 210;

    /// <summary>Key: gameserver.legion.level8maxmembers</summary>
    public static int LEGION_LEVEL8_MAX_MEMBERS = 240;

    /// <summary>Enable/disable Legion Warehouse. Key: gameserver.legion.warehouse</summary>
    public static bool LEGION_WAREHOUSE = true;

    /// <summary>Enable/disable Legion Invite Other Faction. Key: gameserver.legion.inviteotherfaction</summary>
    public static bool LEGION_INVITEOTHERFACTION = false;

    /// <summary>Key: gameserver.legion.task.requirement.enable</summary>
    public static bool ENABLE_GUILD_TASK_REQ = true;

    /// <summary>Enable/Disable legion dominion key requirement. Key: gameserver.legion.require_key_for_stonespear_reach</summary>
    public static bool REQUIRE_KEY_FOR_STONESPEAR_REACH = true;

    /// <summary>Min points reached in stonespear reach instance to account for a territory election. Key: gameserver.legion.stonespear_reach_min_points</summary>
    public static int STONESPEAR_REACH_MIN_POINTS_FOR_TERRITORY = 0;
}
