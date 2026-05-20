using Aion.Commons.Network;
using Aion.ChatServer.Models;

namespace Aion.ChatServer.Network.Packets;

public abstract class AbstractClientPacket
{
	protected AbstractClientPacket(byte opCode)
	{
		OpCode = opCode;
	}

	public byte OpCode { get; }

	public void Read(PacketBuffer buffer)
	{
		ReadPayload(buffer);
	}

	public virtual Task RunAsync(ChatClient? client, IServiceProvider services, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	protected abstract void ReadPayload(PacketBuffer buffer);
}
