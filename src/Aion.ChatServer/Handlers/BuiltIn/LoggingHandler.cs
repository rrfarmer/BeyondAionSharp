using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Handlers.BuiltIn;

[ChatHandler("logging")]
public sealed class LoggingHandler : IChatMessageHandler
{
	private readonly ChatServerOptions _options;
	private readonly IChatLogRepository _chatLogRepository;
	private readonly ILogger<LoggingHandler> _logger;

	public LoggingHandler(ChatServerOptions options, IChatLogRepository chatLogRepository, ILogger<LoggingHandler> logger)
	{
		_options = options;
		_chatLogRepository = chatLogRepository;
		_logger = logger;
	}

	public int Order => 100;

	public async Task HandleAsync(Message message, ChatClient sender, Channel channel, CancellationToken cancellationToken = default)
	{
		if (_options.LogChat)
			_logger.LogInformation("[{Channel}] {Sender}: {Message}", channel.Name(), sender.Name, message.TextString);
		if (_options.LogChatToDatabase)
			await _chatLogRepository.InsertChatLogAsync(sender.Name, message.TextString, channel.Name(), cancellationToken);
	}
}
