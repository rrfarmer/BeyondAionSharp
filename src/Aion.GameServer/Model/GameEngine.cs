namespace Aion.GameServer.Model;

public interface GameEngine
{
	string Name { get; }

	ValueTask InitAsync(CancellationToken cancellationToken = default);

	ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}
