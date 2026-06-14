using Aion.Commons.Network;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_CHANGE_ALLOWED_HDD_SERIAL (ViAl, opcode 15).
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

    protected override void WritePayload(PacketBuffer buffer)
    {
        // Java parity: SM_CHANGE_ALLOWED_HDD_SERIAL super(15); writeImpl writeD(accountId) + writeS(hddSerial).
        buffer.WriteC(0x0F);
        buffer.WriteD(accountId);
        buffer.WriteS(hddSerial);
    }
}
