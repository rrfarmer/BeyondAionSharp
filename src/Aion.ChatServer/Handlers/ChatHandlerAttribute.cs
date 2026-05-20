namespace Aion.ChatServer.Handlers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ChatHandlerAttribute : Attribute
{
	public ChatHandlerAttribute(string name)
	{
		Name = name;
	}

	public string Name { get; }
}
