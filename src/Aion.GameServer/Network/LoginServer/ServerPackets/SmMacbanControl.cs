using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>Java parity: gameserver/network/loginserver/serverpackets/SM_MACBAN_CONTROL (KID, opcode 9).</summary>
public sealed class SmMacbanControl : LoginServerPacket
{
	private readonly byte _type;
	private readonly string _address;
	private readonly string _details;
	private readonly long _time;

	public SmMacbanControl(byte type, string address, long time, string details)
	{
		_type = type;
		_address = address;
		_time = time;
		_details = details;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_MACBAN_CONTROL.writeImpl.
		buffer.WriteC(9);
		buffer.WriteC(_type);
		buffer.WriteS(_address);
		buffer.WriteS(_details);
		buffer.WriteQ(_time);
	}
}
