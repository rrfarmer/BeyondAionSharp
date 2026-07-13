using Aion.Commons.Network;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_CHANGE_ALLOWED_HDD_SERIAL (ViAl, opcode 11).
/// Notifies the login server of an account's new allowed HDD serial (account computer-lock).
/// </summary>
public sealed class SM_CHANGE_ALLOWED_HDD_SERIAL : LoginServerPacket
{
    private readonly int accountId;
    private readonly string hddSerial;

    public SM_CHANGE_ALLOWED_HDD_SERIAL(Account playerAccount)
    {
        this.accountId = playerAccount.GetId();
        this.hddSerial = playerAccount.GetAllowedHddSerial();
    }

    // Java parity (writeImpl audited 1:1 vs game-server/.../loginserver/serverpackets/SM_CHANGE_ALLOWED_HDD_SERIAL.java): 2026-06-17
    protected override void WritePayload(PacketBuffer buffer)
    {
        // Java parity: SM_CHANGE_ALLOWED_HDD_SERIAL super(11); writeImpl writeD(accountId) + writeS(hddSerial).
        buffer.WriteC(0x0B);
        buffer.WriteD(accountId);
        buffer.WriteS(hddSerial);
    }
}
