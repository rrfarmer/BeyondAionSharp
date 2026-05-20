namespace Aion.ChatServer.Data.Repositories;

public interface IChatLogRepository
{
	Task InsertChatLogAsync(string sender, string message, string type, CancellationToken cancellationToken = default);
}
