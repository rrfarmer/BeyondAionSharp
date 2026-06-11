using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FRIEND_RESPONSE (Ben, Neon). Replies to add/delete-friend requests. Writes target name + a result code. Java static-final factory methods -> C# static methods; parameterless static-final instances -> static readonly fields.</summary>
public class SM_FRIEND_RESPONSE : AionServerPacket
{
    /// <summary>You have successfully added %s to your friend list.</summary>
    public static SM_FRIEND_RESPONSE TARGET_ADDED(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x0);
    }

    /// <summary>That person is offline.</summary>
    public static readonly SM_FRIEND_RESPONSE TARGET_OFFLINE = new SM_FRIEND_RESPONSE(0x1);

    /// <summary>The character is already on your friend list.</summary>
    public static readonly SM_FRIEND_RESPONSE TARGET_ALREADY_FRIEND = new SM_FRIEND_RESPONSE(0x02);

    /// <summary>The character does not exist.</summary>
    public static readonly SM_FRIEND_RESPONSE TARGET_NOT_FOUND = new SM_FRIEND_RESPONSE(0x03);

    /// <summary>%s denied your request to add him.</summary>
    public static SM_FRIEND_RESPONSE TARGET_DENIED(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x04);
    }

    /// <summary>Your friend list is full.</summary>
    public static readonly SM_FRIEND_RESPONSE LIST_FULL = new SM_FRIEND_RESPONSE(0x05);

    /// <summary>You have removed %s from your friend list.</summary>
    public static SM_FRIEND_RESPONSE TARGET_REMOVED(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x06);
    }

    /// <summary>The target cannot be added to your friends list because he has blocked you.</summary>
    public static readonly SM_FRIEND_RESPONSE TARGET_BLOCKED_YOU = new SM_FRIEND_RESPONSE(0x08);

    /// <summary>The specified character is already dead.</summary>
    public static readonly SM_FRIEND_RESPONSE TARGET_DEAD = new SM_FRIEND_RESPONSE(0x09);

    /// <summary>The friend list of %s is full.</summary>
    public static SM_FRIEND_RESPONSE TARGET_LIST_FULL(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x0A);
    }

    /// <summary>%s is currently not online. The friend request has been sent though.</summary>
    public static SM_FRIEND_RESPONSE TARGET_OFFLINE_SENT_REQUEST(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x0B);
    }

    /// <summary>A friend request to %s exists already.</summary>
    public static SM_FRIEND_RESPONSE TARGET_REQUESTED_ALREADY(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x0C);
    }

    /// <summary>No more friend requests can be sent. Reached the maximum number of requests.</summary>
    public static readonly SM_FRIEND_RESPONSE TOO_MANY_REQUESTS = new SM_FRIEND_RESPONSE(0x0D);

    /// <summary>The friend list of %s is full. Accepting requests is not possible anymore.</summary>
    public static SM_FRIEND_RESPONSE REQUESTER_LIST_FULL_CANT_ACCEPT(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x0E);
    }

    /// <summary>(this closes the send friend request window without any chat notification)</summary>
    public static readonly SM_FRIEND_RESPONSE CLOSE_SEND_REQUEST_WINDOW = new SM_FRIEND_RESPONSE(0x11);

    /// <summary>You have denied the friend request from %s.</summary>
    public static SM_FRIEND_RESPONSE REQUEST_DENIED(string requesterName)
    {
        return new SM_FRIEND_RESPONSE(requesterName, 0x12);
    }

    /// <summary>You have already received a request from %s.</summary>
    public static SM_FRIEND_RESPONSE REQUEST_ALREADY_RECEIVED(string targetName)
    {
        return new SM_FRIEND_RESPONSE(targetName, 0x13);
    }

    private readonly string playerName;
    private readonly int code;

    public SM_FRIEND_RESPONSE(int messageType)
        : this("", messageType)
    {
    }

    public SM_FRIEND_RESPONSE(string playerName, int messageType)
    {
        this.playerName = playerName;
        this.code = messageType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(playerName);
        WriteC(code);
    }
}
