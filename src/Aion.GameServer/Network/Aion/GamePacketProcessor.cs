using Aion.GameServer.Utils.Concurrent;

namespace Aion.GameServer.Network.Aion;

public sealed class GamePacketProcessor<TKey> : IAsyncDisposable
	where TKey : notnull
{
	private readonly OrderedTaskExecutor<TKey> _executor;
	private readonly Func<GameClientPacket, CancellationToken, Task> _packetHandler;

	public GamePacketProcessor(Func<GameClientPacket, CancellationToken, Task> packetHandler)
	{
		_packetHandler = packetHandler ?? throw new ArgumentNullException(nameof(packetHandler));
		_executor = new OrderedTaskExecutor<TKey>();
	}

	public Task ProcessAsync(TKey connectionKey, GameClientPacket packet, CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/AionPacketHandler processes packets in connection order.
		ArgumentNullException.ThrowIfNull(packet);
		return _executor.EnqueueAsync(connectionKey, token => _packetHandler(packet, token), cancellationToken);
	}

	public int ActiveConnectionQueueCount => _executor.GetActiveQueueCount();

	public ValueTask DisposeAsync()
	{
		return _executor.DisposeAsync();
	}
}
