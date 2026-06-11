using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ENTER_WORLD_CHECK (-Nemesiss-). Connection-status message box for the selected character. Nested Msg enum ids are sequential (0..6 = ordinal) so a plain C# enum + (byte) cast is faithful.</summary>
public class SM_ENTER_WORLD_CHECK : AionServerPacket
{
    private byte msg;

    public SM_ENTER_WORLD_CHECK(Msg msg)
    {
        this.msg = (byte)msg;
    }

    public SM_ENTER_WORLD_CHECK()
        : this(Msg.OK)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(msg);
        WriteC(0x00);
        WriteC(0x00);
    }

    public enum Msg
    {
        /// <summary>indicates that enter world was successful</summary>
        OK = 0,

        /// <summary>The selected character is already playing on the selected server.</summary>
        CHAR_ALREADY_ONLINE = 1,

        /// <summary>The connection to the game server has failed. (this disconnects and closes the client)</summary>
        CONNECTION_ERROR = 2,

        /// <summary>Characters of both factions exist on the server.</summary>
        BOTH_FACTIONS = 3,

        /// <summary>You cannot connect to the game during character reservation time.</summary>
        RESERVATION_TIME = 4,

        /// <summary>You have exceeded the limit of characters for an account and must delete some to be able to play again.</summary>
        TOO_MANY_CHARACTERS = 5,

        /// <summary>Reconnection of the character is being prepared (max. 20s).</summary>
        REENTRY_TIME = 6,
    }
}
