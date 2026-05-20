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
		Assert.Equal(3306, options.Port);
		Assert.Equal(5, options.MaxPoolSize);
		Assert.Equal(5000, options.ConnectionTimeout);
	}
}
