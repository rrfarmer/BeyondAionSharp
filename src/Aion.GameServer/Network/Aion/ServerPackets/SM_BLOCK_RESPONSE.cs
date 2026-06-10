using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_BLOCK_RESPONSE (Ben). Responses to block-list requests: writes target name + a result code.</summary>
public class SM_BLOCK_RESPONSE : AionServerPacket
{
    /// <summary>You have blocked %0</summary>
    public const int BLOCK_SUCCESSFUL = 0;
    /// <summary>You have unblocked %0</summary>
    public const int UNBLOCK_SUCCESSFUL = 1;
    /// <summary>That character does not exist.</summary>
    public const int TARGET_NOT_FOUND = 2;
    /// <summary>Your Block List is full.</summary>
    public const int LIST_FULL = 3;
    /// <summary>You cannot block yourself.</summary>
    public const int CANT_BLOCK_SELF = 4;
    /// <summary>This code is sent after editing the note (block reason).</summary>
    public const int EDIT_NOTE = 5;

    private int code;
    private string playerName;

    /// <summary>
    /// Constructs a new block request response packet
    /// </summary>
    /// <param name="code">Message code to use - see class constants</param>
    /// <param name="playerName">Parameters inserted into message. Usually the target player's name</param>
    public SM_BLOCK_RESPONSE(int code, string playerName)
    {
        this.code = code;
        this.playerName = playerName;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(playerName);
        WriteC(code);
    }
}
