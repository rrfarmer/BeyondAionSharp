using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>Java parity: gameserver/network/loginserver/serverpackets/SM_BAN (Watson, opcode 0x06).
/// The universal packet for account/IP bans.</summary>
public sealed class SM_BAN : LoginServerPacket
{
    /// <summary>Ban type 1 = account 2 = IP 3 = Full ban (account and IP).</summary>
    private readonly byte _type;

    /// <summary>Account to ban.</summary>
    private readonly int _accountId;

    /// <summary>IP or mask to ban.</summary>
    private readonly string _ip;

    /// <summary>Time in minutes. 0 = infinity; if time &lt; 0 then it's an unban command.</summary>
    private readonly int _time;

    /// <summary>Object ID of Admin, who requested the ban.</summary>
    private readonly int _adminObjId;

    public SM_BAN(byte type, int accountId, string ip, int time, int adminObjId)
    {
        _type = type;
        _accountId = accountId;
        _ip = ip;
        _time = time;
        _adminObjId = adminObjId;
    }

    protected override void WritePayload(PacketBuffer buffer)
    {
        buffer.WriteC(0x06);
        buffer.WriteC(_type);
        buffer.WriteD(_accountId);
        buffer.WriteS(_ip);
        buffer.WriteD(_time);
        buffer.WriteD(_adminObjId);
    }
}
