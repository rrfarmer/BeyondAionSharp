namespace Aion.GameServer.Utils.IdFactory;

public sealed class IDFactoryException : Exception
{
	public IDFactoryException(string message)
		: base(message)
	{
	}

	public IDFactoryException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
