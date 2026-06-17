using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/PlayerTransferConfig (KID). [Property] keys/defaults bound at boot.</summary>
public static class PlayerTransferConfig
{
    /// <summary>Property key: ptransfer.max.kinah</summary>
    [Property(key: "ptransfer.max.kinah", defaultValue: "0")]
    public static long MAX_KINAH = 0;

    /// <summary>Property key: ptransfer.bindpoint.elyos</summary>
    [Property(key: "ptransfer.bindpoint.elyos", defaultValue: "210010000 1212.9423 1044.8516 140.75568 32")]
    public static string BIND_ELYOS = "210010000 1212.9423 1044.8516 140.75568 32";

    /// <summary>Property key: ptransfer.bindpoint.asmo</summary>
    [Property(key: "ptransfer.bindpoint.asmo", defaultValue: "220010000 571.0388 2787.3420 299.8750 32")]
    public static string BIND_ASMO = "220010000 571.0388 2787.3420 299.8750 32";

    /// <summary>Property key: ptransfer.allow.emotions</summary>
    [Property(key: "ptransfer.allow.emotions", defaultValue: "true")]
    public static bool ALLOW_EMOTIONS = true;

    /// <summary>Property key: ptransfer.allow.motions</summary>
    [Property(key: "ptransfer.allow.motions", defaultValue: "true")]
    public static bool ALLOW_MOTIONS = true;

    /// <summary>Property key: ptransfer.allow.macro</summary>
    [Property(key: "ptransfer.allow.macro", defaultValue: "true")]
    public static bool ALLOW_MACRO = true;

    /// <summary>Property key: ptransfer.allow.npcfactions</summary>
    [Property(key: "ptransfer.allow.npcfactions", defaultValue: "true")]
    public static bool ALLOW_NPCFACTIONS = true;

    /// <summary>Property key: ptransfer.allow.pets</summary>
    [Property(key: "ptransfer.allow.pets", defaultValue: "true")]
    public static bool ALLOW_PETS = true;

    /// <summary>Property key: ptransfer.allow.recipes</summary>
    [Property(key: "ptransfer.allow.recipes", defaultValue: "true")]
    public static bool ALLOW_RECIPES = true;

    /// <summary>Property key: ptransfer.allow.skills</summary>
    [Property(key: "ptransfer.allow.skills", defaultValue: "true")]
    public static bool ALLOW_SKILLS = true;

    /// <summary>Property key: ptransfer.allow.titles</summary>
    [Property(key: "ptransfer.allow.titles", defaultValue: "true")]
    public static bool ALLOW_TITLES = true;

    /// <summary>Property key: ptransfer.allow.quests</summary>
    [Property(key: "ptransfer.allow.quests", defaultValue: "true")]
    public static bool ALLOW_QUESTS = true;

    /// <summary>Property key: ptransfer.allow.inventory</summary>
    [Property(key: "ptransfer.allow.inventory", defaultValue: "true")]
    public static bool ALLOW_INV = true;

    /// <summary>Property key: ptransfer.allow.warehouse</summary>
    [Property(key: "ptransfer.allow.warehouse", defaultValue: "true")]
    public static bool ALLOW_WAREHOUSE = true;

    /// <summary>Property key: ptransfer.allow.stigma</summary>
    [Property(key: "ptransfer.allow.stigma", defaultValue: "true")]
    public static bool ALLOW_STIGMA = true;

    /// <summary>Property key: ptransfer.block.samename</summary>
    [Property(key: "ptransfer.block.samename", defaultValue: "false")]
    public static bool BLOCK_SAMENAME = false;

    /// <summary>Property key: ptransfer.retransfer.hours</summary>
    [Property(key: "ptransfer.retransfer.hours", defaultValue: "0")]
    public static int REUSE_HOURS = 0;

    /// <summary>Property key: ptransfer.remove.skills.list</summary>
    [Property(key: "ptransfer.remove.skills.list", defaultValue: "*")]
    public static string REMOVE_SKILL_LIST = "*";
}
