using System.Collections.Generic;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/SecurityConfig.
/// Fields keep Java SCREAMING_SNAKE names + @Property default values.
/// </summary>
public static class SecurityConfig
{
    /// <summary>Key: gameserver.security.aion.bin.check</summary>
    public static bool AION_BIN_CHECK = false;

    /// <summary>Key: gameserver.security.antihack.teleportation</summary>
    public static bool TELEPORTATION = false;

    /// <summary>Key: gameserver.security.antihack.speedhack</summary>
    public static bool SPEEDHACK = false;

    /// <summary>Key: gameserver.security.antihack.speedhack.counter</summary>
    public static int SPEEDHACK_COUNTER = 1;

    /// <summary>Key: gameserver.security.antihack.abnormal</summary>
    public static bool ABNORMAL = false;

    /// <summary>Key: gameserver.security.antihack.abnormal.counter</summary>
    public static int ABNORMAL_COUNTER = 1;

    /// <summary>Key: gameserver.security.antihack.punish</summary>
    public static int PUNISH = 0;

    /// <summary>Check for no-animation hacks (prevents premature skill executions and logs suspicious players). Key: gameserver.security.check_animations</summary>
    public static bool CHECK_ANIMATIONS = true;

    /// <summary>Key: gameserver.security.captcha.enable</summary>
    public static bool CAPTCHA_ENABLE = false;

    /// <summary>Key: gameserver.security.captcha.appear</summary>
    public static string CAPTCHA_APPEAR = "OD";

    /// <summary>Key: gameserver.security.captcha.appear.rate</summary>
    public static int CAPTCHA_APPEAR_RATE = 5;

    /// <summary>Key: gameserver.security.captcha.extraction.ban.time</summary>
    public static int CAPTCHA_EXTRACTION_BAN_TIME = 3000;

    /// <summary>Key: gameserver.security.captcha.extraction.ban.add.time</summary>
    public static int CAPTCHA_EXTRACTION_BAN_ADD_TIME = 600;

    /// <summary>Key: gameserver.security.captcha.bonus.fp.time</summary>
    public static int CAPTCHA_BONUS_FP_TIME = 5;

    /// <summary>Key: gameserver.security.passkey.enable</summary>
    public static bool PASSKEY_ENABLE = false;

    /// <summary>Key: gameserver.security.passkey.wrong.maxcount</summary>
    public static int PASSKEY_WRONG_MAXCOUNT = 5;

    /// <summary>Key: gameserver.security.pingcheck.kick</summary>
    public static bool PINGCHECK_KICK = true;

    /// <summary>Key: gameserver.security.flood.delay</summary>
    public static int FLOOD_DELAY = 1;

    /// <summary>Key: gameserver.security.flood.msg</summary>
    public static int FLOOD_MSG = 6;

    /// <summary>Key: gameserver.security.validation.flypath</summary>
    public static bool ENABLE_FLYPATH_VALIDATOR = false;

    /// <summary>Key: gameserver.security.survey.delay.minute</summary>
    public static int SURVEY_DELAY = 20;

    /// <summary>
    /// Restriction mode for multi-clienting: NONE (multiple accounts/computer), FULL (one account/computer),
    /// SAME_FACTION (multiple accounts but same faction only). Key: gameserver.security.multi_clienting.restriction_mode
    /// </summary>
    public static MultiClientingRestrictionMode MULTI_CLIENTING_RESTRICTION_MODE = MultiClientingRestrictionMode.NONE;

    /// <summary>Comma separated MAC addresses allowed regardless of restrictions. Key: gameserver.security.multi_clienting.ignored_mac_addresses</summary>
    public static ISet<string> MULTI_CLIENTING_IGNORED_MAC_ADDRESSES = new HashSet<string>();

    /// <summary>Key: gameserver.security.multi_clienting.faction_switch_cooldown_minutes</summary>
    public static int MULTI_CLIENTING_FACTION_SWITCH_COOLDOWN_MINUTES = 20;

    /// <summary>Key: gameserver.security.hdd_serial_lock.enable</summary>
    public static bool HDD_SERIAL_LOCK_ENABLE = false;

    /// <summary>Key: gameserver.security.hdd_serial_lock.auto_lock</summary>
    public static bool HDD_SERIAL_LOCK_UNLOCKED_ACCOUNTS = false;

    public enum MultiClientingRestrictionMode
    {
        NONE, FULL, SAME_FACTION
    }
}
