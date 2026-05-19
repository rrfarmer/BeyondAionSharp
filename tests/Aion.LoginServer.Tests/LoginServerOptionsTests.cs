using System.Net;
using Aion.LoginServer.Configuration;

namespace Aion.LoginServer.Tests;

public class LoginServerOptionsTests
{
	[Fact]
	public void LoadFromJavaConfig_ReadsKnownLoginServerProperties()
	{
		var options = LoginServerOptions.LoadFromJavaConfig(AppContext.BaseDirectory);

		Assert.Equal(new IPEndPoint(IPAddress.Any, 2106), options.ClientEndPoint);
		Assert.Equal(new IPEndPoint(IPAddress.Any, 9014), options.GameServerEndPoint);
		Assert.Equal(5, options.LoginTryBeforeBan);
		Assert.Equal(15, options.WrongLoginBanMinutes);
		Assert.Equal(0, options.NioReadWriteThreads);
		Assert.True(options.AutoCreateAccounts);
		Assert.True(options.BruteForceProtectionEnabled);
		Assert.False(options.LogGameServerLogins);
		Assert.False(options.UseExternalAuth);
	}
}
