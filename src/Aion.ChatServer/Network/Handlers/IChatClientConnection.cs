using Aion.ChatServer.Network.Packets;

namespace Aion.ChatServer.Network.Handlers;

public interface IChatClientConnection
{
	Task SendPacketAsync(AbstractServerPacket packet, CancellationToken cancellationToken = default);

	Task CloseAsync(CancellationToken cancellationToken = default);
}
