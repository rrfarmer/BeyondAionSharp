using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network;
using Aion.ChatServer.Services;
using Aion.Commons.Database;
using Aion.Commons.Logging;
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
			var options = ChatServerOptions.LoadFromJavaConfig(Directory.GetCurrentDirectory());
			var databaseOptions = ChatServerOptions.LoadDatabaseOptionsFromJavaConfig(Directory.GetCurrentDirectory());
			DatabaseFactory.Initialize(databaseOptions);

			services.AddSingleton(options);
			services.AddSingleton<ChatChannels>();
			services.AddSingleton<IChatLogRepository, ChatLogRepository>();
			services.AddSingleton<IBroadcastService, BroadcastService>();
			services.AddSingleton<IChatService, ChatService>();
			services.AddSingleton<IGameServerService, GameServerService>();
			services.AddSingleton<ClientSocketServer>();
			services.AddSingleton<GameServerSocketServer>();
			services.AddHostedService<ChatServerHostedService>();
		}
	)
	.ConfigureLogging(
		(hostContext, logging) =>
		{
			logging.ClearProviders();
			logging.AddConsole();
			logging.AddProvider(new AionFileLoggerProvider(ResolveJavaModuleLogDirectory("chat-server")));
			if (hostContext.HostingEnvironment.IsDevelopment())
			{
				logging.AddDebug();
			}
		}
	);

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Aion Chat Server starting...");

await host.RunAsync();

logger.LogInformation("Aion Chat Server stopped.");

static string ResolveJavaModuleLogDirectory(string moduleName)
{
	var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
	while (directory != null)
	{
		if (Directory.Exists(Path.Combine(directory.FullName, moduleName, "config")))
			return Path.Combine(directory.FullName, moduleName, "log");
		directory = directory.Parent;
	}

	return Path.Combine(Directory.GetCurrentDirectory(), "log");
}
