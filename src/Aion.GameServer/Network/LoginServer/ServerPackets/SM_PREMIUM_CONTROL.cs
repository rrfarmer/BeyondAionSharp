using Aion.Commons.Network;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.Ingameshop;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>Java parity: gameserver/network/loginserver/serverpackets/SM_PREMIUM_CONTROL (super opcode 11).</summary>
public sealed class SM_PREMIUM_CONTROL : LoginServerPacket
{
    private readonly IGRequest _request;
    private readonly long _cost;

    public SM_PREMIUM_CONTROL(IGRequest request, long cost)
    {
        _request = request;
        _cost = cost;
    }

    // Java parity (writeImpl audited 1:1 vs game-server/.../loginserver/serverpackets/SM_PREMIUM_CONTROL.java): 2026-06-17
    protected override void WritePayload(PacketBuffer buffer)
    {
        buffer.WriteC(11);
        buffer.WriteD(_request.accountId);
        buffer.WriteD(_request.requestId);
        buffer.WriteQ(_cost);
        buffer.WriteC(NetworkConfig.GAMESERVER_ID);
    }
}
