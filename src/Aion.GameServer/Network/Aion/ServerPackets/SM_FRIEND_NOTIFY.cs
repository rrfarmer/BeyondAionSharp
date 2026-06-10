using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FRIEND_NOTIFY (Ben). Notifies a player when a friend logs in/out or deletes them. AionServerPacket red-tolerated.</summary>
public class SM_FRIEND_NOTIFY : AionServerPacket
{
    /// <summary>Buddy has logged in (or become visible)</summary>
    public const byte LOGIN = 0;
    /// <summary>Buddy has logged out (or become invisible)</summary>
    public const byte LOGOUT = 1;
    /// <summary>Buddy has deleted you</summary>
    public const byte DELETED = 2;

    private readonly byte code;
    private readonly string name;

    public SM_FRIEND_NOTIFY(byte code, string name)
    {
        this.code = code;
        this.name = name;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(name);
        WriteC(code);
    }
}
