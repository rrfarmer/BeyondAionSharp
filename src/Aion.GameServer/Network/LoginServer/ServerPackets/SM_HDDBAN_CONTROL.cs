using Aion.Commons.Network;
using Aion.GameServer.Services.Ban;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>Java parity: gameserver/network/loginserver/serverpackets/SM_HDDBAN_CONTROL (super opcode 14).</summary>
public sealed class SM_HDDBAN_CONTROL : LoginServerPacket
{
    private readonly BanAction _action;
    private readonly string _serial;
    private readonly long _time;

    public SM_HDDBAN_CONTROL(BanAction action, string address, long time)
    {
        _action = action;
        _serial = address;
        _time = time;
    }

    protected override void WritePayload(PacketBuffer buffer)
    {
        buffer.WriteC(14);
        buffer.WriteC(_action.GetId());
        buffer.WriteS(_serial);
        buffer.WriteQ(_time);
    }
}
