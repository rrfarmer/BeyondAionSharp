using Aion.Commons.Nio.Channels;

namespace Aion.GameServer.Commons.Network;

/// <summary>Java parity: commons/network/ConnectionFactory (-Nemesiss-). Factory for AConnection impls, used by Acceptor.</summary>
public interface ConnectionFactory
{
    /// <summary>Create a new AConnection for the accepted socket, registered to the given dispatcher.</summary>
    AConnection Create(SocketChannel socket, Dispatcher dispatcher);
}
