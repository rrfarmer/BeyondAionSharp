namespace Aion.ChatServer.Handlers;

public sealed class HandlerVetoException : Exception
{
	public HandlerVetoException(string responseText)
		: base(responseText)
	{
		ResponseText = responseText;
	}

	public string ResponseText { get; }
}
