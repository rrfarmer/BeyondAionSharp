namespace Aion.GameServer.Model;

/// <summary>
/// Chat types supported by Aion (client id + whether it's a system message).
/// Java parity: model/ChatType.
/// </summary>
public enum ChatType
{
    NORMAL = 0,        // [MT_SAY] Normal chat (White)
    NPC = 1,           // [MT_THINK] Npc chat (Light Blue)
    SHOUT = 3,         // [MT_SHOUT] Shout chat (Orange)
    WHISPER = 4,       // [MT_WHISPER] Whisper chat (Green)
    GROUP = 5,         // [MT_PARTY] Group chat (Blue)
    ALLIANCE = 6,      // [MT_ALLIANCE] Alliance chat (Aqua)
    GROUP_LEADER = 7,  // [MT_ALERT] Group Leader chat (Orange)
    LEAGUE = 8,        // [MT_UNION] League chat (Dark Blue)
    LEAGUE_ALERT = 9,  // [MT_UNIONALERT] League chat (Orange)
    LEGION = 10,       // [MT_GUILD] Legion chat (Green)
    CH1 = 14,
    CH2 = 15,
    CH3 = 16,
    CH4 = 17,
    CH5 = 18,
    CH6 = 19,
    CH7 = 20,
    CH8 = 21,
    CH9 = 22,
    CH10 = 23,

    COMMAND = 24,      // [MT_RANKER_CHAT] Command chat (Yellow)

    // Global chat types
    GOLDEN_YELLOW = 25,        // [MT_SYSMSG_HIGH_PRI] most common system message
    GM_CHAT = 27,              // [MT_SYSMSG_PETITION] petition/support window
    WHITE = 31,                // [MT_GMMSG_NORMAL_LEVEL_1]
    YELLOW = 32,               // [MT_GMMSG_NORMAL_LEVEL_2]
    BRIGHT_YELLOW = 33,        // [MT_GMMSG_NORMAL_LEVEL_3]
    WHITE_CENTER = 34,         // [MT_GMMSG_HIGH_LEVEL_1] periodic notice (center box)
    YELLOW_CENTER = 35,        // [MT_GMMSG_HIGH_LEVEL_2] periodic announcement (center box)
    BRIGHT_YELLOW_CENTER = 36, // [MT_GMMSG_HIGH_LEVEL_3] system notice (center box)
}

public static class ChatTypeExtensions
{
    // Java parity: per-constant sysMsg flag (true = all races can read).
    private static readonly HashSet<ChatType> SysMsgTypes = new()
    {
        ChatType.GOLDEN_YELLOW, ChatType.WHITE, ChatType.YELLOW, ChatType.BRIGHT_YELLOW,
        ChatType.WHITE_CENTER, ChatType.YELLOW_CENTER, ChatType.BRIGHT_YELLOW_CENTER,
    };

    private static readonly Dictionary<byte, ChatType> ById = BuildById();

    private static Dictionary<byte, ChatType> BuildById()
    {
        var map = new Dictionary<byte, ChatType>();
        foreach (ChatType ct in Enum.GetValues<ChatType>())
            map[ct.GetId()] = ct;
        return map;
    }

    // Java parity: getId()
    public static byte GetId(this ChatType chatType) => (byte)chatType;

    // Java parity: isSysMsg()
    public static bool IsSysMsg(this ChatType chatType) => SysMsgTypes.Contains(chatType);

    // Java parity: static getChatType(byte) — throws if unsupported.
    public static ChatType GetChatType(byte id)
    {
        if (!ById.TryGetValue(id, out var ct))
            throw new ArgumentException("Unsupported chat type: " + (id & 0xFF));
        return ct;
    }
}
