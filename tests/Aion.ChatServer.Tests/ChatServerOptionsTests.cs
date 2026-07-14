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
		Assert.InRange(options.Port, 1, 65535);
		Assert.Equal(5, options.MaxPoolSize);
		Assert.Equal(5000, options.ConnectionTimeout);
		Assert.Equal("utf8mb4", options.CharacterSet);
		Assert.Null(options.ConnectionTimeZone);
		Assert.Null(options.SslMode);
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

	[Fact]
	public void ProgramOptionsPath_UsesJavaPropertiesGrammarAndTransformers()
	{
		var root = Path.Combine(Path.GetTempPath(), $"AionChatJavaProperties_{Guid.NewGuid()}");
		try
		{
			var configRoot = Path.Combine(root, "chat-server", "config");
			Directory.CreateDirectory(Path.Combine(configRoot, "main"));
			Directory.CreateDirectory(Path.Combine(configRoot, "network"));
			File.WriteAllText(
				Path.Combine(configRoot, "mycs.properties"),
				"""
				chatserver.log.chat 1
				chatserver.log.chat_to_db:0
				chatserver.network.nio.threads = \u0037
				chatserver.network.gameserver.password = pa\=ss\
				    word
				chatserver.network.client.socket_address = 127.0.0.1\:10400
				chatserver.network.client.connect_address = ${chatserver.network.client.socket_address}
				"""
			);

			// Program.cs constructs its singleton with this exact public factory.
			var options = ChatServerOptions.LoadFromJavaConfig(root);

			Assert.True(options.LogChat);
			Assert.False(options.LogChatToDatabase);
			Assert.Equal(7, options.NioReadWriteThreads);
			Assert.Equal("pa=ssword", options.GameServerPassword);
			Assert.Equal(10400, options.ClientEndPoint.Port);
			Assert.Equal(options.ClientEndPoint, options.ClientConnectEndPoint);
		}
		finally
		{
			Config.LoadFrom(new JavaProperties());
			if (Directory.Exists(root))
				Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void ProgramOptionsPath_RejectsInvalidJavaBoolean()
	{
		var root = Path.Combine(Path.GetTempPath(), $"AionChatInvalidBoolean_{Guid.NewGuid()}");
		try
		{
			var configRoot = Path.Combine(root, "chat-server", "config");
			Directory.CreateDirectory(Path.Combine(configRoot, "main"));
			Directory.CreateDirectory(Path.Combine(configRoot, "network"));
			File.WriteAllText(Path.Combine(configRoot, "mycs.properties"), "chatserver.log.chat = enabled\n");

			var exception = Assert.Throws<InvalidOperationException>(() => ChatServerOptions.LoadFromJavaConfig(root));
			Assert.IsType<TransformationException>(exception.InnerException);
		}
		finally
		{
			Config.LoadFrom(new JavaProperties());
			if (Directory.Exists(root))
				Directory.Delete(root, recursive: true);
		}
	}
}
