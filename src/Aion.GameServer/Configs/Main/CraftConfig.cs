using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/CraftConfig. SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot.</summary>
public static class CraftConfig
{
    /// <summary>Key: gameserver.craft.skills.delete.excess.enable</summary>
    [Property(key: "gameserver.craft.skills.delete.excess.enable", defaultValue: "false")]
    public static bool DELETE_EXCESS_CRAFT_ENABLE = false;

    /// <summary>Maximum number of expert skills a player can have. Key: gameserver.craft.max.expert.skills</summary>
    [Property(key: "gameserver.craft.max.expert.skills", defaultValue: "2")]
    public static int MAX_EXPERT_CRAFTING_SKILLS = 2;

    /// <summary>Maximum number of master skills a player can have. Key: gameserver.craft.max.master.skills</summary>
    [Property(key: "gameserver.craft.max.master.skills", defaultValue: "1")]
    public static int MAX_MASTER_CRAFTING_SKILLS = 1;

    /// <summary>Enable leveling of aether and essence tapping skills above 499 points. Key: gameserver.craft.disable.tapping.cap</summary>
    [Property(key: "gameserver.craft.disable.tapping.cap", defaultValue: "false")]
    public static bool DISABLE_AETHER_AND_ESSENCE_TAPPING_CAP = false;

    /// <summary>Key: gameserver.craft.fail.chance</summary>
    [Property(key: "gameserver.craft.fail.chance", defaultValue: "33")]
    public static int MAX_CRAFT_FAILURE_CHANCE = 33;

    /// <summary>Key: gameserver.gather.fail.chance</summary>
    [Property(key: "gameserver.gather.fail.chance", defaultValue: "33")]
    public static int MAX_GATHER_FAILURE_CHANCE = 33;
}
