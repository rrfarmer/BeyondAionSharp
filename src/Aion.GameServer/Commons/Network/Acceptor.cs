using Aion.Commons.Nio.Channels;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/Acceptor (-Nemesiss-). Attachment of the ServerSocketChannel accept-key;
/// creates a new AConnection via ConnectionFactory for each accepted socket and registers it on a
/// ReadWrite Dispatcher for read ops.
/// </summary>
public class Acceptor
{
    private readonly ConnectionFactory factory;
    private readonly NioServer nioServer;

    internal Acceptor(ConnectionFactory factory, NioServer nioServer)
    {
        this.factory = factory;
        this.nioServer = nioServer;
    }

    public void Accept(SelectionKey key)
    {
        ServerSocketChannel serverSocketChannel = (ServerSocketChannel)key.Channel();
        SocketChannel? socketChannel = serverSocketChannel.Accept();
        if (socketChannel == null)
            return;
        socketChannel.ConfigureBlocking(false);
        socketChannel.Socket().SetSoLinger(true, 10);
        socketChannel.Socket().SetTcpNoDelay(true);

        Dispatcher dispatcher = nioServer.GetReadWriteDispatcher();
        AConnection con = factory.Create(socketChannel, dispatcher);

        if (con == null)
        {
            socketChannel.Close();
            return;
        }

        dispatcher.Register(socketChannel, SelectionKey.OP_READ, con);
        con.InitializedInternal();
    }
}
