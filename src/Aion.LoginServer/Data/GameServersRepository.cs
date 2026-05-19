using Aion.Commons.Database;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Data;

public interface IGameServersRepository
{
	Task<IReadOnlyDictionary<byte, GameServerInfo>> GetAllGameServersAsync(CancellationToken cancellationToken = default);
}

public sealed class GameServersRepository : IGameServersRepository
{
	public async Task<IReadOnlyDictionary<byte, GameServerInfo>> GetAllGameServersAsync(CancellationToken cancellationToken = default)
	{
		var result = new Dictionary<byte, GameServerInfo>();
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT * FROM gameservers";
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var id = reader.GetByte("id");
			result[id] = new GameServerInfo(id, reader.GetString("mask"), reader.GetString("password"));
		}
		return result;
	}
}
