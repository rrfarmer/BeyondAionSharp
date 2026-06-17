using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/MembershipConfig. SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot.</summary>
public static class MembershipConfig
{
    /// <summary>Key: gameserver.membership.types (default "Premium"). Java String[] → ArrayTransformer.
    /// PlayerEnterWorldService dereferences MEMBERSHIP_TYPES.Length and MEMBERSHIP_TYPES[membership-1] on enter-world
    /// for any membership>0 account; the @Property default "Premium" yields String[]{"Premium"} matching Java.</summary>
    [Property(key: "gameserver.membership.types", defaultValue: "Premium")]
    public static string[] MEMBERSHIP_TYPES = { "Premium" };

    /// <summary>Key: gameserver.membership.gathering.allow_on_mount</summary>
    [Property(key: "gameserver.membership.gathering.allow_on_mount", defaultValue: "10")]
    public static byte GATHERING_ALLOW_ON_MOUNT = 10;

    /// <summary>Key: gameserver.instances.title.requirement</summary>
    [Property(key: "gameserver.instances.title.requirement", defaultValue: "10")]
    public static byte INSTANCES_TITLE_REQ = 10;

    /// <summary>Key: gameserver.instances.race.requirement</summary>
    [Property(key: "gameserver.instances.race.requirement", defaultValue: "10")]
    public static byte INSTANCES_RACE_REQ = 10;

    /// <summary>Key: gameserver.instances.level.requirement</summary>
    [Property(key: "gameserver.instances.level.requirement", defaultValue: "10")]
    public static byte INSTANCES_LEVEL_REQ = 10;

    /// <summary>Key: gameserver.instances.group.requirement</summary>
    [Property(key: "gameserver.instances.group.requirement", defaultValue: "10")]
    public static byte INSTANCES_GROUP_REQ = 10;

    /// <summary>Key: gameserver.instances.quest.requirement</summary>
    [Property(key: "gameserver.instances.quest.requirement", defaultValue: "10")]
    public static byte INSTANCES_QUEST_REQ = 10;

    /// <summary>Key: gameserver.instances.cooldown</summary>
    [Property(key: "gameserver.instances.cooldown", defaultValue: "10")]
    public static byte INSTANCES_COOLDOWN = 10;

    /// <summary>Key: gameserver.emotions.all</summary>
    [Property(key: "gameserver.emotions.all", defaultValue: "10")]
    public static byte EMOTIONS_ALL = 10;

    /// <summary>Key: gameserver.quest.stigma.slot</summary>
    [Property(key: "gameserver.quest.stigma.slot", defaultValue: "10")]
    public static byte STIGMA_SLOT_QUEST = 10;

    /// <summary>Key: gameserver.soulsickness.disable</summary>
    [Property(key: "gameserver.soulsickness.disable", defaultValue: "10")]
    public static byte DISABLE_SOULSICKNESS = 10;

    /// <summary>Key: gameserver.autolearn.stigma</summary>
    [Property(key: "gameserver.autolearn.stigma", defaultValue: "10")]
    public static byte STIGMA_AUTOLEARN = 10;

    /// <summary>Key: gameserver.quest.limit.disable</summary>
    [Property(key: "gameserver.quest.limit.disable", defaultValue: "10")]
    public static byte QUEST_LIMIT_DISABLED = 10;

    /// <summary>Key: gameserver.character.additional.enable</summary>
    [Property(key: "gameserver.character.additional.enable", defaultValue: "10")]
    public static byte CHARACTER_ADDITIONAL_ENABLE = 10;

    /// <summary>Key: gameserver.character.additional.count</summary>
    [Property(key: "gameserver.character.additional.count", defaultValue: "8")]
    public static byte CHARACTER_ADDITIONAL_COUNT = 8;
}
