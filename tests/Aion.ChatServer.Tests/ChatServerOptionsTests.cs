using Aion.ChatServer.Configuration;

namespace Aion.ChatServer.Tests;

public class ChatServerOptionsTests
{
	[Fact]
	public void LoadFromJavaConfig_LoadsChatNetworkDefaults()
	{
		var options = ChatServerOptions.LoadFromJavaConfig(Directory.GetCurrentDirectory());

		Assert.Equal(10241, options.ClientEndPoint.Port);
		Assert.Equal(10241, options.ClientConnectEndPoint.Port);
		Assert.Equal(9021, options.GameServerEndPoint.Port);
		Assert.Equal(1, options.NioReadWriteThreads);
	}

	[Fact]
	public void LoadDatabaseOptionsFromJavaConfig_LoadsChatDatabaseDefaults()
	{
		var options = ChatServerOptions.LoadDatabaseOptionsFromJavaConfig(Directory.GetCurrentDirectory());

		Assert.Equal("aion_cs", options.Database);
		// Faithful to disk: chat-server/config/network/database.properties defaults to 3306, but the highest-precedence
		// per-instance override (chat-server/config/mycs.properties) points database.url at localhost:3307. The cascade
		// applies the my* file last, exactly as Java's PropertiesUtils does, so the loaded value is 3307.
		Assert.Equal(3307, options.Port);
		Assert.Equal(5, options.MaxPoolSize);
		Assert.Equal(5000, options.ConnectionTimeout);
	}
}
