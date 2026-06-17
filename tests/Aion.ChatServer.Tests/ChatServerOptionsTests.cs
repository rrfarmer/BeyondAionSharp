using Aion.ChatServer.Configuration;
using Aion.Commons.Configuration;

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

	[Fact]
	public void Load_BindsRealCheckedInChatConfig_ResolvingConnectAddressReference()
	{
		// Loads the REAL checked-in chat-server/config/*.properties through the [Property] holder + ConfigurableProcessor
		// (the exact Java Config.load() path). Asserts the on-disk network.properties values bind, including the
		// connect_address = ${chatserver.network.client.socket_address} placeholder the processor resolves verbatim.
		Config.Load(Directory.GetCurrentDirectory());

		Assert.Equal("0.0.0.0:10241", Config.CLIENT_SOCKET_ADDRESS);
		Assert.Equal("0.0.0.0:10241", Config.CLIENT_CONNECT_ADDRESS);
		Assert.Equal("0.0.0.0:9021", Config.GAMESERVER_SOCKET_ADDRESS);
		Assert.Equal(1, Config.NIO_READ_WRITE_THREADS);
	}

	[Fact]
	public void Load_HighestPrecedenceOverrideWins_OnMigratedField()
	{
		// Override-proof: build the same cascade Java uses (config/main + config/network defaults), then apply a
		// per-instance override last (as mycs.properties does), and assert the migrated [Property] field takes the
		// overridden value — proving the holder honors Java precedence, not just on-disk defaults.
		var defaults = Config.LoadProperties(Directory.GetCurrentDirectory());
		var overridden = new JavaProperties(defaults);
		overridden.SetProperty("chatserver.network.nio.threads", "7");
		overridden.SetProperty("chatserver.log.chat", "true");
		overridden.SetProperty("chatserver.network.gameserver.password", "secret");

		Config.LoadFrom(overridden);

		Assert.Equal(7, Config.NIO_READ_WRITE_THREADS);
		Assert.True(Config.LOG_CHAT);
		Assert.Equal("secret", Config.GAMESERVER_PASSWORD);
	}
}
