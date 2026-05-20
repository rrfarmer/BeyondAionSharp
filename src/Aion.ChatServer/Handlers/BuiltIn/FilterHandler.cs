using Aion.ChatServer.Configuration;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;

namespace Aion.ChatServer.Handlers.BuiltIn;

[ChatHandler("filter")]
public sealed class FilterHandler : IChatMessageHandler
{
	private readonly IReadOnlyCollection<string> _blockedKeywords;

	public FilterHandler(ChatServerOptions options)
	{
		_blockedKeywords = options.FilteredKeywords;
	}

	public FilterHandler(IEnumerable<string> blockedKeywords)
	{
		_blockedKeywords = blockedKeywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).ToArray();
	}

	public int Order => 10;

	public Task HandleAsync(Message message, ChatClient sender, Channel channel, CancellationToken cancellationToken = default)
	{
		if (_blockedKeywords.Count == 0)
			return Task.CompletedTask;

		var text = message.TextString;
		foreach (var keyword in _blockedKeywords)
		{
			text = text.Replace(keyword, new string('*', keyword.Length), StringComparison.OrdinalIgnoreCase);
		}

		if (!string.Equals(text, message.TextString, StringComparison.Ordinal))
			message.SetText(text);

		return Task.CompletedTask;
	}
}
