using Aion.Commons.Database;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Data.Repositories;

public sealed class ChatLogRepository : IChatLogRepository
{
	private const string InsertQuery = "INSERT INTO `chatlog` (`sender`, `message`, `type`) VALUES (@sender, @message, @type)";
	private readonly ILogger<ChatLogRepository> _logger;

	public ChatLogRepository(ILogger<ChatLogRepository> logger)
	{
		_logger = logger;
	}

	public async Task InsertChatLogAsync(string sender, string message, string type, CancellationToken cancellationToken = default)
	{
		try
		{
			await using var connection = await DatabaseFactory.GetConnectionAsync();
			await using var command = connection.CreateCommand();
			command.CommandText = InsertQuery;
			command.Parameters.AddWithValue("@sender", sender);
			command.Parameters.AddWithValue("@message", message);
			command.Parameters.AddWithValue("@type", type);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Cannot insert chat message");
		}
	}
}
