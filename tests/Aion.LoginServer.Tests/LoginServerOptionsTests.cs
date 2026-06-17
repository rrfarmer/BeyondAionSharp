using System.Net;
using Aion.Commons.Configuration;
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

	/// <summary>
	/// Full-Parity §C proof: the login-server Config holder is now [Property]-migrated and bound by
	/// ConfigurableProcessor with verbatim Java keys/defaults. An override fed through Config.LoadFrom flows into the
	/// static field, and absent keys keep their annotated defaults — the same hard-contract binding Java's Config.load
	/// performs over config/main + config/network + myls.properties.
	/// </summary>
	[Fact]
	public void Config_PropertyHolder_BindsDefaults_AndHonorsOverrides()
	{
		try
		{
			// Baseline: empty properties -> annotated [Property] defaults (verbatim Java).
			Config.LoadFrom(new JavaProperties());
			Assert.Equal("0.0.0.0:2106", Config.CLIENT_SOCKET_ADDRESS);
			Assert.Equal("0.0.0.0:9014", Config.GAMESERVER_SOCKET_ADDRESS);
			Assert.Equal(5, Config.LOGIN_TRY_BEFORE_BAN);
			Assert.Equal(15, Config.WRONG_LOGIN_BAN_TIME);
			Assert.Equal(0, Config.NIO_READ_WRITE_THREADS);
			Assert.True(Config.ACCOUNT_AUTO_CREATION);
			Assert.True(Config.ENABLE_BRUTEFORCE_PROTECTION);
			Assert.False(Config.LOG_LOGINS);
			Assert.Equal(string.Empty, Config.EXTERNAL_AUTH_URL);
			Assert.False(Config.UseExternalAuth());

			// Override every key to a value differing from the default and prove flow-through (binding precedence).
			var props = new JavaProperties();
			props.SetProperty("loginserver.network.client.socket_address", "127.0.0.1:3000");
			props.SetProperty("loginserver.network.gameserver.socket_address", "127.0.0.1:4000");
			props.SetProperty("loginserver.network.client.logintrybeforeban", "9");
			props.SetProperty("loginserver.network.client.bantimeforbruteforcing", "30");
			props.SetProperty("loginserver.network.nio.threads", "4");
			props.SetProperty("loginserver.accounts.autocreate", "false");
			props.SetProperty("loginserver.server.bruteforceprotector", "false");
			props.SetProperty("loginserver.log.logins", "true");
			props.SetProperty("loginserver.accounts.external_auth.url", "https://auth.example/check");
			Config.LoadFrom(props);

			Assert.Equal("127.0.0.1:3000", Config.CLIENT_SOCKET_ADDRESS);
			Assert.Equal("127.0.0.1:4000", Config.GAMESERVER_SOCKET_ADDRESS);
			Assert.Equal(9, Config.LOGIN_TRY_BEFORE_BAN);
			Assert.Equal(30, Config.WRONG_LOGIN_BAN_TIME);
			Assert.Equal(4, Config.NIO_READ_WRITE_THREADS);
			Assert.False(Config.ACCOUNT_AUTO_CREATION);
			Assert.False(Config.ENABLE_BRUTEFORCE_PROTECTION);
			Assert.True(Config.LOG_LOGINS);
			Assert.Equal("https://auth.example/check", Config.EXTERNAL_AUTH_URL);
			Assert.True(Config.UseExternalAuth());
		}
		finally
		{
			// Restore annotated defaults so the global static holder does not leak into sibling tests.
			Config.LoadFrom(new JavaProperties());
		}
	}
}
