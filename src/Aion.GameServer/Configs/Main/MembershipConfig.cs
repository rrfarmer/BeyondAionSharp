namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/MembershipConfig. SCREAMING_SNAKE field names + @Property defaults.</summary>
public static class MembershipConfig
{
    /// <summary>Key: gameserver.membership.types (config value "Premium"). Initialized from the shipped
    /// config/main/membership.properties value as a field initializer (Java @Property has no defaultValue; the
    /// value is loaded from the properties file -> String[]{"Premium"}). PlayerEnterWorldService dereferences
    /// MEMBERSHIP_TYPES.Length and MEMBERSHIP_TYPES[membership-1] on enter-world for any membership>0 account, so
    /// a null here NREs the live enter-world path; matches the Java non-null runtime array.</summary>
    public static string[] MEMBERSHIP_TYPES = { "Premium" };

    /// <summary>Key: gameserver.membership.gathering.allow_on_mount</summary>
    public static byte GATHERING_ALLOW_ON_MOUNT = 10;

    /// <summary>Key: gameserver.instances.title.requirement</summary>
    public static byte INSTANCES_TITLE_REQ = 10;

    /// <summary>Key: gameserver.instances.race.requirement</summary>
    public static byte INSTANCES_RACE_REQ = 10;

    /// <summary>Key: gameserver.instances.level.requirement</summary>
    public static byte INSTANCES_LEVEL_REQ = 10;

    /// <summary>Key: gameserver.instances.group.requirement</summary>
    public static byte INSTANCES_GROUP_REQ = 10;

    /// <summary>Key: gameserver.instances.quest.requirement</summary>
    public static byte INSTANCES_QUEST_REQ = 10;

    /// <summary>Key: gameserver.instances.cooldown</summary>
    public static byte INSTANCES_COOLDOWN = 10;

    /// <summary>Key: gameserver.emotions.all</summary>
    public static byte EMOTIONS_ALL = 10;

    /// <summary>Key: gameserver.quest.stigma.slot</summary>
    public static byte STIGMA_SLOT_QUEST = 10;

    /// <summary>Key: gameserver.soulsickness.disable</summary>
    public static byte DISABLE_SOULSICKNESS = 10;

    /// <summary>Key: gameserver.autolearn.stigma</summary>
    public static byte STIGMA_AUTOLEARN = 10;

    /// <summary>Key: gameserver.quest.limit.disable</summary>
    public static byte QUEST_LIMIT_DISABLED = 10;

    /// <summary>Key: gameserver.character.additional.enable</summary>
    public static byte CHARACTER_ADDITIONAL_ENABLE = 10;

    /// <summary>Key: gameserver.character.additional.count</summary>
    public static byte CHARACTER_ADDITIONAL_COUNT = 8;
}
