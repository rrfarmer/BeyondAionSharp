using System.Collections.Generic;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/SecurityConfig.
/// Fields keep Java SCREAMING_SNAKE names + @Property keys/default values, bound by the config framework
/// (the MAC-address Set field uses the Collection transformer; restriction mode uses the Enum transformer).
/// </summary>
public static class SecurityConfig
{
    /// <summary>Key: gameserver.security.aion.bin.check</summary>
    [Property(key: "gameserver.security.aion.bin.check", defaultValue: "false")]
    public static bool AION_BIN_CHECK = false;

    /// <summary>Key: gameserver.security.antihack.teleportation</summary>
    [Property(key: "gameserver.security.antihack.teleportation", defaultValue: "false")]
    public static bool TELEPORTATION = false;

    /// <summary>Key: gameserver.security.antihack.speedhack</summary>
    [Property(key: "gameserver.security.antihack.speedhack", defaultValue: "false")]
    public static bool SPEEDHACK = false;

    /// <summary>Key: gameserver.security.antihack.speedhack.counter</summary>
    [Property(key: "gameserver.security.antihack.speedhack.counter", defaultValue: "1")]
    public static int SPEEDHACK_COUNTER = 1;

    /// <summary>Key: gameserver.security.antihack.abnormal</summary>
    [Property(key: "gameserver.security.antihack.abnormal", defaultValue: "false")]
    public static bool ABNORMAL = false;

    /// <summary>Key: gameserver.security.antihack.abnormal.counter</summary>
    [Property(key: "gameserver.security.antihack.abnormal.counter", defaultValue: "1")]
    public static int ABNORMAL_COUNTER = 1;

    /// <summary>Key: gameserver.security.antihack.punish</summary>
    [Property(key: "gameserver.security.antihack.punish", defaultValue: "0")]
    public static int PUNISH = 0;

    /// <summary>Check for no-animation hacks (prevents premature skill executions and logs suspicious players). Key: gameserver.security.check_animations</summary>
    [Property(key: "gameserver.security.check_animations", defaultValue: "true")]
    public static bool CHECK_ANIMATIONS = true;

    /// <summary>Key: gameserver.security.captcha.enable</summary>
    [Property(key: "gameserver.security.captcha.enable", defaultValue: "false")]
    public static bool CAPTCHA_ENABLE = false;

    /// <summary>Key: gameserver.security.captcha.appear</summary>
    [Property(key: "gameserver.security.captcha.appear", defaultValue: "OD")]
    public static string CAPTCHA_APPEAR = "OD";

    /// <summary>Key: gameserver.security.captcha.appear.rate</summary>
    [Property(key: "gameserver.security.captcha.appear.rate", defaultValue: "5")]
    public static int CAPTCHA_APPEAR_RATE = 5;

    /// <summary>Key: gameserver.security.captcha.extraction.ban.time</summary>
    [Property(key: "gameserver.security.captcha.extraction.ban.time", defaultValue: "3000")]
    public static int CAPTCHA_EXTRACTION_BAN_TIME = 3000;

    /// <summary>Key: gameserver.security.captcha.extraction.ban.add.time</summary>
    [Property(key: "gameserver.security.captcha.extraction.ban.add.time", defaultValue: "600")]
    public static int CAPTCHA_EXTRACTION_BAN_ADD_TIME = 600;

    /// <summary>Key: gameserver.security.captcha.bonus.fp.time</summary>
    [Property(key: "gameserver.security.captcha.bonus.fp.time", defaultValue: "5")]
    public static int CAPTCHA_BONUS_FP_TIME = 5;

    /// <summary>Key: gameserver.security.passkey.enable</summary>
    [Property(key: "gameserver.security.passkey.enable", defaultValue: "false")]
    public static bool PASSKEY_ENABLE = false;

    /// <summary>Key: gameserver.security.passkey.wrong.maxcount</summary>
    [Property(key: "gameserver.security.passkey.wrong.maxcount", defaultValue: "5")]
    public static int PASSKEY_WRONG_MAXCOUNT = 5;

    /// <summary>Key: gameserver.security.pingcheck.kick</summary>
    [Property(key: "gameserver.security.pingcheck.kick", defaultValue: "true")]
    public static bool PINGCHECK_KICK = true;

    /// <summary>Key: gameserver.security.flood.delay</summary>
    [Property(key: "gameserver.security.flood.delay", defaultValue: "1")]
    public static int FLOOD_DELAY = 1;

    /// <summary>Key: gameserver.security.flood.msg</summary>
    [Property(key: "gameserver.security.flood.msg", defaultValue: "6")]
    public static int FLOOD_MSG = 6;

    /// <summary>Key: gameserver.security.validation.flypath</summary>
    [Property(key: "gameserver.security.validation.flypath", defaultValue: "false")]
    public static bool ENABLE_FLYPATH_VALIDATOR = false;

    /// <summary>Key: gameserver.security.survey.delay.minute</summary>
    [Property(key: "gameserver.security.survey.delay.minute", defaultValue: "20")]
    public static int SURVEY_DELAY = 20;

    /// <summary>
    /// Restriction mode for multi-clienting: NONE (multiple accounts/computer), FULL (one account/computer),
    /// SAME_FACTION (multiple accounts but same faction only). Key: gameserver.security.multi_clienting.restriction_mode
    /// </summary>
    [Property(key: "gameserver.security.multi_clienting.restriction_mode", defaultValue: "NONE")]
    public static MultiClientingRestrictionMode MULTI_CLIENTING_RESTRICTION_MODE = MultiClientingRestrictionMode.NONE;

    /// <summary>Comma separated MAC addresses allowed regardless of restrictions. Key: gameserver.security.multi_clienting.ignored_mac_addresses</summary>
    [Property(key: "gameserver.security.multi_clienting.ignored_mac_addresses", defaultValue: "")]
    public static ISet<string> MULTI_CLIENTING_IGNORED_MAC_ADDRESSES = new HashSet<string>();

    /// <summary>Key: gameserver.security.multi_clienting.faction_switch_cooldown_minutes</summary>
    [Property(key: "gameserver.security.multi_clienting.faction_switch_cooldown_minutes", defaultValue: "20")]
    public static int MULTI_CLIENTING_FACTION_SWITCH_COOLDOWN_MINUTES = 20;

    /// <summary>Key: gameserver.security.hdd_serial_lock.enable</summary>
    [Property(key: "gameserver.security.hdd_serial_lock.enable", defaultValue: "false")]
    public static bool HDD_SERIAL_LOCK_ENABLE = false;

    /// <summary>Key: gameserver.security.hdd_serial_lock.auto_lock</summary>
    [Property(key: "gameserver.security.hdd_serial_lock.auto_lock", defaultValue: "false")]
    public static bool HDD_SERIAL_LOCK_UNLOCKED_ACCOUNTS = false;

    public enum MultiClientingRestrictionMode
    {
        NONE, FULL, SAME_FACTION
    }
}
