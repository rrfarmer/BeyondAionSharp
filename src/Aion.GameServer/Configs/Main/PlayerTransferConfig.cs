namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/PlayerTransferConfig.</summary>
public static class PlayerTransferConfig
{
    /// <summary>Property key: ptransfer.max.kinah</summary>
    public static long MAX_KINAH = 0;

    /// <summary>Property key: ptransfer.bindpoint.elyos</summary>
    public static string BIND_ELYOS = "210010000 1212.9423 1044.8516 140.75568 32";

    /// <summary>Property key: ptransfer.bindpoint.asmo</summary>
    public static string BIND_ASMO = "220010000 571.0388 2787.3420 299.8750 32";

    /// <summary>Property key: ptransfer.allow.emotions</summary>
    public static bool ALLOW_EMOTIONS = true;

    /// <summary>Property key: ptransfer.allow.motions</summary>
    public static bool ALLOW_MOTIONS = true;

    /// <summary>Property key: ptransfer.allow.macro</summary>
    public static bool ALLOW_MACRO = true;

    /// <summary>Property key: ptransfer.allow.npcfactions</summary>
    public static bool ALLOW_NPCFACTIONS = true;

    /// <summary>Property key: ptransfer.allow.pets</summary>
    public static bool ALLOW_PETS = true;

    /// <summary>Property key: ptransfer.allow.recipes</summary>
    public static bool ALLOW_RECIPES = true;

    /// <summary>Property key: ptransfer.allow.skills</summary>
    public static bool ALLOW_SKILLS = true;

    /// <summary>Property key: ptransfer.allow.titles</summary>
    public static bool ALLOW_TITLES = true;

    /// <summary>Property key: ptransfer.allow.quests</summary>
    public static bool ALLOW_QUESTS = true;

    /// <summary>Property key: ptransfer.allow.inventory</summary>
    public static bool ALLOW_INV = true;

    /// <summary>Property key: ptransfer.allow.warehouse</summary>
    public static bool ALLOW_WAREHOUSE = true;

    /// <summary>Property key: ptransfer.allow.stigma</summary>
    public static bool ALLOW_STIGMA = true;

    /// <summary>Property key: ptransfer.block.samename</summary>
    public static bool BLOCK_SAMENAME = false;

    /// <summary>Property key: ptransfer.retransfer.hours</summary>
    public static int REUSE_HOURS = 0;

    /// <summary>Property key: ptransfer.remove.skills.list</summary>
    public static string REMOVE_SKILL_LIST = "*";
}
