using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmPlayerTransferControl : GsClientPacket
{
	public CmPlayerTransferControl(byte opCode)
		: base(opCode)
	{
	}

	public byte ActionId { get; private set; }

	public int TaskId { get; private set; }

	public string Name { get; private set; } = string.Empty;

	public byte[] Db { get; private set; } = Array.Empty<byte>();

	public string Reason { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		ActionId = buffer.ReadC();
		switch (ActionId)
		{
			case 1:
				TaskId = buffer.ReadD();
				Name = buffer.ReadS();
				Db = buffer.ReadB(buffer.Remaining);
				break;
			case 2:
			case 4:
				TaskId = buffer.ReadD();
				Reason = buffer.ReadS();
				break;
			case 3:
				TaskId = buffer.ReadD();
				break;
		}
	}
}
