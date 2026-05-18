using Aion.LoginServer.Configuration;
using Aion.LoginServer.Network;
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
