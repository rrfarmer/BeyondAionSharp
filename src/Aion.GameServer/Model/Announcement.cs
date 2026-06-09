using System;

namespace Aion.GameServer.Model;

/// <summary>
/// This class represents an announcement.
/// Java parity: model/Announcement (Divinity).
/// </summary>
public class Announcement
{
    private readonly int id;
    private readonly Race? faction;
    private readonly string announce;
    private readonly string chatType;
    private readonly int delay;

    public Announcement(int id, string announce, string faction, string chatType, int delay)
    {
        this.id = id;
        this.announce = announce;
        this.faction = GetFactionEnum(faction);
        this.chatType = chatType;
        this.delay = delay;
    }

    private Race? GetFactionEnum(string faction)
    {
        if (string.Equals(faction, "ELYOS", StringComparison.OrdinalIgnoreCase))
            return Race.ELYOS;
        else if (string.Equals(faction, "ASMODIANS", StringComparison.OrdinalIgnoreCase))
            return Race.ASMODIANS;
        return null;
    }

    /// <summary>Return the id of the announcement.</summary>
    public int GetId()
    {
        return id;
    }

    /// <summary>Return the announcement's text.</summary>
    public string GetAnnounce()
    {
        return announce;
    }

    /// <summary>Return the announcement's faction (ELYOS or ASMODIANS, null if unrestricted).</summary>
    public Race? GetFaction()
    {
        return faction;
    }

    /// <summary>Return the chatType in String mode (for the insert in database). Java parity: getType().</summary>
    public string GetType_()
    {
        return chatType;
    }

    /// <summary>Return the chatType with the ChatType Enum.</summary>
    public ChatType GetChatType()
    {
        if (string.Equals(chatType, "System", StringComparison.OrdinalIgnoreCase))
            return ChatType.GOLDEN_YELLOW;
        else if (string.Equals(chatType, "White", StringComparison.OrdinalIgnoreCase))
            return ChatType.WHITE_CENTER;
        else if (string.Equals(chatType, "Yellow", StringComparison.OrdinalIgnoreCase))
            return ChatType.YELLOW_CENTER;
        else if (string.Equals(chatType, "Shout", StringComparison.OrdinalIgnoreCase))
            return ChatType.SHOUT;
        else if (string.Equals(chatType, "Orange", StringComparison.OrdinalIgnoreCase))
            return ChatType.GROUP_LEADER;
        else
            return ChatType.BRIGHT_YELLOW_CENTER;
    }

    /// <summary>Return the announcement's delay.</summary>
    public int GetDelay()
    {
        return delay;
    }
}
