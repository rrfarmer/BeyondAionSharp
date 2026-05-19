using Aion.Commons.Configuration;
using Aion.Commons.Database;
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
			var databaseOptions = DatabaseOptions.LoadFromJavaConfig(Directory.GetCurrentDirectory());
			DatabaseFactory.Initialize(databaseOptions);
			services.AddSingleton(options);
			services.AddSingleton<IAccountTimeRepository, AccountTimeRepository>();
			services.AddSingleton<IAccountRepository, AccountRepository>();
			services.AddSingleton<IBannedIpRepository, BannedIpRepository>();
			services.AddSingleton<IBannedMacRepository, BannedMacRepository>();
			services.AddSingleton<IBannedHddRepository, BannedHddRepository>();
			services.AddSingleton<IGameServersRepository, GameServersRepository>();
			services.AddSingleton<IPremiumRepository, PremiumRepository>();
			services.AddSingleton<IAccountsLogRepository, AccountsLogRepository>();
			services.AddSingleton<IPlayerTransferRepository, PlayerTransferRepository>();
			services.AddSingleton<IBannedIpService, BannedIpService>();
			services.AddSingleton<IBannedMacService, BannedMacService>();
			services.AddSingleton<IBannedHddService, BannedHddService>();
			services.AddSingleton<IBruteForceProtector, BruteForceProtector>();
			services.AddSingleton<IExternalAuthClient>(serviceProvider => new ExternalAuthClient(new HttpClient(), serviceProvider.GetRequiredService<ILogger<ExternalAuthClient>>()));
			services.AddSingleton<ILoginAuthService, LoginAuthService>();
			services.AddSingleton<IPlayerTransferService, PlayerTransferService>();
			services.AddSingleton<IPlayerTransferScheduler, PlayerTransferScheduler>();
			services.AddSingleton<ILoginSessionRegistry, LoginSessionRegistry>();
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
