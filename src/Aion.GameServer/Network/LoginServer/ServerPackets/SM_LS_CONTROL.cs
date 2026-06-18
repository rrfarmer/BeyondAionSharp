using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>Java parity: gameserver/network/loginserver/serverpackets/SM_LS_CONTROL (Aionchs-Wylovech, opcode 0x05).
/// Requests the login server change an account's access (type 1) or membership (type 2) level.</summary>
public sealed class SM_LS_CONTROL : LoginServerPacket
{
    private readonly int _type, _param, _accountId, _adminId;

    public SM_LS_CONTROL(int type, int param, Player player, Player admin)
    {
        _type = type;
        _param = param;
        _accountId = player.GetAccount().GetId();
        _adminId = admin.GetObjectId();
    }

    // Java parity (writeImpl 1:1 vs game-server/.../loginserver/serverpackets/SM_LS_CONTROL.java).
    protected override void WritePayload(PacketBuffer buffer)
    {
        buffer.WriteC(0x05);
        buffer.WriteC(_type);
        buffer.WriteC(_param);
        buffer.WriteD(_accountId);
        buffer.WriteD(_adminId);
    }
}
