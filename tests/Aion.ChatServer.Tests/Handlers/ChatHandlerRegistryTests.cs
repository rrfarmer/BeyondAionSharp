using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Handlers.BuiltIn;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests.Handlers;

public class ChatHandlerRegistryTests
{
	[Fact]
	public async Task FloodProtectionHandler_UpdatesFirstMessageAndVetoesImmediateSecondMessage()
	{
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, 0);
		var channel = new RegionChannel(1, Race.Elyos, "ALL");
		var message = new Message(channel, [], client);
		var handler = new FloodProtectionHandler();

		await handler.HandleAsync(message, client, channel);
		var veto = await Assert.ThrowsAsync<HandlerVetoException>(() => handler.HandleAsync(message, client, channel));

		Assert.Equal("You can chat again in this channel in 1 second.", veto.ResponseText);
	}

	[Fact]
	public async Task FilterHandler_ReplacesConfiguredKeywords()
	{
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, 0);
		var channel = new RegionChannel(1, Race.Elyos, "ALL");
		var message = new Message(channel, System.Text.Encoding.Unicode.GetBytes("hello badword"), client);
		var handler = new FilterHandler(["badword"]);

		await handler.HandleAsync(message, client, channel);

		Assert.Equal("hello *******", message.TextString);
	}

	[Fact]
	public async Task LoggingHandler_WritesToRepositoryWhenConfigured()
	{
		var repository = new RecordingChatLogRepository();
		var options = new ChatServerOptions { LogChatToDatabase = true };
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, 0);
		var channel = new RegionChannel(1, Race.Elyos, "ALL");
		var message = new Message(channel, System.Text.Encoding.Unicode.GetBytes("hello"), client);
		var handler = new LoggingHandler(options, repository, NullLogger<LoggingHandler>.Instance);

		await handler.HandleAsync(message, client, channel);

		Assert.Equal(("Daeva", "hello", channel.Name()), repository.LastRecord);
	}

	[Fact]
	public void Registry_OrdersHandlersByOrder()
	{
		var registry = new ChatHandlerRegistry([], NullLogger<ChatHandlerRegistry>.Instance);
		registry.RegisterHandler("late", new OrderedHandler(10));
		registry.RegisterHandler("early", new OrderedHandler(0));

		Assert.Equal([0, 10], registry.GetHandlers().Select(handler => handler.Order));
	}

	private sealed class RecordingChatLogRepository : IChatLogRepository
	{
		public (string Sender, string Message, string Type)? LastRecord { get; private set; }

		public Task InsertChatLogAsync(string sender, string message, string type, CancellationToken cancellationToken = default)
		{
			LastRecord = (sender, message, type);
			return Task.CompletedTask;
		}
	}

	private sealed class OrderedHandler : IChatMessageHandler
	{
		public OrderedHandler(int order)
		{
			Order = order;
		}

		public int Order { get; }

		public Task HandleAsync(Message message, ChatClient sender, Channel channel, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}
}
