using System.Text.RegularExpressions;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/NameConfig (nrg). SCREAMING_SNAKE field names + @Property defaults.
/// Java java.util.regex.Pattern → System.Text.RegularExpressions.Regex (raw default strings kept).
/// </summary>
public static class NameConfig
{
    /// <summary>Enables custom names usage. Key: gameserver.name.allow.custom</summary>
    public static bool ALLOW_CUSTOM_NAMES = false;

    /// <summary>Character name pattern (checked when character is being created or renamed). Key: gameserver.name.character_pattern</summary>
    public static Regex CHAR_NAME_PATTERN = new Regex("[a-zA-Z]{2,16}");

    /// <summary>Pet name pattern (checked when pet is being adopted or renamed). Key: gameserver.name.pet_pattern</summary>
    public static Regex PET_NAME_PATTERN = new Regex("[a-zA-Z]{2,16}");

    /// <summary>Forbidden word sequences (Regex pattern). Filters names. Key: gameserver.name.forbidden_sequences_pattern</summary>
    public static Regex FORBIDDEN_SEQUENCE_PATTERN = null;

    /// <summary>Forbidden words. Filters names &amp; chat. Key: gameserver.name.forbidden_words</summary>
    public static string[] FORBIDDEN_WORDS = null;

    /// <summary>Number of days a name is reserved after renaming. Key: gameserver.name.reserve_old_name_days</summary>
    public static int RESERVE_OLD_NAME_DAYS = 30;
}
