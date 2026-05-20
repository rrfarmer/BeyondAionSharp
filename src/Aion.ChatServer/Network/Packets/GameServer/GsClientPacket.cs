using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public abstract class GsClientPacket
{
	protected GsClientPacket(byte opCode)
	{
		OpCode = opCode;
	}

	public byte OpCode { get; }

	public void Read(PacketBuffer buffer)
	{
		ReadPayload(buffer);
	}

	public virtual Task RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	protected abstract void ReadPayload(PacketBuffer buffer);
}
