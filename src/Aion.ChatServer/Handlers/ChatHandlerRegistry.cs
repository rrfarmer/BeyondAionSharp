using System.Collections.Concurrent;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Handlers;

public sealed class ChatHandlerRegistry
{
	private readonly ConcurrentDictionary<string, IChatMessageHandler> _handlers = new(StringComparer.Ordinal);
	private readonly ILogger<ChatHandlerRegistry> _logger;

	public ChatHandlerRegistry(IEnumerable<IChatMessageHandler> handlers, ILogger<ChatHandlerRegistry> logger)
	{
		_logger = logger;
		foreach (var handler in handlers)
			RegisterHandler(GetHandlerName(handler), handler);
	}

	public void RegisterHandler(string name, IChatMessageHandler handler)
	{
		_handlers[name] = handler;
		_logger.LogDebug("Registered chat handler {Name} ({Type})", name, handler.GetType().Name);
	}

	public IReadOnlyCollection<IChatMessageHandler> GetHandlers()
	{
		return _handlers.Values.OrderBy(handler => handler.Order).ToArray();
	}

	public async Task ExecuteHandlersAsync(Message message, ChatClient sender, Channel channel, CancellationToken cancellationToken = default)
	{
		foreach (var handler in GetHandlers())
			await handler.HandleAsync(message, sender, channel, cancellationToken);
	}

	private static string GetHandlerName(IChatMessageHandler handler)
	{
		return handler.GetType().GetCustomAttributes(typeof(ChatHandlerAttribute), inherit: false)
			.OfType<ChatHandlerAttribute>()
			.FirstOrDefault()
			?.Name
			?? handler.GetType().Name;
	}
}
