using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(
		(hostContext, config) =>
		{
			config.AddEnvironmentVariables();
		}
	)
	.ConfigureServices(
		(hostContext, services) =>
		{
			var options = LoginServerOptions.LoadFromJavaConfig(Directory.GetCurrentDirectory());
			services.AddSingleton(options);
			services.AddSingleton<IAccountTimeRepository, AccountTimeRepository>();
			services.AddSingleton<IAccountRepository, AccountRepository>();
			services.AddSingleton<IBannedIpRepository, BannedIpRepository>();
			services.AddSingleton<IGameServersRepository, GameServersRepository>();
			services.AddSingleton<IPremiumRepository, PremiumRepository>();
			services.AddSingleton<ILoginAuthService, LoginAuthService>();
			services.AddSingleton<ILoginKeyGenerator, LoginKeyGenerator>();
			services.AddSingleton<IGameServerRegistry, GameServerRegistry>();
			services.AddSingleton<LoginClientSocketServer>();
			services.AddSingleton<GameServerSocketServer>();
			services.AddHostedService<LoginServerHostedService>();
		}
	)
	.ConfigureLogging(
		(hostContext, logging) =>
		{
			logging.ClearProviders();
			logging.AddConsole();
			if (hostContext.HostingEnvironment.IsDevelopment())
			{
				logging.AddDebug();
			}
		}
	);

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Aion Login Server starting...");

await host.RunAsync();

logger.LogInformation("Aion Login Server stopped.");
